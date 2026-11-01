using System;
using System.Collections;
using UnityEngine;
using Vtp.VR.LocalAlgorithm;
using Vtp.VR.Rest;

namespace Vtp.VR.Integration
{
    /// <summary>
    /// 编排任务详情、算例下载、算法同步、算法运行和调度结果校验。
    /// 建议与FastApiProcessManager、LocalAlgorithmCoordinator挂在同一持久物体。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TaskExecutionCoordinator : MonoBehaviour
    {
        public static TaskExecutionCoordinator Instance
        {
            get;
            private set;
        }

        [SerializeField]
        private LocalAlgorithmCoordinator localAlgorithmCoordinator;

        [SerializeField]
        private float localServiceReadyTimeoutSeconds = 320f;

        public bool IsPreparing { get; private set; }

        private RestApiClient _restClient;
        private VrTaskService _taskService;
        private TaskCaseDocumentService _caseService;
        private AlgorithmCatalogService _algorithmCatalog;

        private Action _onSuccess;
        private Action<string> _onProgress;
        private Action<RestApiError> _onError;
        private VrTaskDetailDto _currentTask;
        private ValidatedTaskCase _currentCase;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _restClient = RestApiClient.EnsureInstance();
            _taskService = new VrTaskService(_restClient);
            _caseService =
                new TaskCaseDocumentService(_restClient);
            _algorithmCatalog =
                new AlgorithmCatalogService(_restClient);

            if (localAlgorithmCoordinator == null)
            {
                localAlgorithmCoordinator =
                    GetComponent<LocalAlgorithmCoordinator>();
            }
            if (localAlgorithmCoordinator == null)
            {
                localAlgorithmCoordinator =
                    FindObjectOfType<LocalAlgorithmCoordinator>();
            }
        }

        public void PrepareTask(
            string taskId,
            Action onSuccess,
            Action<string> onProgress,
            Action<RestApiError> onError)
        {
            if (IsPreparing)
            {
                InvokeError(
                    onError,
                    RestApiError.Client(
                        "已有任务正在准备，请勿重复操作。"));
                return;
            }

            if (!RestSession.IsAuthenticated)
            {
                InvokeError(
                    onError,
                    RestApiError.Client("尚未登录远程服务器。"));
                return;
            }

            if (string.IsNullOrWhiteSpace(taskId))
            {
                InvokeError(
                    onError,
                    RestApiError.Client("taskId 不能为空。"));
                return;
            }

            IsPreparing = true;
            _onSuccess = onSuccess;
            _onProgress = onProgress;
            _onError = onError;
            _currentTask = null;
            _currentCase = null;
            RemoteTaskRuntime.Clear();

            Progress("正在读取任务详情……");
            _taskService.GetTask(
                taskId.Trim(),
                OnTaskReceived,
                Fail);
        }

        private void OnTaskReceived(VrTaskDetailDto task)
        {
            if (task == null ||
                task.caseDocument == null ||
                task.requiredAlgorithm == null)
            {
                Fail(
                    RestApiError.Client(
                        "任务详情缺少caseDocument或requiredAlgorithm。"));
                return;
            }

            _currentTask = task;
            Progress("正在下载并校验教师算例……");
            _caseService.DownloadAndValidate(
                task,
                delegate(ValidatedTaskCase taskCase)
                {
                    _currentCase = taskCase;
                    StartCoroutine(WaitForLocalService());
                },
                Fail);
        }

        private IEnumerator WaitForLocalService()
        {
            Progress("正在等待本地算法服务就绪……");

            if (localAlgorithmCoordinator == null)
            {
                Fail(
                    RestApiError.Client(
                        "场景中没有LocalAlgorithmCoordinator。"));
                yield break;
            }

            float deadline =
                Time.realtimeSinceStartup +
                Mathf.Max(5f, localServiceReadyTimeoutSeconds);

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
                        ? "等待本地FastAPI超时。"
                        : localAlgorithmCoordinator.LastError;
                Fail(RestApiError.Client(detail));
                yield break;
            }

