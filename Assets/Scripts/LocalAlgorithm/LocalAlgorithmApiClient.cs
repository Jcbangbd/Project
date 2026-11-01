using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Vtp.VR.Rest;

namespace Vtp.VR.LocalAlgorithm
{
    public sealed class LocalAlgorithmApiClient
    {
        private const string ApiKeyHeader = "X-Local-Api-Key";
        private readonly FastApiProcessManager _manager;

        public LocalAlgorithmApiClient(FastApiProcessManager manager)
        {
            if (manager == null)
                throw new ArgumentNullException("manager");
            _manager = manager;
        }

        public IEnumerator GetStatus(
            Action<AlgorithmStatusDto> onSuccess,
            Action<string> onError)
        {
            if (!EnsureReady(onError))
                yield break;

            using (UnityWebRequest request = UnityWebRequest.Get(
                       _manager.BaseUrl +
                       "/api/v1/algorithm/status"))
            {
                Prepare(request, _manager.RequestTimeoutSeconds);
                yield return request.SendWebRequest();

                if (!Succeeded(request))
                {
                    InvokeError(onError, BuildError(request));
                    yield break;
                }

                AlgorithmStatusDto response;
                try
                {
                    response =
                        JsonUtility.FromJson<AlgorithmStatusDto>(
                            request.downloadHandler.text);
                }
                catch (Exception exception)
                {
                    InvokeError(
                        onError,
                        "算法状态JSON无法解析：" +
                        exception.Message);
                    yield break;
                }

                if (onSuccess != null)
                    onSuccess(response);
            }
        }

        public IEnumerator SyncAlgorithm(
            AlgorithmDownloadTicketDto ticket,
            int timeoutSeconds,
            Action<AlgorithmSyncResponseDto> onSuccess,
            Action<string> onError)
        {
            if (!EnsureReady(onError))
                yield break;
            if (ticket == null ||
                string.IsNullOrWhiteSpace(ticket.algorithmId) ||
                string.IsNullOrWhiteSpace(ticket.version) ||
                string.IsNullOrWhiteSpace(ticket.sha256) ||
                string.IsNullOrWhiteSpace(ticket.downloadUrl))
            {
                InvokeError(onError, "算法下载票据不完整。");
                yield break;
            }

            AlgorithmSyncRequestDto body =
                new AlgorithmSyncRequestDto
                {
                    algorithm_id = ticket.algorithmId,
                    version = ticket.version,
                    sha256 = ticket.sha256,
                    file_size = ticket.fileSize,
                    signature = ticket.signature ?? string.Empty,
                    download_url = ticket.downloadUrl,
                    download_timeout_seconds =
                        Mathf.Clamp(timeoutSeconds, 5, 300),
                    source = "remote-vr-server"
                };

            yield return SendJson(
                "/api/v1/algorithm/sync",
                body,
                Mathf.Clamp(timeoutSeconds + 10, 15, 310),
                delegate(string responseText)
                {
                    AlgorithmSyncResponseDto response;
                    try
                    {
                        response =
                            JsonUtility.FromJson<
                                AlgorithmSyncResponseDto>(
                                responseText);
                    }
                    catch (Exception exception)
                    {
                        InvokeError(
                            onError,
                            "算法同步响应无法解析：" +
                            exception.Message);
                        return;
                    }

                    if (response == null ||
                        response.algorithm == null)
                    {
                        InvokeError(
                            onError,
                            "算法同步响应缺少算法信息。");
                        return;
                    }

                    if (onSuccess != null)
                        onSuccess(response);
                },
                onError);
        }

        public IEnumerator Run(
            TaskCaseDocumentDto taskCase,
            int timeoutSeconds,
            Action<SchedulingRunResponseDto> onSuccess,
            Action<string> onError)
        {
            if (!EnsureReady(onError))
                yield break;
            if (taskCase == null)
            {
                InvokeError(onError, "任务算例为空。");
                yield break;
            }

            SchedulingRunRequestDto body =
                new SchedulingRunRequestDto
                {
                    request_id =
                        taskCase.taskId +
                        "-v" +
                        taskCase.taskVersion,
                    task_id = taskCase.taskId,
                    task_version = taskCase.taskVersion,
                    input = taskCase,
                    timeout_seconds =
                        Mathf.Clamp(timeoutSeconds, 1, 600)
                };

            yield return SendJson(
                "/api/v1/algorithm/run",
                    body,
                 Mathf.Clamp(timeoutSeconds + 60, 30, 660),
                delegate (string responseText)
                {
                    SchedulingRunResponseDto response;
                    try
                    {
                        response =
                            JsonUtility.FromJson<
                                SchedulingRunResponseDto>(
                                responseText);
                    }
                    catch (Exception exception)
                    {
                        InvokeError(
                            onError,
                            "调度结果JSON无法解析：" +
                            exception.Message);
                        return;
                    }

                    if (response == null || response.result == null)
                    {
                        InvokeError(
                            onError,
                            "算法没有返回有效调度结果。");
                        return;
                    }

                    if (onSuccess != null)
                        onSuccess(response);
                },
                onError);
        }

        private IEnumerator SendJson(
            string path,
            object body,
            int timeoutSeconds,
            Action<string> onSuccess,
            Action<string> onError)
        {
            string json = JsonUtility.ToJson(body);
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest request = new UnityWebRequest(
                       _manager.BaseUrl + path,
                       UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(bytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader(
                    "Content-Type",
                    "application/json; charset=utf-8");
                Prepare(request, timeoutSeconds);
                yield return request.SendWebRequest();

                if (!Succeeded(request))
                {
                    InvokeError(onError, BuildError(request));
                    yield break;
                }

                if (onSuccess != null)
                    onSuccess(request.downloadHandler.text);
            }
        }

        private bool EnsureReady(Action<string> onError)
        {
            if (_manager.IsReady)
                return true;

            InvokeError(
                onError,
                "本地FastAPI尚未就绪：" +
                (_manager.LastError ?? "仍在启动"));
            return false;
        }

        private void Prepare(
            UnityWebRequest request,
            int timeoutSeconds)
        {
            request.timeout = Mathf.Max(1, timeoutSeconds);
            request.SetRequestHeader(
                ApiKeyHeader,
                _manager.ApiKey);
        }

        private static bool Succeeded(UnityWebRequest request)
        {
            return request.result ==
                   UnityWebRequest.Result.Success;
        }

        private static string BuildError(UnityWebRequest request)
        {
            string body =
                request.downloadHandler == null
                    ? string.Empty
                    : request.downloadHandler.text;
            return "HTTP " + request.responseCode +
                   " " + request.error +
                   (string.IsNullOrWhiteSpace(body)
                       ? string.Empty
                       : "；响应：" + body);
        }

        private static void InvokeError(
            Action<string> onError,
            string message)
        {
            if (onError != null)
                onError(message);
        }
    }
}
