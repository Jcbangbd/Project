using System;
using System.Collections.Generic;

namespace Vtp.VR.Rest
{
    public static class TaskCaseDocumentValidator
    {
        public static bool TryValidate(
            TaskCaseDocumentDto document,
            VrTaskDetailDto task,
            out string error)
        {
            if (document == null)
            {
                error = "任务算例为空。";
                return false;
            }

            if (task == null)
            {
                error = "任务详情为空。";
                return false;
            }

            if (!string.Equals(
                    document.schemaVersion,
                    "2.0",
                    StringComparison.Ordinal))
            {
                error = "不支持的算例版本：" +
                        (document.schemaVersion ?? "<null>");
                return false;
            }

            if (!string.Equals(
                    document.taskId,
                    task.taskId,
                    StringComparison.Ordinal))
            {
                error = "算例 taskId 与任务详情不一致。";
                return false;
            }

            if (document.taskVersion != task.version)
            {
                error = "算例 taskVersion 与任务版本不一致。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(document.sceneId))
            {
                error = "sceneId 不能为空。";
                return false;
            }

            if (document.cranes == null || document.cranes.Length == 0)
            {
                error = "算例中没有轨道吊。";
                return false;
            }

            HashSet<string> craneIds =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0;
                 index < document.cranes.Length;
                 index++)
            {
                CraneInputDto crane = document.cranes[index];
                if (crane == null ||
                    string.IsNullOrWhiteSpace(crane.craneId) ||
                    !craneIds.Add(crane.craneId.Trim()))
                {
                    error = "轨道吊编号为空或重复，位置：" + index;
                    return false;
                }

                if (crane.initialBay < 0 || crane.speed <= 0f)
                {
                    error = "轨道吊参数无效：" + crane.craneId;
                    return false;
                }
            }

            if (document.jobs == null || document.jobs.Length == 0)
            {
                error = "算例中没有待调度作业。";
                return false;
            }

            HashSet<string> jobIds =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> containerNumbers =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < document.jobs.Length; index++)
            {
                SchedulingJobInputDto job = document.jobs[index];
                string prefix = "第 " + (index + 1) + " 个作业：";

                if (job == null)
                {
                    error = prefix + "内容为空。";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(job.jobId) ||
                    !jobIds.Add(job.jobId.Trim()))
                {
                    error = prefix + "jobId 为空或重复。";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(job.containerNo) ||
                    !containerNumbers.Add(job.containerNo.Trim()))
                {
                    error = prefix + "containerNo 为空或重复。";
                    return false;
                }

                if (!ValidatePosition(job.source, prefix + "起始箱位", out error))
                    return false;
                if (!ValidatePosition(job.target, prefix + "目标箱位", out error))
                    return false;

                if (job.releaseTimeSeconds < 0f ||
                    job.estimatedDurationSeconds < 0f)
                {
                    error = prefix + "时间参数不能为负数。";
                    return false;
                }
            }

            if (document.constraints != null &&
                (document.constraints.minimumCraneDistance < 0 ||
                 document.constraints.maximumRunSeconds < 0))
            {
                error = "算例约束参数不能为负数。";
                return false;
            }

            error = null;
            return true;
        }

        private static bool ValidatePosition(
            SlotPositionDto position,
            string label,
            out string error)
        {
            if (position == null)
            {
                error = label + "为空。";
                return false;
            }

            string type =
                (position.areaType ?? string.Empty).Trim().ToUpperInvariant();
            if (type != "YARD" &&
                type != "VESSEL" &&
                type != "OUTSIDE")
            {
                error = label +
                        " areaType 必须是 YARD、VESSEL 或 OUTSIDE。";
                return false;
            }

            if (type == "OUTSIDE")
            {
                error = null;
                return true;
            }

            if (string.IsNullOrWhiteSpace(position.areaId) ||
                position.bay < 0 ||
                position.row < 0 ||
                position.tier < 0)
            {
                error = label + "的区域或 bay/row/tier 无效。";
                return false;
            }

            error = null;
            return true;
        }
    }
}
