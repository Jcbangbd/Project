using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace Vtp.VR.LocalAlgorithm
{
    [DisallowMultipleComponent]
    public sealed class FastApiProcessManager : MonoBehaviour
    {
        private sealed class PythonCommand
        {
            public string FileName;
            public string PrefixArguments;
            public string DisplayName;
        }

        public static FastApiProcessManager Instance { get; private set; }

        [SerializeField] private bool startAutomatically = true;

        public bool IsReady { get; private set; }
        public bool IsPreparingEnvironment { get; private set; }
        public string LastError { get; private set; }
        public string BaseUrl { get; private set; }
        public string ApiKey { get; private set; }
        public string PythonExecutable { get; private set; }

        public int RequestTimeoutSeconds
        {
            get
            {
                return _config == null
                    ? 30
                    : Mathf.Max(1, _config.requestTimeoutSeconds);
            }
        }

        public event Action Ready;
        public event Action<string> StartupFailed;

        private readonly ConcurrentQueue<string> _processLogs =
            new ConcurrentQueue<string>();

        private LocalFastApiConfig _config;
        private Process _process;
        private Process _toolProcess;
        private bool _ownsProcess;
        private bool _isStarting;
        private string _instanceId;
        private string _environmentError;
        private PythonCommand _bootstrapPython;
        private int _lastToolExitCode;
        private bool _lastToolTimedOut;
        private string _lastToolOutput;
        private string _pythonTempDirectory;
        private string _pipCacheDirectory;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private IEnumerator Start()
        {
            if (startAutomatically)
                yield return StartServer();
        }

        public IEnumerator StartServer()
        {
            if (IsReady || _isStarting)
                yield break;

            _isStarting = true;
            LastError = null;

            string serverRoot;
            try
            {
                LoadConfiguration(out serverRoot);
            }
            catch (Exception exception)
            {
                FailStartup("读取本地服务配置失败：" + exception.Message);
                yield break;
            }

            yield return PreparePythonEnvironment(serverRoot);
            if (!string.IsNullOrEmpty(_environmentError))
            {
                FailStartup(_environmentError);
                yield break;
            }

            try
            {
                StartPythonProcess(serverRoot, PythonExecutable);
            }
            catch (Exception exception)
            {
                FailStartup("启动本地 FastAPI 失败：" + exception.Message);
                yield break;
            }

            float deadline =
                Time.realtimeSinceStartup +
                Mathf.Max(1, _config.startupTimeoutSeconds);

            while (Time.realtimeSinceStartup < deadline)
            {
                if (HasOwnedProcessExited())
                {
                    FailStartup(
                        "FastAPI 进程提前退出，请查看 [FastAPI] 错误日志。");
                    yield break;
                }

                using (UnityWebRequest request =
                       UnityWebRequest.Get(BaseUrl + "/health"))
                {
                    request.timeout = 2;
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        LocalFastApiHealthDto health = null;
                        try
                        {
                            health = JsonUtility.FromJson<LocalFastApiHealthDto>(
                                request.downloadHandler.text);
                        }
                        catch (Exception exception)
                        {
                            Debug.LogWarning(
                                "[FastAPI] 健康检查返回无法解析：" +
                                exception.Message);
                        }

                        if (health != null &&
                            health.ready &&
                            health.service == "vr-local-algorithm")
                        {
                            if (health.instance_id != _instanceId)
                            {
                                FailStartup(
                                    "端口 " + _config.port +
                                    " 已被另一个本地算法服务占用。");
                                yield break;
                            }

                            IsReady = true;
                            _isStarting = false;
                            Debug.Log(
                                "[FastAPI] 本地算法服务已就绪：" + BaseUrl);

                            Action handler = Ready;
                            if (handler != null)
                                handler();
                            yield break;
                        }
                    }
                }

                yield return new WaitForSecondsRealtime(0.5f);
            }

            FailStartup(
                "FastAPI 启动超时。请检查端口 " +
                _config.port + "。");
        }

        private void LoadConfiguration(out string serverRoot)
        {
            string configPath = Path.Combine(
                Application.streamingAssetsPath,
                "local-fastapi-config.json");

            if (!File.Exists(configPath))
                throw new FileNotFoundException(
                    "找不到配置文件。",
                    configPath);

            string json = File.ReadAllText(configPath);
            _config = JsonUtility.FromJson<LocalFastApiConfig>(json);
            if (_config == null)
                throw new InvalidDataException("FastAPI 配置 JSON 无效。");

            if (_config.host != "127.0.0.1" &&
                !string.Equals(
                    _config.host,
                    "localhost",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "本地算法服务只能绑定 127.0.0.1/localhost。");
            }

            if (_config.port < 1 || _config.port > 65535)
                throw new InvalidDataException("FastAPI 端口无效。");

            serverRoot = Path.GetFullPath(
                Path.Combine(
                    Application.streamingAssetsPath,
                    _config.serverFolder));

            if (!Directory.Exists(serverRoot))
                throw new DirectoryNotFoundException(
                    "找不到 FastAPI 服务目录：" + serverRoot);

            BaseUrl =
                "http://" + _config.host + ":" +
                _config.port.ToString(CultureInfo.InvariantCulture);
            ApiKey = Guid.NewGuid().ToString("N");
            _instanceId = Guid.NewGuid().ToString("N");
        }

        private IEnumerator PreparePythonEnvironment(string serverRoot)
        {
            IsPreparingEnvironment = true;
            _environmentError = null;
            PythonExecutable = null;

            string requirementsPath =
                Path.Combine(serverRoot, "requirements.txt");
            if (!File.Exists(requirementsPath))
            {
                SetEnvironmentError(
                    "找不到 Python 依赖文件：" + requirementsPath);
                yield break;
            }

            string tempDirectoryError;
            if (!PrepareWritableTempDirectories(
                    out tempDirectoryError))
            {
                SetEnvironmentError(
                    "无法准备 Python 临时目录：" +
                    tempDirectoryError);
                yield break;
            }

            string environmentRoot;
            try
            {
                environmentRoot = ResolveEnvironmentRoot();
                Directory.CreateDirectory(
                    Path.GetDirectoryName(environmentRoot));
            }
            catch (Exception exception)
            {
                SetEnvironmentError(
                    "无法创建 Python 环境目录：" + exception.Message);
                yield break;
            }

            string environmentPython =
                GetEnvironmentPythonPath(environmentRoot);

            bool interpreterExists = File.Exists(environmentPython);
            bool environmentHealthy = false;

            if (interpreterExists)
            {
                Debug.Log(
                    "[PythonSetup] 检查现有虚拟环境解释器：" +
                    environmentPython);

                yield return RunToolProcess(
                    environmentPython,
                    "-S -c " + QuoteArgument(
                        "import sys; sys.exit(" +
                        "0 if sys.version_info >= (3, 10) else 2)"),
                    serverRoot,
                    15,
                    false);

                environmentHealthy =
                    !_lastToolTimedOut &&
                    _lastToolExitCode == 0;

                if (!environmentHealthy)
                {
                    Debug.LogWarning(
                        "[PythonSetup] 现有虚拟环境已损坏或无响应，" +
                        "将自动清理并重建。");
                }
            }

            bool mustCreateEnvironment =
                !interpreterExists || !environmentHealthy;

            if (mustCreateEnvironment)
            {
                if (!_config.autoCreatePythonEnvironment)
                {
                    SetEnvironmentError(
                        "本地 Python 虚拟环境不存在或已损坏，" +
                        "且自动创建已关闭：" +
                        environmentPython);
                    yield break;
                }

                Debug.Log(
                    interpreterExists
                        ? "[PythonSetup] 开始查找基础 Python 以重建环境。"
                        : "[PythonSetup] 未发现本地虚拟环境，" +
                          "开始检测基础 Python。");

                yield return FindBootstrapPython(serverRoot);
                if (_bootstrapPython == null)
                {
                    SetEnvironmentError(
                        "未找到 Python 3.10 或更高版本。" +
                        "请安装 Python，或把完整 Python 运行时放入 " +
                        "LocalAlgorithmServer/runtime。");
                    yield break;
                }

                Debug.Log(
                    "[PythonSetup] 使用 " +
                    _bootstrapPython.DisplayName +
                    (interpreterExists
                        ? " 重建虚拟环境："
                        : " 创建虚拟环境：") +
                    environmentRoot);

                string createArguments = CombineArguments(
                    _bootstrapPython.PrefixArguments,
                    "-m venv " +
                    (interpreterExists ? "--clear " : string.Empty) +
                    QuoteArgument(environmentRoot));

                yield return RunToolProcess(
                    _bootstrapPython.FileName,
                    createArguments,
                    serverRoot,
                    Mathf.Max(
                        30,
                        _config.environmentSetupTimeoutSeconds),
                    true);

                if (_lastToolTimedOut || _lastToolExitCode != 0)
                {
                    SetEnvironmentError(
                        "创建 Python 虚拟环境失败：" +
                        FormatToolFailure());
                    yield break;
                }

                if (!File.Exists(environmentPython))
                {
                    SetEnvironmentError(
                        "虚拟环境创建完成，但找不到解释器：" +
                        environmentPython);
                    yield break;
                }

                yield return RunToolProcess(
                    environmentPython,
                    "-S -c " + QuoteArgument(
                        "import sys; sys.exit(" +
                        "0 if sys.version_info >= (3, 10) else 2)"),
                    serverRoot,
                    30,
                    false);

                if (_lastToolTimedOut || _lastToolExitCode != 0)
                {
                    SetEnvironmentError(
                        "重建后的 Python 虚拟环境仍无法运行：" +
                        FormatToolFailure());
                    yield break;
                }
            }
            else
            {
                Debug.Log(
                    "[PythonSetup] 已存在且解释器健康，" +
                    "不重复创建虚拟环境：" +
                    environmentRoot);
            }

            string requirementsHash;
            try
            {
                requirementsHash = ComputeSha256(requirementsPath);
            }
            catch (Exception exception)
            {
                SetEnvironmentError(
                    "无法读取 requirements.txt：" + exception.Message);
                yield break;
            }

            string markerPath = Path.Combine(
                environmentRoot,
                ".requirements.sha256");
            bool markerExists = File.Exists(markerPath);
            bool markerMatches = false;
            if (markerExists)
            {
                try
                {
                    markerMatches = string.Equals(
                        File.ReadAllText(markerPath).Trim(),
                        requirementsHash,
                        StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    markerMatches = false;
                }
            }

            yield return RunToolProcess(
                environmentPython,
                "-c " + QuoteArgument(
                    "import sys, fastapi, uvicorn, multipart, cryptography; " +
                    "sys.exit(0 if sys.version_info >= (3, 10) else 2)"),
                serverRoot,
                30,
                false);

            bool dependenciesReady =
                !_lastToolTimedOut && _lastToolExitCode == 0;
            bool requirementsChanged =
                markerExists && !markerMatches;
            bool installRequired =
                !dependenciesReady ||
                (requirementsChanged &&
                 _config.reinstallWhenRequirementsChange);

            if (!installRequired)
            {
                Debug.Log(
                    "[PythonSetup] FastAPI 依赖已存在，不重复安装。");
                TryWriteRequirementsMarker(
                    markerPath,
                    requirementsHash);
                PythonExecutable = environmentPython;
                IsPreparingEnvironment = false;
                yield break;
            }

            if (!_config.autoInstallDependencies)
            {
                SetEnvironmentError(
                    "Python 环境存在，但 FastAPI 依赖缺失或已变更，" +
                    "且自动安装依赖已关闭。");
                yield break;
            }

            Debug.Log(
                dependenciesReady
                    ? "[PythonSetup] requirements.txt 已更新，开始同步依赖。"
                    : "[PythonSetup] FastAPI 依赖不存在，开始自动安装。");

            yield return RunToolProcess(
                environmentPython,
                "-m pip --version",
                serverRoot,
                30,
                false);

            if (_lastToolTimedOut || _lastToolExitCode != 0)
            {
                Debug.Log(
                    "[PythonSetup] 虚拟环境中没有 pip，正在执行 ensurepip。");
                yield return RunToolProcess(
                    environmentPython,
                    "-m ensurepip --upgrade",
                    serverRoot,
                    120,
                    true);

                if (_lastToolTimedOut || _lastToolExitCode != 0)
                {
                    SetEnvironmentError(
                        "初始化 pip 失败：" + FormatToolFailure());
                    yield break;
                }
            }

            string installArguments =
                "-m pip install --disable-pip-version-check " +
                "--no-input -r " + QuoteArgument(requirementsPath);

            yield return RunToolProcess(
                environmentPython,
                installArguments,
                serverRoot,
                Mathf.Max(
                    60,
                    _config.environmentSetupTimeoutSeconds),
                true);

            if (_lastToolTimedOut || _lastToolExitCode != 0)
            {
                SetEnvironmentError(
                    "安装 FastAPI 依赖失败，请检查网络：" +
                    FormatToolFailure());
                yield break;
            }

            yield return RunToolProcess(
                environmentPython,
                "-c " + QuoteArgument(
                    "import sys, fastapi, uvicorn, multipart, cryptography; " +
                    "sys.exit(0 if sys.version_info >= (3, 10) else 2)"),
                serverRoot,
                30,
                false);

            if (_lastToolTimedOut || _lastToolExitCode != 0)
            {
                SetEnvironmentError(
                    "依赖安装结束，但导入验证失败：" +
                    FormatToolFailure());
                yield break;
            }

            TryWriteRequirementsMarker(
                markerPath,
                requirementsHash);

            PythonExecutable = environmentPython;
            IsPreparingEnvironment = false;
            Debug.Log(
                "[PythonSetup] Python 环境准备完成：" +
                PythonExecutable);
        }

        private string ResolveEnvironmentRoot()
        {
            string localApplicationData =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localApplicationData))
            {
                localApplicationData =
                    Application.persistentDataPath;
            }

            string runtimeRoot = Path.GetFullPath(
                Path.Combine(
                    localApplicationData,
                    "VtpVRLocalAlgorithm"));
            string folderName =
                string.IsNullOrWhiteSpace(
                    _config.pythonEnvironmentFolder)
                    ? "LocalAlgorithmPythonEnvironment"
                    : _config.pythonEnvironmentFolder;

            if (Path.IsPathRooted(folderName))
                throw new InvalidDataException(
                    "pythonEnvironmentFolder 必须是相对文件夹名。");

            string result = Path.GetFullPath(
                Path.Combine(runtimeRoot, folderName));
            string safePrefix =
                runtimeRoot.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;

            if (!result.StartsWith(
                    safePrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Python 环境目录超出本地算法运行目录。");
            }

            return result;
        }

        private static string GetEnvironmentPythonPath(
            string environmentRoot)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            return Path.Combine(
                environmentRoot,
                "Scripts",
                "python.exe");
#else
            return Path.Combine(
                environmentRoot,
                "bin",
                "python");
#endif
        }

        private IEnumerator FindBootstrapPython(string serverRoot)
        {
            _bootstrapPython = null;
            List<PythonCommand> candidates =
                new List<PythonCommand>();

            if (!string.IsNullOrWhiteSpace(
                    _config.bootstrapPythonExecutable))
            {
                string configured =
                    _config.bootstrapPythonExecutable.Trim();
                if (Path.IsPathRooted(configured))
                {
                    if (File.Exists(configured))
                    {
                        candidates.Add(
                            new PythonCommand
                            {
                                FileName = configured,
                                PrefixArguments = string.Empty,
                                DisplayName = configured
                            });
                    }
                }
                else if (configured.IndexOf(
                             Path.DirectorySeparatorChar) >= 0 ||
                         configured.IndexOf(
                             Path.AltDirectorySeparatorChar) >= 0)
                {
                    string relative = Path.GetFullPath(
                        Path.Combine(serverRoot, configured));
                    if (File.Exists(relative))
                    {
                        candidates.Add(
                            new PythonCommand
                            {
                                FileName = relative,
                                PrefixArguments = string.Empty,
                                DisplayName = relative
                            });
                    }
                }
                else
                {
                    candidates.Add(
                        new PythonCommand
                        {
                            FileName = configured,
                            PrefixArguments = string.Empty,
                            DisplayName = configured
                        });
                }
            }

            if (!string.IsNullOrWhiteSpace(
                    _config.bundledPythonRelativePath))
            {
                string bundledPath = Path.GetFullPath(
                    Path.Combine(
                        serverRoot,
                        _config.bundledPythonRelativePath));
                if (File.Exists(bundledPath))
                {
                    candidates.Add(
                        new PythonCommand
                        {
                            FileName = bundledPath,
                            PrefixArguments = string.Empty,
                            DisplayName = "项目随附 Python"
                        });
                }
            }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            candidates.Add(
                new PythonCommand
                {
                    FileName = "py",
                    PrefixArguments = "-3",
                    DisplayName = "Windows Python Launcher (py -3)"
                });
            candidates.Add(
                new PythonCommand
                {
                    FileName = "python",
                    PrefixArguments = string.Empty,
                    DisplayName = "系统 Python"
                });
#else
            candidates.Add(
                new PythonCommand
                {
                    FileName = "python3",
                    PrefixArguments = string.Empty,
                    DisplayName = "系统 Python 3"
                });
            candidates.Add(
                new PythonCommand
                {
                    FileName = "python",
                    PrefixArguments = string.Empty,
                    DisplayName = "系统 Python"
                });
#endif

            for (int index = 0; index < candidates.Count; index++)
            {
                PythonCommand candidate = candidates[index];
                string checkArguments = CombineArguments(
                    candidate.PrefixArguments,
                    "-c " + QuoteArgument(
                        "import sys; sys.exit(" +
                        "0 if sys.version_info >= (3, 10) else 2)"));

                yield return RunToolProcess(
                    candidate.FileName,
                    checkArguments,
                    serverRoot,
                    10,
                    false);

                if (!_lastToolTimedOut && _lastToolExitCode == 0)
                {
                    _bootstrapPython = candidate;
                    yield break;
                }
            }
        }

        private IEnumerator RunToolProcess(
            string fileName,
            string arguments,
            string workingDirectory,
            int timeoutSeconds,
            bool echoOutput)
        {
            _lastToolExitCode = -1;
            _lastToolTimedOut = false;
            _lastToolOutput = string.Empty;

            Process process = null;
            StringBuilder output = new StringBuilder();
            object outputLock = new object();

            DataReceivedEventHandler outputHandler =
                delegate(object sender, DataReceivedEventArgs args)
                {
                    if (string.IsNullOrEmpty(args.Data))
                        return;
                    lock (outputLock)
                    {
                        output.AppendLine(args.Data);
                    }
                    if (echoOutput)
                        _processLogs.Enqueue(
                            "[PythonSetup] " + args.Data);
                };

            DataReceivedEventHandler errorHandler =
                delegate(object sender, DataReceivedEventArgs args)
                {
                    if (string.IsNullOrEmpty(args.Data))
                        return;
                    lock (outputLock)
                    {
                        output.AppendLine(args.Data);
                    }
                    if (echoOutput)
                        _processLogs.Enqueue(
                            "[PythonSetup][Error] " + args.Data);
                };

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                ConfigurePythonProcessEnvironment(startInfo);
                startInfo.StandardOutputEncoding = Encoding.UTF8;
                startInfo.StandardErrorEncoding = Encoding.UTF8;

                process = new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true
                };
                process.OutputDataReceived += outputHandler;
                process.ErrorDataReceived += errorHandler;

                if (!process.Start())
                    throw new InvalidOperationException(
                        "Process.Start 返回 false。");

                _toolProcess = process;
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception exception)
            {
                _lastToolOutput = exception.Message;
                if (process != null)
                {
                    process.Dispose();
                    process = null;
                }
            }

            if (process == null)
                yield break;

            float deadline =
                Time.realtimeSinceStartup +
                Mathf.Max(1, timeoutSeconds);
            bool exited = false;

            while (!exited)
            {
                try
                {
                    exited = process.HasExited;
                }
                catch
                {
                    exited = true;
                }

                if (!exited &&
                    Time.realtimeSinceStartup >= deadline)
                {
                    _lastToolTimedOut = true;
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // Process may already have exited.
                    }
                    break;
                }

                if (!exited)
                    yield return null;
            }

            try
            {
                process.WaitForExit(2000);
                if (!_lastToolTimedOut)
                    _lastToolExitCode = process.ExitCode;
            }
            catch (Exception exception)
            {
                lock (outputLock)
                {
                    output.AppendLine(exception.Message);
                }
            }

            lock (outputLock)
            {
                _lastToolOutput = output.ToString().Trim();
            }

            process.OutputDataReceived -= outputHandler;
            process.ErrorDataReceived -= errorHandler;
            process.Dispose();
            if (_toolProcess == process)
                _toolProcess = null;
        }

        private void SetEnvironmentError(string message)
        {
            IsPreparingEnvironment = false;
            _environmentError = message;
        }

        private string FormatToolFailure()
        {
            if (_lastToolTimedOut)
                return "操作超时。";

            string detail = _lastToolOutput;
            if (detail != null && detail.Length > 2000)
                detail = detail.Substring(detail.Length - 2000);

            return "退出码 " + _lastToolExitCode +
                   (string.IsNullOrWhiteSpace(detail)
                       ? string.Empty
                       : "；" + detail);
        }

        private static string CombineArguments(
            string prefix,
            string arguments)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                return arguments;
            if (string.IsNullOrWhiteSpace(arguments))
                return prefix;
            return prefix + " " + arguments;
        }

        private static string QuoteArgument(string value)
        {
            return "\"" +
                   (value ?? string.Empty).Replace("\"", "\\\"") +
                   "\"";
        }

        private static string ComputeSha256(string filePath)
        {
            using (FileStream stream = File.OpenRead(filePath))
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(stream);
                StringBuilder builder =
                    new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                    builder.Append(hash[index].ToString("x2"));
                return builder.ToString();
            }
        }

        private bool PrepareWritableTempDirectories(
            out string error)
        {
            error = null;
            string probePath = null;

            try
            {
                string localApplicationData =
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrWhiteSpace(localApplicationData))
                {
                    localApplicationData =
                        Application.persistentDataPath;
                }

                string runtimeRoot = Path.Combine(
                    localApplicationData,
                    "VtpVRLocalAlgorithm");

                _pythonTempDirectory = Path.Combine(
                    runtimeRoot,
                    "Temp");
                _pipCacheDirectory = Path.Combine(
                    runtimeRoot,
                    "PipCache");

                Directory.CreateDirectory(_pythonTempDirectory);
                Directory.CreateDirectory(_pipCacheDirectory);

                probePath = Path.Combine(
                    _pythonTempDirectory,
                    ".write_test_" +
                    Guid.NewGuid().ToString("N") +
                    ".tmp");

                File.WriteAllText(
                    probePath,
                    "ok",
                    Encoding.UTF8);
                File.Delete(probePath);
                probePath = null;

                Debug.Log(
                    "[PythonSetup] 使用专用临时目录：" +
                    _pythonTempDirectory);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
            finally
            {
                if (!string.IsNullOrEmpty(probePath))
                {
                    try
                    {
                        if (File.Exists(probePath))
                            File.Delete(probePath);
                    }
                    catch
                    {
                        // Ignore cleanup failure for the write probe.
                    }
                }
            }
        }

        private void ConfigurePythonProcessEnvironment(
            ProcessStartInfo startInfo)
        {
            if (!string.IsNullOrWhiteSpace(_pythonTempDirectory))
            {
                startInfo.EnvironmentVariables["TEMP"] =
                    _pythonTempDirectory;
                startInfo.EnvironmentVariables["TMP"] =
                    _pythonTempDirectory;
                startInfo.EnvironmentVariables["TMPDIR"] =
                    _pythonTempDirectory;
            }

            if (!string.IsNullOrWhiteSpace(_pipCacheDirectory))
            {
                startInfo.EnvironmentVariables["PIP_CACHE_DIR"] =
                    _pipCacheDirectory;
            }

            startInfo.EnvironmentVariables["PYTHONUTF8"] = "1";
            startInfo.EnvironmentVariables["PYTHONIOENCODING"] =
                "utf-8";
            startInfo.EnvironmentVariables[
                "PIP_DISABLE_PIP_VERSION_CHECK"] = "1";
            startInfo.EnvironmentVariables["PIP_NO_INPUT"] = "1";
        }

        private static void TryWriteRequirementsMarker(
            string markerPath,
            string requirementsHash)
        {
            try
            {
                File.WriteAllText(
                    markerPath,
                    requirementsHash,
                    Encoding.UTF8);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[PythonSetup] 无法写入依赖标记：" +
                    exception.Message);
            }
        }

        private void StartPythonProcess(
            string serverRoot,
            string pythonExecutable)
        {
            if (string.IsNullOrWhiteSpace(pythonExecutable) ||
                !File.Exists(pythonExecutable))
            {
                throw new FileNotFoundException(
                    "找不到已准备好的 Python 解释器。",
                    pythonExecutable);
            }

            string dataDirectory = Path.Combine(
                Application.persistentDataPath,
                "LocalAlgorithmServerData");
            Directory.CreateDirectory(dataDirectory);

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = pythonExecutable,
                Arguments =
                    "run_server.py --host " +
                    _config.host +
                    " --port " +
                    _config.port.ToString(CultureInfo.InvariantCulture),
                WorkingDirectory = serverRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            ConfigurePythonProcessEnvironment(startInfo);
            startInfo.StandardOutputEncoding = Encoding.UTF8;
            startInfo.StandardErrorEncoding = Encoding.UTF8;
            startInfo.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
            startInfo.EnvironmentVariables["PYTHONNOUSERSITE"] = "1";
            startInfo.EnvironmentVariables["LOCAL_API_KEY"] = ApiKey;
            startInfo.EnvironmentVariables["LOCAL_SERVER_INSTANCE_ID"] =
                _instanceId;
            startInfo.EnvironmentVariables["LOCAL_ALGORITHM_DATA_DIR"] =
                dataDirectory;
            startInfo.EnvironmentVariables[
                "REQUIRE_ALGORITHM_SIGNATURE"] =
                _config.requireAlgorithmSignature
                    ? "true"
                    : "false";

            if (!string.IsNullOrWhiteSpace(
                    _config.algorithmPublicKeyRelativePath))
            {
                string publicKeyPath = Path.GetFullPath(
                    Path.Combine(
                        serverRoot,
                        _config.algorithmPublicKeyRelativePath));
                startInfo.EnvironmentVariables[
                    "ALGORITHM_PUBLIC_KEY_PATH"] =
                    publicKeyPath;

                if (_config.requireAlgorithmSignature &&
                    !File.Exists(publicKeyPath))
                {
                    throw new FileNotFoundException(
                        "已要求校验算法签名，但找不到公钥文件。",
                        publicKeyPath);
                }
            }

            _process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            _process.OutputDataReceived += OnProcessOutput;
            _process.ErrorDataReceived += OnProcessError;

            Debug.Log(
                "[FastAPI] 正在启动：" +
                pythonExecutable + " " + startInfo.Arguments);

            if (!_process.Start())
                throw new InvalidOperationException("无法启动 Python 进程。");

            _ownsProcess = true;
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        private void OnProcessOutput(
            object sender,
            DataReceivedEventArgs args)
        {
            if (!string.IsNullOrEmpty(args.Data))
                _processLogs.Enqueue("[FastAPI] " + args.Data);
        }

        private void OnProcessError(
            object sender,
            DataReceivedEventArgs args)
        {
            if (!string.IsNullOrEmpty(args.Data))
                _processLogs.Enqueue(
                    "[FastAPI][Error] " + args.Data);
        }

        private void Update()
        {
            string message;
            while (_processLogs.TryDequeue(out message))
                Debug.Log(message);

            if (IsReady && HasOwnedProcessExited())
            {
                IsReady = false;
                LastError = "本地算法服务进程已退出。";
                Debug.LogError("[FastAPI] " + LastError);
            }
        }

        private bool HasOwnedProcessExited()
        {
            if (!_ownsProcess || _process == null)
                return false;

            try
            {
                return _process.HasExited;
            }
            catch
            {
                return true;
            }
        }

        private void FailStartup(string message)
        {
            _isStarting = false;
            IsPreparingEnvironment = false;
            IsReady = false;
            LastError = message;
            Debug.LogError("[FastAPI] " + message);
            StopToolProcess();
            StopOwnedProcess();

            Action<string> handler = StartupFailed;
            if (handler != null)
                handler(message);
        }

        public void StopServer()
        {
            StopToolProcess();
            StopOwnedProcess();
            IsReady = false;
        }

        private void StopToolProcess()
        {
            if (_toolProcess == null)
                return;

            try
            {
                if (!_toolProcess.HasExited)
                    _toolProcess.Kill();
            }
            catch
            {
                // Ignore cleanup errors during shutdown.
            }
            finally
            {
                _toolProcess = null;
            }
        }

        private void StopOwnedProcess()
        {
            if (!_ownsProcess || _process == null)
                return;

            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill();
                    _process.WaitForExit(2000);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[FastAPI] 停止进程时发生异常：" +
                    exception.Message);
            }
            finally
            {
                _process.OutputDataReceived -= OnProcessOutput;
                _process.ErrorDataReceived -= OnProcessError;
                _process.Dispose();
                _process = null;
                _ownsProcess = false;
            }
        }

        private void OnApplicationQuit()
        {
            StopToolProcess();
            if (_config == null || _config.stopServerOnQuit)
                StopOwnedProcess();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                StopToolProcess();
                if (_config == null || _config.stopServerOnQuit)
                    StopOwnedProcess();
            }
        }
    }
}
