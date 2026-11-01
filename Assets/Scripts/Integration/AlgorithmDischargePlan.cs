using System;
using System.Collections.Generic;
using Vtp.VR.Rest;
using ZPMC.VR.Unity;

namespace Vtp.VR.Integration
{
    /// <summary>
    /// 把算法结果中“卸船”任务按 sequence 转换成 ScriptSTS 已使用的
    /// DischargeTargetSlot 队列。
    /// </summary>
    public static class AlgorithmDischargePlan
    {
        public static bool TryBuildTargets(
            out List<DischargeTargetSlot> targets,
            out string error)
        {
            targets = new List<DischargeTargetSlot>();
            error = null;

            if (!SceneExampleScheduleRuntime.HasSchedule)
            {
                error = "当前没有已生成的轨道吊调度序列。";
                return false;
            }

            TaskCaseDocumentDto input =
                SceneExampleScheduleRuntime.Input;
            SchedulingResultDto schedule =
                SceneExampleScheduleRuntime.Schedule;

            Dictionary<string, SchedulingJobInputDto> jobs =
                new Dictionary<string, SchedulingJobInputDto>(
                    StringComparer.OrdinalIgnoreCase);
            for (int index = 0;
                 index < input.jobs.Length;
                 index++)
            {
                SchedulingJobInputDto job =
                    input.jobs[index];
                if (job != null &&
                    !string.IsNullOrWhiteSpace(
                        job.jobId))
                {
                    jobs[job.jobId] = job;
                }
            }

            List<ScheduledOperationDto> operations =
                new List<ScheduledOperationDto>(
                    schedule.operations);
            HashSet<string> plannedFootprints =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
            operations.Sort(
                delegate (
                    ScheduledOperationDto left,
                    ScheduledOperationDto right)
                {
                    return left.sequence.CompareTo(
                        right.sequence);
                });

            for (int index = 0;
                 index < operations.Count;
                 index++)
            {
                ScheduledOperationDto operation =
                    operations[index];
                SchedulingJobInputDto job;
                if (operation == null ||
                    !jobs.TryGetValue(
                        operation.jobId,
                        out job))
                {
                    error =
                        "调度序列引用了不存在的 jobId。";
                    return false;
                }

                if (!string.Equals(
                        job.taskType,
                        "卸船",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (job.target == null ||
                    !string.Equals(
                        job.target.areaType,
                        "YARD",
                        StringComparison.OrdinalIgnoreCase))
                {
                    error =
                        "卸船作业 " +
                        job.jobId +
                        " 的目标不是堆场箱位。";
                    return false;
                }

                DischargeTargetSlot requested =
                    new DischargeTargetSlot
                    {
                        BlockName =
                            job.target.areaId,
                        Bay = job.target.bay,
                        Column = job.target.row,
                        Tier = job.target.tier,
                        RawCode =
                            job.target.displayCode
                    };

                DischargeTargetSlot allocated;
                string allocationError;
                if (!FortyFootYardSlotAllocator
                        .TryAllocateNearest(
                            requested,
                            plannedFootprints,
                            out allocated,
                            out allocationError))
                {
                    error =
                        "作业 " +
                        job.jobId +
                        " 无法分配40尺目标箱位：" +
                        allocationError;
                    return false;
                }

                targets.Add(allocated);
            }

            if (targets.Count == 0)
            {
                error =
                    "调度结果中没有“卸船”任务。";
                return false;
            }

            return true;
        }
    }
}
