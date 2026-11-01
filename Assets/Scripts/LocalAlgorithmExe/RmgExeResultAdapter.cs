using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;
using Vtp.VR.Rest;

namespace Vtp.VR.LocalAlgorithmExe
{
    /// <summary>
    /// 把算法EXE详细JSON中的“调度序列”转换成项目现有VR调度模型。
    /// </summary>
    internal static class RmgExeResultAdapter
    {
        private sealed class ConvertedRow
        {
            public int OriginalSequence;
            public SchedulingJobInputDto Job;
            public ScheduledOperationDto Operation;
            public SceneNumericalExampleDto Example;
        }

        public static bool TryConvert(
            string resultJsonPath,
            string taskId,
            string algorithm,
            out TaskCaseDocumentDto input,
            out SchedulingResultDto schedule,
            out SceneNumericalExampleDto[] examples,
            out string error)
        {
            input = null;
            schedule = null;
            examples = null;
            error = null;

            JObject document;
            try
            {
                document = JObject.Parse(
                    File.ReadAllText(resultJsonPath));
            }
            catch (Exception exception)
            {
                error =
                    "无法读取算法详细结果：" +
                    exception.Message;
                return false;
            }

            JObject sheets = document["sheets"] as JObject;
            JArray rows =
                sheets == null
                    ? null
                    : sheets["调度序列"] as JArray;
            if (rows == null || rows.Count == 0)
            {
                error =
                    "算法详细结果中没有 sheets/调度序列。";
                return false;
            }

            List<ConvertedRow> converted =
                new List<ConvertedRow>();
            HashSet<string> jobIds =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> craneInitialBays =
                new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < rows.Count; index++)
            {
                JObject row = rows[index] as JObject;
                if (row == null)
                {
                    error =
                        "调度序列第" +
                        (index + 1) +
                        "行不是有效对象。";
                    return false;
                }

                int sequence =
                    ReadInt(row, "执行顺序", index + 1);
                string jobId =
                    ReadString(row, "任务编号");
                if (string.IsNullOrWhiteSpace(jobId))
                    jobId = (index + 1).ToString();
                if (!jobIds.Add(jobId))
                {
                    jobId = jobId + "-" + (index + 1);
                    jobIds.Add(jobId);
                }

                string taskType =
                    ReadString(row, "任务类型");
                string craneId =
                    ReadString(row, "RMG");
                if (string.IsNullOrWhiteSpace(taskType) ||
                    string.IsNullOrWhiteSpace(craneId))
                {
                    error =
                        "调度序列第" +
                        (index + 1) +
                        "行缺少任务类型或RMG。";
                    return false;
                }

                int sourceBay;
                int sourceRow;
                int sourceTier;
                ParsePosition(
                    ReadString(row, "起点(贝,列,层)"),
                    ReadString(row, "起点箱位号"),
                    out sourceBay,
                    out sourceRow,
                    out sourceTier);

                int targetBay;
                int targetRow;
                int targetTier;
                ParsePosition(
                    ReadString(row, "终点(贝,列,层)"),
                    ReadString(row, "目标箱位号"),
                    out targetBay,
                    out targetRow,
                    out targetTier);

                string fromCode =
                    ReadString(row, "起点箱位号");
                string toCode =
                    ReadString(row, "目标箱位号");
                string sourceAreaType = ResolveAreaType(
                    ReadString(row, "起点类型"),
                    taskType,
                    true);
                string targetAreaType = ResolveAreaType(
                    ReadString(row, "终点类型"),
                    taskType,
                    false);
                string sourceAreaId = ResolveAreaId(
                    sourceAreaType,
                    ReadString(row, "起始箱区"),
                    fromCode);
                string targetAreaId = ResolveAreaId(
                    targetAreaType,
                    ReadString(row, "目标箱区"),
                    toCode);

                float plannedStart =
                    ReadFloat(row, "开始时间(秒)", 0f);
                float plannedEnd =
                    ReadFloat(row, "结束时间(秒)", 0f);
                float duration =
                    ReadFloat(
                        row,
                        "作业时长(秒)",
                        plannedEnd - plannedStart);
                if (plannedEnd <= plannedStart)
                    plannedEnd = plannedStart + Math.Max(0.01f, duration);

                SlotPositionDto source =
                    BuildPosition(
                        sourceAreaType,
                        sourceAreaId,
                        sourceBay,
                        sourceRow,
                        sourceTier,
                        fromCode);
                SlotPositionDto target =
                    BuildPosition(
                        targetAreaType,
                        targetAreaId,
                        targetBay,
                        targetRow,
                        targetTier,
                        toCode);

                SchedulingJobInputDto job =
                    new SchedulingJobInputDto
                    {
                        jobId = jobId,
                        containerNo = "TASK-" + jobId,
                        taskType = taskType,
                        craneHint = craneId,
                        startPointPosition = sourceBay,
                        startList = sourceRow,
                        endPointPosition = targetBay,
                        endList = targetRow,
                        source = source,
                        target = target,
                        releaseTimeSeconds = 0f,
                        priority = 0,
                        estimatedDurationSeconds =
                            Math.Max(0.01f, duration)
                    };

                ScheduledOperationDto operation =
                    new ScheduledOperationDto
                    {
                        sequence = sequence,
                        jobId = jobId,
                        containerNo = job.containerNo,
                        craneId = craneId,
                        fromCode = fromCode,
                        toCode = toCode,
                        plannedStartSeconds = plannedStart,
                        plannedEndSeconds = plannedEnd,
                        taskType = taskType,
                        sourceAreaType = source.areaType,
                        sourceAreaId = source.areaId,
                        sourceBay = source.bay,
                        sourceRow = source.row,
                        sourceTier = source.tier,
                        targetAreaType = target.areaType,
                        targetAreaId = target.areaId,
                        targetBay = target.bay,
                        targetRow = target.row,
                        targetTier = target.tier
                    };

                SceneNumericalExampleDto example =
                    new SceneNumericalExampleDto
                    {
                        id = jobId,
                        courseTaskId = taskId,
                        no = sequence.ToString(),
                        argm = craneId,
                        type = taskType,
                        startPointPosition = sourceBay,
                        startList = sourceRow,
                        endPointPosition = targetBay,
                        endList = targetRow,
                        time_consuming =
                            Math.Max(0.01f, duration)
                    };

                converted.Add(
                    new ConvertedRow
                    {
                        OriginalSequence = sequence,
                        Job = job,
                        Operation = operation,
                        Example = example
                    });

                if (!craneInitialBays.ContainsKey(craneId))
                    craneInitialBays.Add(craneId, sourceBay);
            }

