using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Vtp.VR.Integration;
using Vtp.VR.Rest;

namespace Vtp.VR.LocalAlgorithmExe
{
    /// <summary>
    /// 三维场景算法按钮与独立算法EXE之间的统一协调器。
    /// 运行时自动创建，不要求在Hierarchy中手工挂载。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RmgExeCoordinator : MonoBehaviour
    {
        public static RmgExeCoordinator Instance
        {
            get;
            private set;
        }

        [SerializeField]
        [Min(10)]
        private int algorithmTimeoutSeconds = 900;

        public bool IsRunning
        {
            get;
            private set;
        }

        private RmgExeProcessClient _client;

        public static RmgExeCoordinator EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            RmgExeCoordinator existing =
                FindObjectOfType<RmgExeCoordinator>();
            if (existing != null)
                return existing;

            GameObject host =
                new GameObject("RmgExeAlgorithmCoordinator");
            return host.AddComponent<RmgExeCoordinator>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            _client = new RmgExeProcessClient();
        }

        public void Run(
            string taskId,
            string algorithm,
            Action<RmgExeExecutionResult> onSuccess,
            Action<string> onProgress,
            Action<string> onError)
        {
            JObject defaults;
            try
            {
                defaults =
                    RmgExeParameterFactory.CreateDefaults(algorithm);
            }
            catch (Exception exception)
            {
                InvokeError(onError, exception.Message);
                return;
            }

            Run(
                taskId,
                algorithm,
                defaults,
                onSuccess,
                onProgress,
                onError);
        }

        /// <summary>
        /// 使用VR参数页面已经确认的参数运行算法EXE。
        /// </summary>
        public void Run(
            string taskId,
            string algorithm,
            JObject parameters,
            Action<RmgExeExecutionResult> onSuccess,
            Action<string> onProgress,
            Action<string> onError)
        {
            if (IsRunning)
            {
                InvokeError(onError, "已有算法EXE正在计算。");
                return;
            }

            SceneNumericalExampleDto[] cachedExamples =
                SceneNumericalExampleRuntime.Examples;
            if (cachedExamples == null || cachedExamples.Length == 0)
            {
                InvokeError(
                    onError,
                    "当前没有已查看并缓存的算例，不能启动算法EXE。");
                return;
            }

            string requiredTaskType =
                ResolveTaskType(
                    SceneNumericalExampleRuntime.TaskType,
                    cachedExamples);
            if (string.IsNullOrWhiteSpace(requiredTaskType))
            {
                InvokeError(
                    onError,
                    "当前算例没有明确的实验作业类型，不能启动算法EXE。");
                return;
            }

            SceneNumericalExampleDto[] filteredExamples =
                FilterByTaskType(
                    cachedExamples,
                    requiredTaskType);
            if (filteredExamples.Length == 0)
            {
                InvokeError(
                    onError,
                    "当前缓存中没有“" +
                    requiredTaskType +
                    "”算例。");
                return;
            }

            SceneNumericalExampleDto[] originalExamples =
                SceneNumericalExampleRuntime.OriginalExamples;

            bool inputWasLocalFallback =
                SceneNumericalExampleRuntime.IsLocalFallback;
            string originalSourceMessage =
                SceneNumericalExampleRuntime.SourceMessage;

            IsRunning = true;
            StartCoroutine(
                RunCoroutine(
                    taskId,
                    algorithm,
                    parameters,
                    filteredExamples,
                    originalExamples,
                    requiredTaskType,
                    inputWasLocalFallback,
                    originalSourceMessage,
                    onSuccess,
                    onProgress,
                    onError));
        }

