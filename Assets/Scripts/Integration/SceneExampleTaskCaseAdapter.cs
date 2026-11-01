using System;
using System.Collections.Generic;
using UnityEngine;
using Vtp.VR.Rest;

namespace Vtp.VR.Integration
{
    /// <summary>
    /// 把 findSceneNumericalExample 返回的数据转换为本地算法服务已有的
    /// TaskCaseDocumentDto。原始浮点位置也会保留，算法不会丢失 2.5 之类的值。
    /// </summary>
    public static class SceneExampleTaskCaseAdapter
    {
        private const float SeaSidePosition = 0f;
        private const float LandSidePosition = 51f;
        private const float PositionTolerance = 0.001f;

        private enum PositionArea
        {
            Unknown,
            SeaSide,
            Yard,
            LandSide
        }

        public static bool TryBuild(
            string taskId,
            SceneNumericalExampleDto[] examples,
            string sceneId,
            string yardBlockName,
            string vesselName,
            int defaultTier,
            int maximumRunSeconds,
            out TaskCaseDocumentDto result,
            out string error)
        {
            result = null;
            error = null;

            if (string.IsNullOrWhiteSpace(taskId))
            {
                error = "taskId 不能为空。";
                return false;
            }

            if (examples == null || examples.Length == 0)
            {
                error = "远程算例为空。";
                return false;
            }

            string normalizedBlock =
                string.IsNullOrWhiteSpace(yardBlockName)
                    ? "A40"
                    : yardBlockName.Trim();
            string normalizedVessel =
                string.IsNullOrWhiteSpace(vesselName)
                    ? "Vessel004"
                    : vesselName.Trim();
            int tier = Mathf.Clamp(defaultTier, 1, 5);

            List<SchedulingJobInputDto> jobs =
                new List<SchedulingJobInputDto>();
            HashSet<string> usedIds =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < examples.Length; index++)
            {
                SceneNumericalExampleDto item = examples[index];
                if (item == null)
                    continue;

                string taskType;
                if (!TryNormalizeTaskType(
                        item.type,
                        out taskType))
                {
                    error =
                        "第 " +
                        (index + 1) +
                        " 条算例的作业类型无法识别：" +
                        item.type;
                    return false;
                }

                string jobId = FirstNotEmpty(
                    item.no,
                    item.id,
                    (index + 1).ToString());
                if (!usedIds.Add(jobId))
                {
                    jobId =
                        jobId +
                        "-" +
                        (index + 1);
                    usedIds.Add(jobId);
                }

                string sourceKind;
                string targetKind;
                GetRouteKinds(
                    taskType,
                    out sourceKind,
                    out targetKind);

                SlotPositionDto source = BuildPosition(
                    sourceKind,
                    normalizedBlock,
                    normalizedVessel,
                    item.startPointPosition,
                    item.startList,
                    tier);
                SlotPositionDto target = BuildPosition(
                    targetKind,
                    normalizedBlock,
                    normalizedVessel,
                    item.endPointPosition,
                    item.endList,
                    tier);

                jobs.Add(
                    new SchedulingJobInputDto
                    {
                        jobId = jobId,
                        containerNo = "TASK-" + jobId,
                        taskType = taskType,
                        craneHint = item.argm ?? string.Empty,
                        startPointPosition =
                            item.startPointPosition,
                        startList = item.startList,
                        endPointPosition =
                            item.endPointPosition,
                        endList = item.endList,
                        source = source,
                        target = target,
                        releaseTimeSeconds = 0f,
                        priority = 0,
                        estimatedDurationSeconds =
                            Mathf.Max(
                                0f,
                                item.time_consuming)
                    });
            }

            if (jobs.Count == 0)
            {
                error = "远程算例中没有有效作业。";
                return false;
            }

            result =
                new TaskCaseDocumentDto
                {
                    schemaVersion = "2.0",
                    taskId = taskId.Trim(),
                    taskVersion = 1,
                    sceneId =
                        string.IsNullOrWhiteSpace(sceneId)
                            ? "SceneStage1"
                            : sceneId.Trim(),
                    generatedAt =
                        DateTime.UtcNow.ToString("o"),
                    cranes =
                        new[]
                        {
                            new CraneInputDto
                            {
                                craneId = "RMG1",
                                initialBay = 1,
                                speed = 2f
                            },
                            new CraneInputDto
                            {
                                craneId = "RMG2",
                                initialBay = 50,
                                speed = 2f
                            }
                        },
                    jobs = jobs.ToArray(),
                    constraints =
                        new SchedulingConstraintDto
                        {
                            minimumCraneDistance = 2,
                            allowCrossing = false,
                            maximumRunSeconds =
                                Mathf.Clamp(
                                    maximumRunSeconds,
                                    1,
                                    600)
                        }
                };