            converted.Sort(
                delegate(ConvertedRow left, ConvertedRow right)
                {
                    return left.OriginalSequence.CompareTo(
                        right.OriginalSequence);
                });

            List<SchedulingJobInputDto> jobs =
                new List<SchedulingJobInputDto>();
            List<ScheduledOperationDto> operations =
                new List<ScheduledOperationDto>();
            List<SceneNumericalExampleDto> viewExamples =
                new List<SceneNumericalExampleDto>();
            float makespan = 0f;

            for (int index = 0; index < converted.Count; index++)
            {
                ConvertedRow item = converted[index];
                item.Operation.sequence = index + 1;
                item.Example.no = (index + 1).ToString();
                jobs.Add(item.Job);
                operations.Add(item.Operation);
                viewExamples.Add(item.Example);
                makespan = Math.Max(
                    makespan,
                    item.Operation.plannedEndSeconds);
            }

            List<CraneInputDto> cranes =
                new List<CraneInputDto>();
            foreach (
                KeyValuePair<string, int> item
                in craneInitialBays)
            {
                cranes.Add(
                    new CraneInputDto
                    {
                        craneId = item.Key,
                        initialBay = item.Value,
                        speed = 2f
                    });
            }

            input =
                new TaskCaseDocumentDto
                {
                    schemaVersion = "2.0-exe",
                    taskId =
                        string.IsNullOrWhiteSpace(taskId)
                            ? "exe-task"
                            : taskId.Trim(),
                    taskVersion = 1,
                    sceneId = "SceneStage1",
                    generatedAt = DateTime.UtcNow.ToString("o"),
                    cranes = cranes.ToArray(),
                    jobs = jobs.ToArray(),
                    constraints =
                        new SchedulingConstraintDto
                        {
                            minimumCraneDistance = 2,
                            allowCrossing = false,
                            maximumRunSeconds = 900
                        }
                };