        private IEnumerator RunCoroutine(
            string taskId,
            string algorithm,
            JObject parameters,
            SceneNumericalExampleDto[] inputExamples,
            SceneNumericalExampleDto[] originalExamples,
            string requiredTaskType,
            bool inputWasLocalFallback,
            string originalSourceMessage,
            Action<RmgExeExecutionResult> onSuccess,
            Action<string> onProgress,
            Action<string> onError)
        {
            RmgExeRunResponse response = null;
            string resultJsonPath = null;
            string processError = null;

            yield return _client.Run(
                taskId,
                algorithm,
                parameters,
                inputExamples,
                originalExamples,
                requiredTaskType,
                algorithmTimeoutSeconds,
                onProgress,
                delegate(
                    RmgExeRunResponse completedResponse,
                    string completedResultJsonPath)
                {
                    response = completedResponse;
                    resultJsonPath = completedResultJsonPath;
                },
                delegate(string error)
                {
                    processError = error;
                });

            if (!string.IsNullOrWhiteSpace(processError))
            {
                IsRunning = false;
                InvokeError(onError, processError);
                yield break;
            }

            if (onProgress != null) onProgress("正在校验调度结果并准备自动分配岸桥……");
            TaskCaseDocumentDto input;
            SchedulingResultDto schedule;
            SceneNumericalExampleDto[] convertedExamples;
            string conversionError;
            if (!RmgExeResultAdapter.TryConvert(
                    resultJsonPath,
                    taskId,
                    algorithm,
                    out input,
                    out schedule,
                    out convertedExamples,
                    out conversionError))
            {
                IsRunning = false;
                InvokeError(onError, conversionError);
                yield break;
            }

            string filterError;
            if (!TryKeepOnlyTaskType(
                    requiredTaskType,
                    ref input,
                    ref schedule,
                    out filterError))
            {
                IsRunning = false;
                InvokeError(onError, filterError);
                yield break;
            }

            int operationCount =
                schedule == null || schedule.operations == null
                    ? 0
                    : schedule.operations.Length;
            if (operationCount != inputExamples.Length)
            {
                IsRunning = false;
                InvokeError(
                    onError,
                    "算法EXE返回的调度任务数与输入不一致：输入" +
                    inputExamples.Length +
                    "条，输出" +
                    operationCount +
                    "条。已阻止启动动画，避免执行错误算例。");
                yield break;
            }

            string sourceMessage = BuildSourceMessage(
                inputWasLocalFallback,
                originalSourceMessage,
                requiredTaskType,
                inputExamples.Length);

            // 不再用EXE解析出的展示行覆盖查看页面缓存。
            // 查看、算法输入和后续再次查看始终保持同一批原始算例。
            SceneExampleScheduleRuntime.Set(input, schedule);

            SceneScheduleDebugExporter.LogAndSave(
                input,
                schedule);

            IsRunning = false;

            Debug.Log(
                "[RmgExe] 调度结果已接入VR：algorithm=" +
                algorithm +
                "，operations=" +
                schedule.operations.Length +
                "，source=" +
                (inputWasLocalFallback
                    ? "zip-local-test"
                    : "server") +
                "，taskType=" +
                requiredTaskType);

            if (onSuccess != null)
            {
                onSuccess(
                    new RmgExeExecutionResult
                    {
                        Response = response,
                        Input = input,
                        Schedule = schedule,
                        Examples = inputExamples,
                        InputWasLocalFallback =
                            inputWasLocalFallback,
                        SourceMessage = sourceMessage,
                        Parameters = parameters == null
                            ? null
                            : (JObject)parameters.DeepClone()
                    });
            }
        }

        private static string BuildSourceMessage(
            bool inputWasLocalFallback,
            string originalSourceMessage,
            string taskType,
            int count)
        {
            if (inputWasLocalFallback)
            {
                return string.IsNullOrWhiteSpace(originalSourceMessage)
                    ? "算法使用ZIP本地测试“" +
                      taskType +
                      "”算例，共" +
                      count +
                      "条。"
                    : originalSourceMessage;
            }

            return
                "算法使用服务器“" +
                taskType +
                "”算例，共" +
                count +
                "条。";
        }

        private static string ResolveTaskType(
            string cachedTaskType,
            SceneNumericalExampleDto[] examples)
        {
            string normalized;
            if (SceneExampleTaskCaseAdapter.TryNormalizeTaskType(
                    cachedTaskType,
                    out normalized))
            {
                return normalized;
            }

            if (examples != null)
            {
                for (int index = 0;
                     index < examples.Length;
                     index++)
                {
                    SceneNumericalExampleDto example = examples[index];
                    if (example != null &&
                        SceneExampleTaskCaseAdapter.TryNormalizeTaskType(
                            example.type,
                            out normalized))
                    {
                        return normalized;
                    }
                }
            }

            return null;
        }

