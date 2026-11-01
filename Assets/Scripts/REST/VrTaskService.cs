using System;
using UnityEngine;

namespace Vtp.VR.Rest
{
    public sealed class VrTaskService
    {
        private const string Root = "/vr-api/v1";
        private readonly RestApiClient _client;

        public VrTaskService(RestApiClient client)
        {
            if (client == null)
                throw new ArgumentNullException("client");
            _client = client;
        }

        public void GetMyTasks(
            string statuses,
            string experimentId,
            int page,
            int pageSize,
            Action<VrTaskSummaryDto[]> onSuccess,
            Action<RestApiError> onError)
        {
            if (page < 1)
                page = 1;
            if (pageSize < 1)
                pageSize = 20;

            string path = Root + "/me/tasks?status=" +
                          Uri.EscapeDataString(statuses ?? string.Empty) +
                          "&experimentId=" +
                          Uri.EscapeDataString(experimentId ?? string.Empty) +
                          "&page=" + page +
                          "&pageSize=" + pageSize;

            _client.Get(
                path,
                delegate(string body)
                {
                    VrTaskListResponseEnvelope envelope;
                    try
                    {
                        envelope =
                            JsonUtility.FromJson<VrTaskListResponseEnvelope>(body);
                    }
                    catch (Exception ex)
                    {
                        InvokeError(onError, RestApiError.Client(
                            "任务列表响应无法解析：" + ex.Message));
                        return;
                    }

                    if (envelope == null || envelope.code != 200)
                    {
                        InvokeError(onError, RestApiError.Business(
                            envelope == null ? 0 : envelope.code,
                            envelope == null ? "任务列表响应为空。" : envelope.msg,
                            envelope == null ? null : envelope.requestId));
                        return;
                    }

                    VrTaskSummaryDto[] list =
                        envelope.result == null || envelope.result.list == null
                            ? new VrTaskSummaryDto[0]
                            : envelope.result.list;
                    if (onSuccess != null)
                        onSuccess(list);
                },
                onError);
        }

        public void GetTask(
            string taskId,
            Action<VrTaskDetailDto> onSuccess,
            Action<RestApiError> onError)
        {
            if (string.IsNullOrEmpty(taskId))
            {
                InvokeError(onError, RestApiError.Client("taskId 不能为空。"));
                return;
            }

            _client.Get(
                Root + "/me/tasks/" + Uri.EscapeDataString(taskId),
                delegate(string body)
                {
                    VrTaskDetailResponseEnvelope envelope;
                    try
                    {
                        envelope =
                            JsonUtility.FromJson<VrTaskDetailResponseEnvelope>(body);
                    }
                    catch (Exception ex)
                    {
                        InvokeError(onError, RestApiError.Client(
                            "任务详情响应无法解析：" + ex.Message));
                        return;
                    }

                    if (envelope == null || envelope.code != 200 ||
                        envelope.result == null)
                    {
                        InvokeError(onError, RestApiError.Business(
                            envelope == null ? 0 : envelope.code,
                            envelope == null ? "任务详情响应为空。" : envelope.msg,
                            envelope == null ? null : envelope.requestId));
                        return;
                    }

                    if (onSuccess != null)
                        onSuccess(envelope.result);
                },
                onError);
        }

        public void ReportEvent(
            string taskId,
            TaskEventRequestDto request,
            Action onSuccess,
            Action<RestApiError> onError)
        {
            if (string.IsNullOrEmpty(taskId) || request == null)
            {
                InvokeError(onError,
                    RestApiError.Client("任务事件参数不完整。"));
                return;
            }

            _client.PostJson(
                Root + "/me/tasks/" + Uri.EscapeDataString(taskId) + "/events",
                request,
                true,
                true,
                delegate(string body)
                {
                    ApiEmptyEnvelope envelope;
                    try
                    {
                        envelope = JsonUtility.FromJson<ApiEmptyEnvelope>(body);
                    }
                    catch (Exception ex)
                    {
                        InvokeError(onError, RestApiError.Client(
                            "任务事件响应无法解析：" + ex.Message));
                        return;
                    }

                    if (envelope != null && envelope.code == 200)
                    {
                        if (onSuccess != null)
                            onSuccess();
                        return;
                    }

                    InvokeError(onError, RestApiError.Business(
                        envelope == null ? 0 : envelope.code,
                        envelope == null ? "任务事件响应为空。" : envelope.msg,
                        envelope == null ? null : envelope.requestId));
                },
                onError);
        }

        public static TaskEventRequestDto CreateEvent(
            string eventType,
            int taskVersion,
            string detail)
        {
            return new TaskEventRequestDto
            {
                eventId = Guid.NewGuid().ToString("N"),
                eventType = eventType,
                taskVersion = taskVersion,
                occurredAt = DateTime.UtcNow.ToString("o"),
                deviceId = SystemInfo.deviceUniqueIdentifier,
                clientVersion = Application.version,
                detail = detail
            };
        }

        private static void InvokeError(
            Action<RestApiError> onError,
            RestApiError error)
        {
            if (onError != null)
                onError(error);
        }
    }
}
