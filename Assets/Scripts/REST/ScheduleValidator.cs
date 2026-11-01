using System;
using System.Collections.Generic;

namespace Vtp.VR.Rest
{
    public static class ScheduleValidator
    {
        public static bool TryValidate(
            TaskCaseDocumentDto taskCase,
            AlgorithmReferenceDto requiredAlgorithm,
            Vtp.VR.LocalAlgorithm.SchedulingRunResponseDto response,
            out string error)
        {
            if (taskCase == null ||
                requiredAlgorithm == null ||
                response == null ||
                response.result == null)
            {
                error = "调度校验参数不完整。";
                return false;
            }

            if (!response.success)
            {
                error = "算法服务返回 success=false。";
                return false;
            }

            if (!string.Equals(
                    response.task_id,
                    taskCase.taskId,
                    StringComparison.Ordinal) ||
                response.task_version != taskCase.taskVersion)
            {
                error = "调度结果的任务ID或任务版本不匹配。";
                return false;
            }

            if (!string.Equals(
                    response.algorithm_sha256,
                    requiredAlgorithm.sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                error = "运行算法SHA与任务要求的算法SHA不一致。";
                return false;
            }

            ScheduledOperationDto[] operations =
                response.result.operations;
            if (operations == null ||
                taskCase.jobs == null ||
                operations.Length != taskCase.jobs.Length)
            {
                error = "调度结果没有覆盖全部算例作业。";
                return false;
            }

            Dictionary<string, SchedulingJobInputDto> jobs =
                new Dictionary<string, SchedulingJobInputDto>(
                    StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < taskCase.jobs.Length; index++)
                jobs.Add(taskCase.jobs[index].jobId, taskCase.jobs[index]);

            HashSet<string> craneIds =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < taskCase.cranes.Length; index++)
                craneIds.Add(taskCase.cranes[index].craneId);

            HashSet<string> scheduledJobs =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<int> sequences = new HashSet<int>();
            Dictionary<string, List<ScheduledOperationDto>> byCrane =
                new Dictionary<string, List<ScheduledOperationDto>>(
                    StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < operations.Length; index++)
            {
                ScheduledOperationDto operation = operations[index];
                string prefix = "第 " + (index + 1) + " 条调度指令：";

                if (operation == null)
                {
                    error = prefix + "内容为空。";
                    return false;
                }

                if (operation.sequence <= 0 ||
                    !sequences.Add(operation.sequence))
                {
                    error = prefix + "sequence 无效或重复。";
                    return false;
                }

                SchedulingJobInputDto job;
                if (string.IsNullOrWhiteSpace(operation.jobId) ||
                    !jobs.TryGetValue(operation.jobId, out job) ||
                    !scheduledJobs.Add(operation.jobId))
                {
                    error = prefix + "jobId 不存在或重复调度。";
                    return false;
                }

                if (!string.Equals(
                        operation.containerNo,
                        job.containerNo,
                        StringComparison.OrdinalIgnoreCase))
                {
                    error = prefix + "containerNo 与算例不一致。";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(operation.craneId) ||
                    !craneIds.Contains(operation.craneId))
                {
                    error = prefix + "轨道吊编号不存在。";
                    return false;
                }

                if (operation.plannedStartSeconds < 0f ||
                    operation.plannedEndSeconds <=
                    operation.plannedStartSeconds)
                {
                    error = prefix + "计划开始/结束时间无效。";
                    return false;
                }

                string expectedFrom = BuildDisplayCode(job.source);
                string expectedTo = BuildDisplayCode(job.target);
                if (!string.IsNullOrWhiteSpace(operation.fromCode) &&
                    !string.Equals(
                        operation.fromCode,
                        expectedFrom,
                        StringComparison.OrdinalIgnoreCase))
                {
                    error = prefix + "起始箱位与算例不一致。";
                    return false;
                }
                if (!string.IsNullOrWhiteSpace(operation.toCode) &&
                    !string.Equals(
                        operation.toCode,
                        expectedTo,
                        StringComparison.OrdinalIgnoreCase))
                {
                    error = prefix + "目标箱位与算例不一致。";
                    return false;
                }

                List<ScheduledOperationDto> craneOperations;
                if (!byCrane.TryGetValue(
                        operation.craneId,
                        out craneOperations))
                {
                    craneOperations = new List<ScheduledOperationDto>();
                    byCrane.Add(operation.craneId, craneOperations);
                }
                craneOperations.Add(operation);
            }

            for (int expected = 1;
                 expected <= operations.Length;
                 expected++)
            {
                if (!sequences.Contains(expected))
                {
                    error = "调度 sequence 必须从1开始连续编号。";
                    return false;
                }
            }

            foreach (KeyValuePair<string, List<ScheduledOperationDto>> item
                     in byCrane)
            {
                item.Value.Sort(
                    delegate(
                        ScheduledOperationDto left,
                        ScheduledOperationDto right)
                    {
                        return left.plannedStartSeconds.CompareTo(
                            right.plannedStartSeconds);
                    });

                for (int index = 1; index < item.Value.Count; index++)
                {
                    if (item.Value[index].plannedStartSeconds <
                        item.Value[index - 1].plannedEndSeconds)
                    {
                        error = "轨道吊 " + item.Key +
                                " 的调度时间发生重叠。";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        public static string BuildDisplayCode(SlotPositionDto position)
        {
            if (position == null)
                return string.Empty;
            if (!string.IsNullOrWhiteSpace(position.displayCode))
                return position.displayCode.Trim();

            if (string.Equals(
                    position.areaType,
                    "OUTSIDE",
                    StringComparison.OrdinalIgnoreCase))
                return "港外";

            return string.Format(
                "{0}-{1:D2}-{2:D2}-{3:D2}",
                position.areaId ?? position.areaType,
                position.bay,
                position.row,
                position.tier);
        }
    }
}
