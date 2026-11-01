using UnityEngine;

namespace Vtp.VR.Rest
{
    /// <summary>
    /// 挂到“提交结果/退出实验”按钮所在对象，供 UnityEvent 直接调用。
    /// </summary>
    public sealed class RemoteTaskResultReporter : MonoBehaviour
    {
        [Tooltip("完成事件 detail；正式项目可改为评分结果编号。")]
        public string completedDetail = "completed";

        public void ReportCompleted()
        {
            Report("COMPLETED", completedDetail);
        }

        public void ReportCancelled()
        {
            Report("CANCELLED", "user_cancelled");
        }

        public void ReportFailed(string reason)
        {
            Report("FAILED", reason);
        }

        private static void Report(string eventType, string detail)
        {
            VrTaskDetailDto task = RemoteTaskRuntime.CurrentTask;
            if (task == null)
            {
                Debug.LogWarning("没有当前远程任务，无法回传 " + eventType + "。 ");
                return;
            }

            if (!RestSession.IsAuthenticated)
            {
                Debug.LogWarning("REST 会话已失效，无法回传 " + eventType + "。 ");
                return;
            }

            VrTaskService service =
                new VrTaskService(RestApiClient.EnsureInstance());
            service.ReportEvent(
                task.taskId,
                VrTaskService.CreateEvent(eventType, task.version, detail),
                delegate
                {
                    Debug.Log("远程任务状态已回传：" + eventType);
                },
                delegate(RestApiError error)
                {
                    Debug.LogWarning(
                        "远程任务状态回传失败（" + eventType + "）：" +
                        error.Message);
                });
        }
    }
}
