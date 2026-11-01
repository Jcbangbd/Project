using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vtp.VR.Rest
{
    /// <summary>
    /// 把本地算法生成并校验过的调度序列导入现有TaskManager。
    /// 挂到目标实验场景中始终激活的物体上。
    /// </summary>
    public sealed class RemoteTaskSceneInstaller : MonoBehaviour
    {
        [Tooltip("等待场景原有管理器初始化完成。")]
        public float initialDelaySeconds = 1f;

        [Tooltip("等待TaskManager.Instance的最长时间。")]
        public float taskManagerTimeoutSeconds = 15f;

        [Tooltip("找不到起始箱位集装箱时是否阻止导入。")]
        public bool requireSourceContainer = true;

        [Tooltip("导入完成后向服务器回传STARTED。")]
        public bool reportStartedEvent = true;

        private bool _installed;

        private IEnumerator Start()
        {
            if (!RemoteTaskRuntime.HasReadyTask)
            {
                Debug.LogWarning(
                    "当前没有已完成算法计算的远程任务。");
                yield break;
            }

            if (initialDelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(
                    initialDelaySeconds);
            }

            float deadline =
                Time.realtimeSinceStartup +
                Mathf.Max(1f, taskManagerTimeoutSeconds);
            while (global::TaskManager.Instance == null &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (global::TaskManager.Instance == null)
            {
                Fail("场景中没有找到TaskManager。", true);
                yield break;
            }

            if (_installed)
                yield break;

            List<global::TaskInfo> tasks;
            string error;
            if (!TryBuildTaskInfos(out tasks, out error))
            {
                Fail(error, true);
                yield break;
            }

            global::TaskManager.Instance.ImportTask(tasks);
            _installed = true;

            Debug.Log(
                "算法调度序列已导入：taskId=" +
                RemoteTaskRuntime.CurrentTask.taskId +
                "，algorithm=" +
                RemoteTaskRuntime.CurrentAlgorithm.version +
                "，operations=" +
                tasks.Count);

            if (reportStartedEvent &&
                RestSession.IsAuthenticated)
            {
                ReportStarted();
            }
        }

        private bool TryBuildTaskInfos(
            out List<global::TaskInfo> result,
            out string error)
        {
            result = new List<global::TaskInfo>();
            error = null;

            TaskCaseDocumentDto taskCase =
                RemoteTaskRuntime.CurrentCase;
            SchedulingResultDto schedule =
                RemoteTaskRuntime.CurrentSchedule;

            if (taskCase == null ||
                taskCase.jobs == null ||
                schedule == null ||
                schedule.operations == null)
            {
                error = "任务算例或调度序列为空。";
                return false;
            }

            Dictionary<string, SchedulingJobInputDto> jobs =
                new Dictionary<string, SchedulingJobInputDto>(
                    StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < taskCase.jobs.Length; index++)
            {
                SchedulingJobInputDto job = taskCase.jobs[index];
                jobs[job.jobId] = job;
            }

            List<ScheduledOperationDto> operations =
                new List<ScheduledOperationDto>(
                    schedule.operations);
            operations.Sort(
                delegate(
                    ScheduledOperationDto left,
                    ScheduledOperationDto right)
                {
                    return left.sequence.CompareTo(right.sequence);
                });

            for (int index = 0; index < operations.Count; index++)
            {
                ScheduledOperationDto operation = operations[index];
                SchedulingJobInputDto job;
                if (operation == null ||
                    !jobs.TryGetValue(operation.jobId, out job))
                {
                    error = "调度序列引用了不存在的作业。";
                    return false;
                }

                global::Container container =
                    ResolveSourceContainer(job.source);
                if (container == null &&
                    requireSourceContainer &&
                    !IsOutside(job.source))
                {
                    error =
                        "找不到作业 " +
                        job.jobId +
                        " 起始箱位中的集装箱：" +
                        ScheduleValidator.BuildDisplayCode(job.source);
                    return false;
                }

                string fromCode =
                    string.IsNullOrWhiteSpace(operation.fromCode)
                        ? ScheduleValidator.BuildDisplayCode(job.source)
                        : operation.fromCode;
                string toCode =
                    string.IsNullOrWhiteSpace(operation.toCode)
                        ? ScheduleValidator.BuildDisplayCode(job.target)
                        : operation.toCode;

                global::TaskInfo info = new global::TaskInfo
                {
                    StartTime = operation.plannedStartSeconds,
                    EndTime = operation.plannedEndSeconds,
                    FromCode = fromCode,
                    ToCode = toCode,
                    BindContainer = container,
                    CurrentState = 1
                };
                result.Add(info);

                Debug.Log(
                    "调度指令 " +
                    operation.sequence +
                    "：crane=" +
                    operation.craneId +
                    "，container=" +
                    operation.containerNo +
                    "，from=" +
                    fromCode +
                    "，to=" +
                    toCode);
            }

            if (result.Count == 0)
            {
                error = "调度结果没有可执行指令。";
                return false;
            }

            return true;
        }

        private static global::Container ResolveSourceContainer(
            SlotPositionDto source)
        {
            if (source == null || IsOutside(source))
                return null;

            if (string.Equals(
                    source.areaType,
                    "YARD",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (global::BlockManager.Instance == null)
                    return null;

                global::BlockBuffer block =
                    global::BlockManager.Instance.FindBlockBuffer(
                        source.areaId);
                return block == null
                    ? null
                    : block.FindContainer(
                        source.bay,
                        source.row,
                        source.tier);
            }

            if (string.Equals(
                    source.areaType,
                    "VESSEL",
                    StringComparison.OrdinalIgnoreCase))
            {
                global::VesselManager manager =
                    global::VesselManager.GlobalInstance();
                if (manager == null)
                    return null;

                global::VesselBuffer vessel =
                    manager.FindVesselBuffer(source.areaId);
                return vessel == null
                    ? null
                    : vessel.FindContainer(
                        source.bay,
                        source.row,
                        source.tier);
            }

            return null;
        }

        private static bool IsOutside(SlotPositionDto position)
        {
            return position != null &&
                   string.Equals(
                       position.areaType,
                       "OUTSIDE",
                       StringComparison.OrdinalIgnoreCase);
        }

        private void ReportStarted()
        {
            VrTaskDetailDto task =
                RemoteTaskRuntime.CurrentTask;
            VrTaskService service =
                new VrTaskService(
                    RestApiClient.EnsureInstance());

            service.ReportEvent(
                task.taskId,
                VrTaskService.CreateEvent(
                    "STARTED",
                    task.version,
                    "scheduleId=" +
                    RemoteTaskRuntime.CurrentSchedule.scheduleId),
                delegate { },
                delegate(RestApiError error)
                {
                    Debug.LogWarning(
                        "STARTED状态回传失败：" +
                        error.Message);
                });
        }

        private void Fail(
            string message,
            bool reportToServer)
        {
            Debug.LogError("调度任务导入失败：" + message);

            if (!reportToServer ||
                !RestSession.IsAuthenticated ||
                RemoteTaskRuntime.CurrentTask == null)
                return;

            VrTaskDetailDto task =
                RemoteTaskRuntime.CurrentTask;
            VrTaskService service =
                new VrTaskService(
                    RestApiClient.EnsureInstance());
            service.ReportEvent(
                task.taskId,
                VrTaskService.CreateEvent(
                    "FAILED",
                    task.version,
                    message),
                delegate { },
                delegate(RestApiError error)
                {
                    Debug.LogWarning(
                        "FAILED状态回传失败：" +
                        error.Message);
                });
        }
    }
}
