using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Vtp.VR.Rest;
using Debug = UnityEngine.Debug;

namespace Vtp.VR.LocalAlgorithmExe
{
    /// <summary>
    /// Windows版Unity与RmgAlgorithmEngine.exe之间的文件交换和进程调用层。
    /// 算法在独立进程运行，不阻塞Unity主线程。
    /// </summary>
    internal sealed class RmgExeProcessClient
    {
        private const string PayloadFolderName =
            "RmgAlgorithmEngine";
        private const string EngineFileName =
            "RmgAlgorithmEngine.exe";
        private const string ConfigFileName =
            "engine_config.json";
        private Process _activeProcess;

        public IEnumerator Run(
            string taskId,
            string algorithm,
            JObject suppliedParameters,
            SceneNumericalExampleDto[] examples,
            SceneNumericalExampleDto[] originalExamples,
            string requiredTaskType,
            int timeoutSeconds,
            Action<string> onProgress,
            Action<RmgExeRunResponse, string> onSuccess,
            Action<string> onError)
        {
            if (Application.platform != RuntimePlatform.WindowsEditor &&
                Application.platform != RuntimePlatform.WindowsPlayer)
            {
                InvokeError(
                    onError,
                    "当前算法包是Windows EXE，只能在Windows编辑器或Windows程序中运行。");
                yield break;
            }

            string normalizedAlgorithm =
                (algorithm ?? string.Empty).Trim().ToLowerInvariant();

            JObject parameters;
            string parameterError;
            if (!RmgExeParameterFactory.TryNormalizeAndValidate(
                    normalizedAlgorithm,
                    suppliedParameters,
                    out parameters,
                    out parameterError))
            {
                InvokeError(
                    onError,
                    "算法参数校验失败：" + parameterError);
                yield break;
            }

            string engineDirectory;
            string enginePath;
            string configPath;
            string prepareError;
            if (!TryPrepareRuntime(
                    out engineDirectory,
                    out enginePath,
                    out configPath,
                    out prepareError))
            {
                InvokeError(onError, prepareError);
                yield break;
            }

            string runId = Guid.NewGuid().ToString("N");
            string runtimeDirectory =
                GetProjectRuntimeDirectory();
            string exchangeDirectory =
                Path.Combine(runtimeDirectory, "exchange");
            string resultsDirectory =
                Path.Combine(runtimeDirectory, "results");

            // PyInstaller、请求文件和结果文件全部使用项目/成品目录，
            // 不再把算法发布资源或运行文件放到 AppData/LocalLow。
            string processTempDirectory =
                Path.Combine(
                    runtimeDirectory,
                    "temp",
                    runId.Substring(0, 8));

            try
            {
                Directory.CreateDirectory(exchangeDirectory);
                Directory.CreateDirectory(resultsDirectory);
                Directory.CreateDirectory(processTempDirectory);
            }
            catch (Exception exception)
            {
                InvokeError(
                    onError,
                    "项目内算法运行目录不可写：" +
                    runtimeDirectory +
                    "。" + exception.Message);
                yield break;
            }

            string safeTaskId = MakeSafeFileName(taskId, "task");
            string fileStem =
                safeTaskId +
                "_" +
                normalizedAlgorithm +
                "_" +
                DateTime.Now.ToString("yyyyMMdd_HHmmss") +
                "_" +
                runId.Substring(0, 8);

            string requestPath =
                Path.Combine(
                    exchangeDirectory,
                    fileStem + "_request.json");
            string responsePath =
                Path.Combine(
                    exchangeDirectory,
                    fileStem + "_response.json");
            string outputExcelPath =
                Path.Combine(
                    resultsDirectory,
                    fileStem + ".xlsx");
            string inputExcelPath =
                Path.Combine(
                    exchangeDirectory,
                    fileStem + "_input.xlsx");
            string runConfigPath =
                Path.Combine(
                    exchangeDirectory,
                    fileStem + "_config.json");

            int inputCount;
            string workbookError;
            if (!RmgCaseWorkbookBridge.TryWriteExamples(
                    inputExcelPath,
                    examples,
                    requiredTaskType,
                    originalExamples,
                    out inputCount,
                    out workbookError))
            {
                InvokeError(onError, workbookError);
                yield break;
            }

            // EXE 0.2.0不能直接接收Unity的JSON算例，因此为本次运行
            // 生成独立配置，关闭EXE自己的FastAPI取数，并让它只读取上面
            // 由当前缓存算例生成的输入Excel。
            try
            {
                JObject runConfig = JObject.Parse(
                    File.ReadAllText(configPath, Encoding.UTF8));
                JObject instanceApi =
                    runConfig["instance_api"] as JObject ??
                    new JObject();
                instanceApi["enabled"] = false;
                runConfig["instance_api"] = instanceApi;
                runConfig["local_instance_file"] = inputExcelPath;
                runConfig["output_directory"] = resultsDirectory;

                File.WriteAllText(
                    runConfigPath,
                    runConfig.ToString(Formatting.Indented),
                    new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                InvokeError(
                    onError,
                    "无法生成本次算法配置：" +
                    exception.Message);
                yield break;
            }

            JObject request = new JObject
            {
                ["algorithm"] = normalizedAlgorithm,
                ["parameters"] = parameters,
                ["output_file"] = outputExcelPath
            };

            try
            {
                File.WriteAllText(
                    requestPath,
                    request.ToString(Formatting.Indented),
                    new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                InvokeError(
                    onError,
                    "无法写入算法请求文件：" + exception.Message);
                yield break;
            }

            Progress(
                onProgress,
                BuildExecutionProgress(inputCount, requiredTaskType, normalizedAlgorithm));

            ProcessStartInfo startInfo =
                new ProcessStartInfo
                {
                    FileName = enginePath,
                    Arguments =
                        "--request " + Quote(requestPath) +
                        " --response " + Quote(responsePath) +
                        " --config " + Quote(runConfigPath),
                    WorkingDirectory = engineDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

            // 显式传给算法子进程，修复 PyInstaller 的
            // "Could not create temporary directory" 启动错误。
            startInfo.EnvironmentVariables["TEMP"] =
                processTempDirectory;
            startInfo.EnvironmentVariables["TMP"] =
                processTempDirectory;
            startInfo.EnvironmentVariables["TMPDIR"] =
                processTempDirectory;

            Process process =
                new Process { StartInfo = startInfo };
            Task<string> standardOutputTask;
            Task<string> standardErrorTask;

            try
            {
                process.Start();
                _activeProcess = process;
                standardOutputTask =
                    process.StandardOutput.ReadToEndAsync();
                standardErrorTask =
                    process.StandardError.ReadToEndAsync();
            }
            catch (Exception exception)
            {
                process.Dispose();
                _activeProcess = null;
                InvokeError(
                    onError,
                    "算法EXE启动失败：" + exception.Message);
                yield break;
            }

            float deadline =
                Time.realtimeSinceStartup +
                Mathf.Max(10, timeoutSeconds);
            bool timedOut = false;

            while (!process.HasExited)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    timedOut = true;
                    TryKill(process);
                    break;
                }

                yield return null;
            }

            float streamReadDeadline =
                Time.realtimeSinceStartup + 5f;
            while ((!standardOutputTask.IsCompleted ||
                    !standardErrorTask.IsCompleted) &&
                   Time.realtimeSinceStartup < streamReadDeadline)
            {
                yield return null;
            }

            string standardOutput =
                GetTaskText(standardOutputTask);
            string standardError =
                GetTaskText(standardErrorTask);
            int exitCode = process.HasExited
                ? process.ExitCode
                : -1;

            process.Dispose();
            _activeProcess = null;

            if (timedOut)
            {
                InvokeError(
                    onError,
                    "算法计算超过" +
                    Mathf.Max(10, timeoutSeconds) +
                    "秒，已终止本次计算。");
                yield break;
            }

            if (!File.Exists(responsePath))
            {
                InvokeError(
                    onError,
                    "算法EXE没有生成响应文件。" +
                    BuildProcessDetail(standardOutput, standardError));
                yield break;
            }

            RmgExeRunResponse response;
            try
            {
                response =
                    JsonConvert.DeserializeObject<RmgExeRunResponse>(
                        File.ReadAllText(responsePath, Encoding.UTF8));
            }
            catch (Exception exception)
            {
                InvokeError(
                    onError,
                    "算法响应无法解析：" + exception.Message);
                yield break;
            }

            if (response == null ||
                exitCode != 0 ||
                !response.success)
            {
                string message =
                    response == null ||
                    string.IsNullOrWhiteSpace(response.message)
                        ? "算法EXE返回失败，退出码=" + exitCode + "。"
                        : response.message;
                InvokeError(
                    onError,
                    message +
                    BuildProcessDetail(standardOutput, standardError));
                yield break;
            }

            string resultJsonPath = ResolveReturnedPath(
                runtimeDirectory,
                response.output_json_file);
            if (string.IsNullOrWhiteSpace(resultJsonPath) ||
                !File.Exists(resultJsonPath))
            {
                InvokeError(
                    onError,
                    "算法成功响应中没有可读取的详细调度JSON：" +
                    (response.output_json_file ?? "<空>"));
                yield break;
            }

            Progress(
                onProgress,
                "算法计算完成，正在转换VR调度序列……");

            if (onSuccess != null)
                onSuccess(response, resultJsonPath);
        }

        public void StopActiveProcess()
        {
            if (_activeProcess == null)
                return;

            TryKill(_activeProcess);
            _activeProcess = null;
        }

        private static bool TryPrepareRuntime(
            out string engineDirectory,
            out string enginePath,
            out string configPath,
            out string error)
        {
            engineDirectory = null;
            enginePath = null;
            configPath = null;
            error = null;

            try
            {
                engineDirectory =
                    Path.Combine(
                        Application.streamingAssetsPath,
                        PayloadFolderName);

                enginePath =
                    Path.Combine(engineDirectory, EngineFileName);
                configPath =
                    Path.Combine(engineDirectory, ConfigFileName);

                EnsurePayloadFile(enginePath);
                EnsurePayloadFile(configPath);
                return true;
            }
            catch (Exception exception)
            {
                error =
                    "读取项目内算法资源失败：" +
                    exception.Message;
                return false;
            }
        }

        private static string GetProjectRuntimeDirectory()
        {
            // Editor：项目根目录/RmgAlgorithmRuntime
            // Player：打包程序根目录/RmgAlgorithmRuntime
            string packageRoot =
                Path.GetFullPath(
                    Path.Combine(Application.dataPath, ".."));
            return Path.Combine(
                packageRoot,
                "RmgAlgorithmRuntime");
        }

        private static void EnsurePayloadFile(
            string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "算法发布资源缺失：" +
                    Path.GetFileName(path),
                    path);
            }
        }