            schedule =
                new SchedulingResultDto
                {
                    scheduleId =
                        input.taskId +
                        "-" +
                        algorithm +
                        "-" +
                        DateTime.Now.ToString("yyyyMMddHHmmss"),
                    operations = operations.ToArray(),
                    metrics =
                        new SchedulingMetricsDto
                        {
                            jobCount = operations.Count,
                            makespanSeconds = makespan,
                            emptyTravelDistance = 0f
                        }
                };
            examples = viewExamples.ToArray();
            return true;
        }

        private static SlotPositionDto BuildPosition(
            string areaType,
            string areaId,
            int bay,
            int row,
            int tier,
            string displayCode)
        {
            return new SlotPositionDto
            {
                areaType = areaType,
                areaId = areaId,
                bay = bay,
                row = row,
                tier = Math.Max(1, tier),
                displayCode = displayCode
            };
        }

        private static string ResolveAreaType(
            string label,
            string taskType,
            bool source)
        {
            string value = label ?? string.Empty;
            if (value.Contains("堆场")) return "YARD";
            if (value.Contains("支架") || value.Contains("船"))
                return "VESSEL";
            if (value.Contains("车") || value.Contains("陆侧"))
                return "OUTSIDE";

            switch (taskType)
            {
                case "卸船": return source ? "VESSEL" : "YARD";
                case "装船": return source ? "YARD" : "VESSEL";
                case "翻箱": return "YARD";
                case "卸车": return source ? "OUTSIDE" : "YARD";
                case "装车": return source ? "YARD" : "OUTSIDE";
                default: return "YARD";
            }
        }

        private static string ResolveAreaId(
            string areaType,
            string block,
            string code)
        {
            if (areaType == "VESSEL") return "Vessel004";
            if (areaType == "OUTSIDE") return "OUTSIDE";
            if (!string.IsNullOrWhiteSpace(block))
                return block.Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(code) &&
                code.Length >= 3)
            {
                return code.Substring(0, 3).ToUpperInvariant();
            }
            return "A40";
        }

        private static void ParsePosition(
            string tuple,
            string code,
            out int bay,
            out int row,
            out int tier)
        {
            bay = 0;
            row = 1;
            tier = 1;

            string normalized =
                (tuple ?? string.Empty)
                .Trim()
                .Trim('(', ')', '（', '）');
            string[] parts = normalized.Split(',');
            if (parts.Length == 3 &&
                TryParseInt(parts[0], out bay) &&
                TryParseInt(parts[1], out row) &&
                TryParseInt(parts[2], out tier))
            {
                return;
            }

            string value = (code ?? string.Empty).Trim();
            if (value.Length >= 9)
            {
                int parsedBay;
                int parsedRow;
                int parsedTier;
                string numeric = value.Substring(3);
                if (numeric.Length >= 6 &&
                    TryParseInt(numeric.Substring(0, 3), out parsedBay) &&
                    TryParseInt(numeric.Substring(3, 2), out parsedRow) &&
                    TryParseInt(numeric.Substring(5, 1), out parsedTier))
                {
                    bay = parsedBay;
                    row = parsedRow;
                    tier = parsedTier;
                }
            }
        }

        private static string ReadString(
            JObject row,
            string key)
        {
            JToken token = row[key];
            return token == null || token.Type == JTokenType.Null
                ? string.Empty
                : token.ToString().Trim();
        }

        private static int ReadInt(
            JObject row,
            string key,
            int fallback)
        {
            int value;
            return TryParseInt(ReadString(row, key), out value)
                ? value
                : fallback;
        }

        private static float ReadFloat(
            JObject row,
            string key,
            float fallback)
        {
            float value;
            return float.TryParse(
                    ReadString(row, key),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value)
                ? value
                : fallback;
        }

        private static bool TryParseInt(
            string value,
            out int result)
        {
            return int.TryParse(
                (value ?? string.Empty).Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out result);
        }
    }
}
