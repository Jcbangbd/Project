using System;
using System.Collections;
using UnityEngine;
using Vtp.VR.Rest;

namespace Vtp.VR.LocalAlgorithm
{
    [DisallowMultipleComponent]
    public sealed class LocalAlgorithmCoordinator : MonoBehaviour
    {
        [SerializeField] private FastApiProcessManager processManager;
        [SerializeField] private int algorithmDownloadTimeoutSeconds = 60;
        [SerializeField] private int algorithmRunTimeoutSeconds = 30;

        public bool IsReady { get; private set; }
        public string LastError { get; private set; }
        public AlgorithmInfoDto CurrentAlgorithm { get; private set; }

        private LocalAlgorithmApiClient _client;

        private IEnumerator Start()
        {
            if (processManager == null)
                processManager = FastApiProcessManager.Instance;

            if (processManager == null)
            {
                LastError = "场景中没有FastApiProcessManager。";
                Debug.LogError("[Algorithm] " + LastError);
                yield break;
            }

            while (!processManager.IsReady &&
                   string.IsNullOrEmpty(processManager.LastError))
            {
                yield return null;
            }

            if (!processManager.IsReady)
            {
                LastError =
                    "本地服务不可用：" +
                    processManager.LastError;
                Debug.LogError("[Algorithm] " + LastError);
                yield break;
            }

            _client = new LocalAlgorithmApiClient(processManager);
            yield return _client.GetStatus(
                delegate(AlgorithmStatusDto status)
                {
                    CurrentAlgorithm =
                        status == null ? null : status.algorithm;
                    IsReady = true;
                    LastError = null;
                    Debug.Log("[Algorithm] 本地算法协调器已就绪。");
                },
                delegate(string error)
                {
                    LastError = error;
                    Debug.LogError(
                        "[Algorithm] 初始化失败：" + error);
                });
        }

        public void SyncRequiredAlgorithm(
            AlgorithmDownloadTicketDto ticket,
            Action<AlgorithmSyncResponseDto> onSuccess,
            Action<string> onError)
        {
            if (!EnsureReady(onError))
                return;

            StartCoroutine(
                _client.SyncAlgorithm(
                    ticket,
                    algorithmDownloadTimeoutSeconds,
                    delegate(AlgorithmSyncResponseDto response)
                    {
                        CurrentAlgorithm = response.algorithm;
                        Debug.Log(
                            "[Algorithm] 算法已就绪：version=" +
                            CurrentAlgorithm.version +
                            "，sha256=" +
                            CurrentAlgorithm.sha256);
                        if (onSuccess != null)
                            onSuccess(response);
                    },
                    delegate(string error)
                    {
                        LastError = error;
                        Debug.LogError(
                            "[Algorithm] 同步失败：" + error);
                        if (onError != null)
                            onError(error);
                    }));
        }

        public void RunTaskCase(
            TaskCaseDocumentDto taskCase,
            Action<SchedulingRunResponseDto> onSuccess,
            Action<string> onError)
        {
            if (!EnsureReady(onError))
                return;

            int timeout = algorithmRunTimeoutSeconds;
            if (taskCase != null &&
                taskCase.constraints != null &&
                taskCase.constraints.maximumRunSeconds > 0)
            {
                timeout = Mathf.Clamp(
                    taskCase.constraints.maximumRunSeconds,
                    1,
                    600);
            }

            StartCoroutine(
                _client.Run(
                    taskCase,
                    timeout,
                    delegate(SchedulingRunResponseDto response)
                    {
                        Debug.Log(
                            "[Algorithm] 调度完成：requestId=" +
                            response.request_id +
                            "，耗时=" +
                            response.duration_ms +
                            "ms");
                        if (onSuccess != null)
                            onSuccess(response);
                    },
                    delegate(string error)
                    {
                        LastError = error;
                        Debug.LogError(
                            "[Algorithm] 运行失败：" + error);
                        if (onError != null)
                            onError(error);
                    }));
        }

        private bool EnsureReady(Action<string> onError)
        {
            if (IsReady && _client != null)
                return true;

            string error =
                string.IsNullOrWhiteSpace(LastError)
                    ? "本地算法协调器仍在启动。"
                    : LastError;
            if (onError != null)
                onError(error);
            return false;
        }
    }
}