        private static SceneNumericalExampleDto[] FilterByTaskType(
            SceneNumericalExampleDto[] examples,
            string requiredTaskType)
        {
            if (examples == null || examples.Length == 0)
                return new SceneNumericalExampleDto[0];

            System.Collections.Generic.List<SceneNumericalExampleDto>
                result =
                    new System.Collections.Generic.List<
                        SceneNumericalExampleDto>();
            for (int index = 0; index < examples.Length; index++)
            {
                SceneNumericalExampleDto example = examples[index];
                string taskType;
                if (example != null &&
                    SceneExampleTaskCaseAdapter.TryNormalizeTaskType(
                        example.type,
                        out taskType) &&
                    string.Equals(
                        taskType,
                        requiredTaskType,
                        StringComparison.Ordinal))
                {
                    result.Add(example);
                }
            }

            return result.ToArray();
        }

        private static bool TryKeepOnlyTaskType(
            string requiredTaskType,
            ref TaskCaseDocumentDto input,
            ref SchedulingResultDto schedule,
            out string error)
        {
            error = null;
            if (input == null || input.jobs == null ||
                schedule == null || schedule.operations == null)
            {
                error = "算法EXE返回的任务或调度序列为空。";
                return false;
            }

            System.Collections.Generic.HashSet<string> keptJobIds =
                new System.Collections.Generic.HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
            System.Collections.Generic.List<SchedulingJobInputDto>
                keptJobs =
                    new System.Collections.Generic.List<
                        SchedulingJobInputDto>();

            for (int index = 0; index < input.jobs.Length; index++)
            {
                SchedulingJobInputDto job = input.jobs[index];
                string normalized;
                if (job != null &&
                    SceneExampleTaskCaseAdapter.TryNormalizeTaskType(
                        job.taskType,
                        out normalized) &&
                    string.Equals(
                        normalized,
                        requiredTaskType,
                        StringComparison.Ordinal))
                {
                    keptJobs.Add(job);
                    if (!string.IsNullOrWhiteSpace(job.jobId))
                        keptJobIds.Add(job.jobId);
                }
            }

            System.Collections.Generic.List<ScheduledOperationDto>
                keptOperations =
                    new System.Collections.Generic.List<
                        ScheduledOperationDto>();
            float makespan = 0f;
            for (int index = 0;
                 index < schedule.operations.Length;
                 index++)
            {
                ScheduledOperationDto operation =
                    schedule.operations[index];
                if (operation == null ||
                    !keptJobIds.Contains(operation.jobId))
                {
                    continue;
                }

                operation.sequence = keptOperations.Count + 1;
                keptOperations.Add(operation);
                makespan = Mathf.Max(
                    makespan,
                    operation.plannedEndSeconds);
            }

            if (keptJobs.Count == 0 || keptOperations.Count == 0)
            {
                error =
                    "算法EXE返回结果中没有“" +
                    requiredTaskType +
                    "”调度任务。";
                return false;
            }

            input.jobs = keptJobs.ToArray();
            schedule.operations = keptOperations.ToArray();
            if (schedule.metrics == null)
                schedule.metrics = new SchedulingMetricsDto();
            schedule.metrics.jobCount = keptOperations.Count;
            schedule.metrics.makespanSeconds = makespan;
            return true;
        }

        private static string Shorten(
            string value,
            int maximumLength)
        {
            string result = (value ?? string.Empty).Trim();
            return result.Length <= maximumLength
                ? result
                : result.Substring(0, maximumLength) + "…";
        }

        private static void InvokeError(
            Action<string> callback,
            string message)
        {
            Debug.LogError("[RmgExe] " + message);
            if (callback != null)
                callback(message);
        }

        private void OnApplicationQuit()
        {
            if (_client != null)
                _client.StopActiveProcess();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                if (_client != null)
                    _client.StopActiveProcess();
                Instance = null;
            }
        }
    }
}
