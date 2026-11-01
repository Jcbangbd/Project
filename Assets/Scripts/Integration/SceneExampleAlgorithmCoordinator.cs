using System;
using System.Collections;
using UnityEngine;
using Vtp.VR.LocalAlgorithm;
using Vtp.VR.Rest;

namespace Vtp.VR.Integration
{
    /// <summary>
    /// 登录后接收远程算例，等待本地 FastAPI 就绪，调用轨道吊算法并缓存结果。
    /// 挂在 DontDestroyOnLoad/LocalAlgorithmSystem 上。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneExampleAlgorithmCoordinator :
        MonoBehaviour
    {
        public static SceneExampleAlgorithmCoordinator Instance
        {
            get;
            private set;
        }

        [Header("引用")]
        [SerializeField]
        private LocalAlgorithmCoordinator localAlgorithmCoordinator;

        [Header("算例到场景箱位的映射")]
        [SerializeField]
        private string sceneId = "SceneStage1";

        [SerializeField]
        private string yardBlockName = "A40";

        [SerializeField]
        private string vesselName = "Vessel004";

        [SerializeField]
        [Range(1, 5)]
        private int defaultTier = 1;

        [Header("超时")]
        [SerializeField]
        private float serviceReadyTimeoutSeconds = 320f;

        [SerializeField]
        [Min(1)]
        private int algorithmRunTimeoutSeconds = 600;

        public bool IsRunning
        {
            get;
            private set;
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

            if (localAlgorithmCoordinator == null)
            {
                localAlgorithmCoordinator =
                    GetComponent<LocalAlgorithmCoordinator>();
            }
            if (localAlgorithmCoordinator == null)
            {
                localAlgorithmCoordinator =
                    FindObjectOfType<
                        LocalAlgorithmCoordinator>();
            }
        }

        public void Run(
            string taskId,
            SceneNumericalExampleDto[] examples,
            Action<SchedulingResultDto> onSuccess,
            Action<string> onProgress,
            Action<string> onError)
        {
            if (IsRunning)
            {
                InvokeError(
                    onError,
                    "已有轨道吊算例正在计算。");
                return;
            }

            TaskCaseDocumentDto input;
            string error;
            if (!SceneExampleTaskCaseAdapter.TryBuild(
                    taskId,
                    examples,
                    sceneId,
                    yardBlockName,
                    vesselName,
                    defaultTier,
                    algorithmRunTimeoutSeconds,
                    out input,
                    out error))
            {
                InvokeError(onError, error);
                return;
            }

            IsRunning = true;
            SceneExampleScheduleRuntime.Clear();
            StartCoroutine(
                RunCoroutine(
                    input,
                    onSuccess,
                    onProgress,
                    onError));
        }

        private IEnumerator RunCoroutine(
            TaskCaseDocumentDto input,
            Action<SchedulingResultDto> onSuccess,
            Action<string> onProgress,
            Action<string> onError)
        {
            Progress(
                onProgress,
                "正在等待本地轨道吊算法服务……");

            if (localAlgorithmCoordinator == null)
            {
                Fail(
                    onError,
                    "场景中没有 LocalAlgorithmCoordinator。");
                yield break;
            }

            float deadline =
                Time.realtimeSinceStartup +
                Mathf.Max(
                    5f,
                    serviceReadyTimeoutSeconds);

            while (!localAlgorithmCoordinator.IsReady &&
                   string.IsNullOrWhiteSpace(
                       localAlgorithmCoordinator.LastError) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (!localAlgorithmCoordinator.IsReady)
            {
                string detail =
                    string.IsNullOrWhiteSpace(
                        localAlgorithmCoordinator.LastError)
                        ? "等待本地 FastAPI 超时。"
                        : localAlgorithmCoordinator.LastError;
                Fail(onError, detail);
                yield break;
            }

            bool finished = false;
            string runError = null;
            SchedulingRunResponseDto runResponse = null;

            Progress(
                onProgress,
                "本地服务已就绪，正在计算轨道吊任务序列……");

            localAlgorithmCoordinator.RunTaskCase(
                input,
                delegate(
                    SchedulingRunResponseDto response)
                {
                    runResponse = response;
                    finished = true;
                },
                delegate(string error)
                {
                    runError = error;
                    finished = true;
                });

            while (!finished)
                yield return null;

            if (!string.IsNullOrWhiteSpace(runError))
            {
                Fail(onError, runError);
                yield break;
            }

            string validationError;
            if (!TryValidate(
                    input,
                    runResponse,
                    out validationError))
            {
                Fail(onError, validationError);
                yield break;
            }

            SceneExampleScheduleRuntime.Set(
                input,
                runResponse.result);

            // 在Console输出全部任务，并保存完整JSON。
            SceneScheduleDebugExporter.LogAndSave(
                input,
                runResponse.result);

            IsRunning = false;

            Debug.Log(
                "[SceneAlgorithm] 调度完成，scheduleId=" +
                runResponse.result.scheduleId +
                "，任务数=" +
                runResponse.result.operations.Length);
            Progress(
                onProgress,
                "轨道吊任务序列已生成，正在进入 VR 场景……");

            if (onSuccess != null)
                onSuccess(runResponse.result);
        }

        private static bool TryValidate(
            TaskCaseDocumentDto input,
            SchedulingRunResponseDto response,
            out string error)
        {
            if (response == null ||
                !response.success ||
                response.result == null ||
                response.result.operations == null)
            {
                error = "本地算法没有返回有效调度结果。";
                return false;
            }

            if (input.jobs == null ||
                response.result.operations.Length !=
                input.jobs.Length)
            {
                error =
                    "调度结果没有覆盖全部作业：输入=" +
                    (input.jobs == null
                        ? 0
                        : input.jobs.Length) +
                    "，输出=" +
                    response.result.operations.Length;
                return false;
            }

            bool[] sequences =
                new bool[
                    response.result.operations.Length + 1];
            for (int index = 0;
                 index <
                 response.result.operations.Length;
                 index++)
            {
                ScheduledOperationDto operation =
                    response.result.operations[index];
                if (operation == null ||
                    operation.sequence <= 0 ||
                    operation.sequence >=
                    sequences.Length ||
                    sequences[operation.sequence] ||
                    string.IsNullOrWhiteSpace(
                        operation.jobId) ||
                    string.IsNullOrWhiteSpace(
                        operation.craneId) ||
                    operation.plannedStartSeconds < 0f ||
                    operation.plannedEndSeconds <=
                    operation.plannedStartSeconds)
                {
                    error =
                        "第 " +
                        (index + 1) +
                        " 条调度指令无效。";
                    return false;
                }

                sequences[operation.sequence] = true;
            }

            error = null;
            return true;
        }

        private void Fail(
            Action<string> onError,
            string message)
        {
            IsRunning = false;
            Debug.LogError(
                "[SceneAlgorithm] " +
                message);
            InvokeError(onError, message);
        }

        private static void Progress(
            Action<string> callback,
            string message)
        {
            Debug.Log(
                "[SceneAlgorithm] " +
                message);
            if (callback != null)
                callback(message);
        }

        private static void InvokeError(
            Action<string> callback,
            string message)
        {
            if (callback != null)
                callback(message);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}