        private static string ResolveReturnedPath(
            string engineDirectory,
            string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            return Path.IsPathRooted(path)
                ? path
                : Path.GetFullPath(
                    Path.Combine(engineDirectory, path));
        }

        private static string MakeSafeFileName(
            string value,
            string fallback)
        {
            string result =
                string.IsNullOrWhiteSpace(value)
                    ? fallback
                    : value.Trim();

            char[] invalid = Path.GetInvalidFileNameChars();
            for (int index = 0; index < invalid.Length; index++)
                result = result.Replace(invalid[index], '_');
            return result;
        }

        private static string Quote(string value)
        {
            return "\"" +
                   value.Replace("\"", "\\\"") +
                   "\"";
        }

        private static string BuildExecutionProgress(int inputCount, string taskType, string algorithm)
        {
            // 使用本次实际写入的作业算例数，以及所选算法，避免固定数量或固定算法提示。
            return "已传入" + inputCount + "条“" + taskType +
                   "”算例，正在执行" + GetDisplayName(algorithm) + "……";
        }

        private static string GetDisplayName(string algorithm)
        {
            switch (algorithm)
            {
                case "ga": return "遗传算法";
                case "pso": return "粒子群优化算法";
                case "alns": return "自适应大邻域搜索算法";
                default: return "调度算法";
            }
        }

        private static string GetTaskText(Task<string> task)
        {
            return task != null &&
                   task.Status == TaskStatus.RanToCompletion
                ? task.Result
                : string.Empty;
        }

        private static string BuildProcessDetail(
            string standardOutput,
            string standardError)
        {
            string detail =
                !string.IsNullOrWhiteSpace(standardError)
                    ? standardError.Trim()
                    : standardOutput == null
                        ? string.Empty
                        : standardOutput.Trim();
            if (string.IsNullOrWhiteSpace(detail))
                return string.Empty;
            if (detail.Length > 600)
                detail = detail.Substring(detail.Length - 600);
            return "\n" + detail;
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (process != null && !process.HasExited)
                    process.Kill();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[RmgExe] 终止算法进程失败：" +
                    exception.Message);
            }
        }

        private static void Progress(
            Action<string> callback,
            string message)
        {
            Debug.Log("[RmgExe] " + message);
            if (callback != null)
                callback(message);
        }

        private static void InvokeError(
            Action<string> callback,
            string message)
        {
            Debug.LogError("[RmgExe] " + message);
            if (callback != null)
                callback(message);
        }
    }
}