            RequestAlgorithmTicket();
        }

        private void RequestAlgorithmTicket()
        {
            Progress("正在取得任务绑定算法的下载票据……");
            _algorithmCatalog.GetDownloadTicket(
                _currentTask.requiredAlgorithm,
                SyncAlgorithm,
                Fail);
        }

        private void SyncAlgorithm(
            AlgorithmDownloadTicketDto ticket)
        {
            Progress("正在检查并同步任务要求的算法……");
            localAlgorithmCoordinator.SyncRequiredAlgorithm(
                ticket,
                delegate(AlgorithmSyncResponseDto response)
                {
                    if (!AlgorithmMatchesTask(response.algorithm))
                    {
                        Fail(
                            RestApiError.Client(
                                "本地算法同步后仍与任务要求不一致。"));
                        return;
                    }

                    RunAlgorithm();
                },
                delegate(string error)
                {
                    Fail(RestApiError.Client(error));
                });
        }

        private bool AlgorithmMatchesTask(
            AlgorithmInfoDto installed)
        {
            return installed != null &&
                   string.Equals(
                       installed.algorithm_id,
                       _currentTask.requiredAlgorithm.algorithmId,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       NormalizeHash(installed.sha256),
                       NormalizeHash(
                           _currentTask.requiredAlgorithm.sha256),
                       StringComparison.OrdinalIgnoreCase);
        }

        private void RunAlgorithm()
        {
            Progress("算法已就绪，正在计算调度序列……");
            localAlgorithmCoordinator.RunTaskCase(
                _currentCase.Document,
                OnScheduleGenerated,
                delegate(string error)
                {
                    Fail(RestApiError.Client(error));
                });
        }

        private void OnScheduleGenerated(
            SchedulingRunResponseDto response)
        {
            Progress("正在校验调度序列……");

            string validationError;
            if (!ScheduleValidator.TryValidate(
                    _currentCase.Document,
                    _currentTask.requiredAlgorithm,
                    response,
                    out validationError))
            {
                Fail(
                    RestApiError.Client(
                        "调度结果校验失败：" +
                        validationError));
                return;
            }

            RemoteTaskRuntime.SetPrepared(
                _currentTask,
                _currentCase,
                response.result,
                _currentTask.requiredAlgorithm);

            Progress("任务、算法和调度序列均已准备完成。");
            ReportAcknowledgedAndComplete();
        }

        private void ReportAcknowledgedAndComplete()
        {
            TaskEventRequestDto request = VrTaskService.CreateEvent(
                "ACKNOWLEDGED",
                _currentTask.version,
                "algorithmVersion=" +
                _currentTask.requiredAlgorithm.version +
                ";algorithmSha256=" +
                _currentTask.requiredAlgorithm.sha256 +
                ";scheduleId=" +
                RemoteTaskRuntime.CurrentSchedule.scheduleId);

            _taskService.ReportEvent(
                _currentTask.taskId,
                request,
                Complete,
                delegate(RestApiError error)
                {
                    Debug.LogWarning(
                        "ACKNOWLEDGED回传失败，但本地任务已准备完成：" +
                        error.Message);
                    Complete();
                });
        }

        private void Complete()
        {
            IsPreparing = false;
            Action callback = _onSuccess;
            ClearCallbacks();
            if (callback != null)
                callback();
        }

        private void Fail(RestApiError error)
        {
            if (!IsPreparing)
                return;

            IsPreparing = false;
            ReportFailure(error == null ? "未知错误" : error.Message);

            Action<RestApiError> callback = _onError;
            ClearCallbacks();
            if (callback != null)
                callback(error ?? RestApiError.Client("未知错误"));
        }

        private void ReportFailure(string detail)
        {
            if (_currentTask == null ||
                !RestSession.IsAuthenticated)
                return;

            _taskService.ReportEvent(
                _currentTask.taskId,
                VrTaskService.CreateEvent(
                    "FAILED",
                    _currentTask.version,
                    detail),
                delegate { },
                delegate(RestApiError reportError)
                {
                    Debug.LogWarning(
                        "FAILED状态回传失败：" +
                        reportError.Message);
                });
        }

        private void Progress(string message)
        {
            Debug.Log("[TaskPipeline] " + message);
            if (_onProgress != null)
                _onProgress(message);
        }

        private void ClearCallbacks()
        {
            _onSuccess = null;
            _onProgress = null;
            _onError = null;
            _currentTask = null;
            _currentCase = null;
        }

        private static string NormalizeHash(string value)
        {
            return (value ?? string.Empty)
                .Replace("-", string.Empty)
                .Trim()
                .ToLowerInvariant();
        }

        private static void InvokeError(
            Action<RestApiError> callback,
            RestApiError error)
        {
            if (callback != null)
                callback(error);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