            return true;
        }

        public static bool TryNormalizeTaskType(
            string raw,
            out string taskType)
        {
            string value =
                (raw ?? string.Empty)
                .Replace(" ", string.Empty)
                .Replace("→", "->");

            if (value.Contains("支架->堆场") ||
                value.Contains("海侧->堆场"))
                taskType = "卸船";
            else if (value.Contains("堆场->支架") ||
                     value.Contains("堆场->海侧"))
                taskType = "装船";
            else if (value.Contains("堆场->堆场"))
                taskType = "翻箱";
            else if (value.Contains("车道->堆场") ||
                     value.Contains("陆侧->堆场"))
                taskType = "卸车";
            else if (value.Contains("堆场->车道") ||
                     value.Contains("堆场->陆侧"))
                taskType = "装车";
            else if (value.Contains("卸船"))
                taskType = "卸船";
            else if (value.Contains("装船"))
                taskType = "装船";
            else if (value.Contains("翻箱"))
                taskType = "翻箱";
            else if (value.Contains("卸车"))
                taskType = "卸车";
            else if (value.Contains("装车"))
                taskType = "装车";
            else
            {
                taskType = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 根据算例起止贝位推断作业类型。
        /// 0贝为海侧支架，1至50贝为堆场，51贝为陆侧车道。
        /// </summary>
        public static bool TryInferTaskTypeFromPositions(
            SceneNumericalExampleDto example,
            out string taskType)
        {
            if (example == null)
            {
                taskType = null;
                return false;
            }

            return TryInferTaskTypeFromPositions(
                example.startPointPosition,
                example.endPointPosition,
                out taskType);
        }

        public static bool TryInferTaskTypeFromPositions(
            float startPointPosition,
            float endPointPosition,
            out string taskType)
        {
            PositionArea source = ClassifyPosition(startPointPosition);
            PositionArea target = ClassifyPosition(endPointPosition);

            if (source == PositionArea.SeaSide &&
                target == PositionArea.Yard)
                taskType = "卸船";
            else if (source == PositionArea.Yard &&
                     target == PositionArea.SeaSide)
                taskType = "装船";
            else if (source == PositionArea.Yard &&
                     target == PositionArea.Yard)
                taskType = "翻箱";
            else if (source == PositionArea.LandSide &&
                     target == PositionArea.Yard)
                taskType = "卸车";
            else if (source == PositionArea.Yard &&
                     target == PositionArea.LandSide)
                taskType = "装车";
            else
            {
                taskType = null;
                return false;
            }

            return true;
        }

        private static PositionArea ClassifyPosition(float position)
        {
            if (float.IsNaN(position) || float.IsInfinity(position))
                return PositionArea.Unknown;
            if (Mathf.Abs(position - SeaSidePosition) <= PositionTolerance)
                return PositionArea.SeaSide;
            if (Mathf.Abs(position - LandSidePosition) <= PositionTolerance)
                return PositionArea.LandSide;
            if (position > SeaSidePosition && position < LandSidePosition)
                return PositionArea.Yard;
            return PositionArea.Unknown;
        }

        private static void GetRouteKinds(
            string taskType,
            out string source,
            out string target)
        {
            switch (taskType)
            {
                case "卸船":
                    source = "VESSEL";
                    target = "YARD";
                    return;
                case "装船":
                    source = "YARD";
                    target = "VESSEL";
                    return;
                case "翻箱":
                    source = "YARD";
                    target = "YARD";
                    return;
                case "卸车":
                    source = "OUTSIDE";
                    target = "YARD";
                    return;
                case "装车":
                    source = "YARD";
                    target = "OUTSIDE";
                    return;
                default:
                    throw new ArgumentOutOfRangeException(
                        "taskType");
            }
        }

        private static SlotPositionDto BuildPosition(
            string kind,
            string blockName,
            string vesselName,
            float point,
            float row,
            int tier)
        {
            string areaId;
            if (kind == "YARD")
                areaId = blockName;
            else if (kind == "VESSEL")
                areaId = vesselName;
            else
                areaId = "OUTSIDE";

            return new SlotPositionDto
            {
                areaType = kind,
                areaId = areaId,
                bay = Mathf.RoundToInt(point),
                row = Mathf.RoundToInt(row),
                tier = tier,
                displayCode =
                    kind == "OUTSIDE"
                        ? "港外"
                        : string.Format(
                            "{0}-{1:0.###}-{2:0.###}-{3}",
                            areaId,
                            point,
                            row,
                            tier)
            };
        }

        private static string FirstNotEmpty(
            string first,
            string second,
            string fallback)
        {
            if (!string.IsNullOrWhiteSpace(first))
                return first.Trim();
            if (!string.IsNullOrWhiteSpace(second))
                return second.Trim();
            return fallback;
        }
    }
}
