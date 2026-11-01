using System;
using System.Collections.Generic;
using UnityEngine;
using ZPMC.VR.Unity;

namespace Vtp.VR.Integration
{
    /// <summary>
    /// 一个40英尺箱实际占用的两个20英尺箱位。
    /// 例如中心贝位为2时，占用贝位1和3。
    /// </summary>
    [Serializable]
    public struct FortyFootSlotFootprint
    {
        public string BlockName;
        public int CenterBay;
        public int FirstBay;
        public int SecondBay;
        public int Column;
        public int Tier;
        public string FirstSlotCode;
        public string SecondSlotCode;

        public FortyFootSlotFootprint(
            string blockName,
            int centerBay,
            int firstBay,
            int secondBay,
            int column,
            int tier,
            string firstSlotCode,
            string secondSlotCode)
        {
            BlockName = blockName;
            CenterBay = centerBay;
            FirstBay = firstBay;
            SecondBay = secondBay;
            Column = column;
            Tier = tier;
            FirstSlotCode = firstSlotCode;
            SecondSlotCode = secondSlotCode;
        }

        public override string ToString()
        {
            return string.Format(
                "{0}，中心Bay={1}，占用={2}、{3}",
                BlockName,
                CenterBay,
                FirstSlotCode,
                SecondSlotCode);
        }
    }

    /// <summary>
    /// 40英尺目标箱位检查和分配工具。
    /// </summary>
    public static class FortyFootYardSlotAllocator
    {
        public const int MinimumCenterBay = 2;
        // A40 的地图已由 SettingsLoader 补齐到 Bay 125。
        // 40尺中心位最大取122，对应实际占用121和123。
        public const int MaximumCenterBay = 122;
        public const int CenterBayStep = 4;
        public const int MaximumColumn = 10;
        public const int MaximumTier = 5;

        /// <summary>
        /// 判断Bay是否为40英尺箱的合法中心贝位。
        /// 合法值为2、6、10、14……122。
        /// </summary>
        public static bool IsValidCenterBay(int bay)
        {
            return bay >= MinimumCenterBay &&
                   bay <= MaximumCenterBay &&
                   (bay - MinimumCenterBay) % CenterBayStep == 0;
        }

        /// <summary>
        /// 使用项目原有方法生成标准箱位编码。
        /// </summary>
        public static string BuildSlotCode(
            string blockName,
            int bay,
            int column,
            int tier)
        {
            return BlockBuffer.BuildYardCode(
                blockName,
                bay,
                column,
                tier);
        }

        /// <summary>
        /// 计算一个40英尺箱占用的两个20英尺箱位。
        /// </summary>
        public static bool TryGetFootprint(
            string blockName,
            int centerBay,
            int column,
            int tier,
            out FortyFootSlotFootprint footprint,
            out string error)
        {
            footprint = default(FortyFootSlotFootprint);
            error = null;

            if (string.IsNullOrWhiteSpace(blockName))
            {
                error = "箱区名称为空。";
                return false;
            }

            blockName = blockName.Trim().ToUpperInvariant();

            if (!IsValidCenterBay(centerBay))
            {
                error = string.Format(
                    "Bay={0}不是合法的40英尺中心贝位，合法值为2、6、10……122。",
                    centerBay);
                return false;
            }

            if (column <= 0)
            {
                error = "列号必须大于0。";
                return false;
            }

            if (tier <= 0)
            {
                error = "层号必须大于0。";
                return false;
            }

            int firstBay = centerBay - 1;
            int secondBay = centerBay + 1;

            string firstSlotCode = BuildSlotCode(
                blockName,
                firstBay,
                column,
                tier);

            string secondSlotCode = BuildSlotCode(
                blockName,
                secondBay,
                column,
                tier);

            footprint = new FortyFootSlotFootprint(
                blockName,
                centerBay,
                firstBay,
                secondBay,
                column,
                tier,
                firstSlotCode,
                secondSlotCode);

            return true;
        }

        /// <summary>
        /// 返回占位检查用的贝位。
        /// 中心贝位用于检查场景中已经按40英尺中心位登记的箱子，
        /// 两侧贝位用于检查两个20英尺逻辑格。
        /// </summary>
        public static int[] GetFootprintBays(int centerBay)
        {
            return new[]
            {
                centerBay - 1,
                centerBay,
                centerBay + 1
            };
        }

        public static string BuildFootprintKey(
            string blockName,
            int footprintBay,
            int column,
            int tier)
        {
            return string.Format(
                "{0}|{1}|{2}|{3}",
                blockName == null
                    ? string.Empty
                    : blockName.Trim().ToUpperInvariant(),
                footprintBay,
                column,
                tier);
        }

        /// <summary>
        /// 把指定目标的完整40英尺范围加入预订集合。
        /// </summary>
        public static void Reserve(
            DischargeTargetSlot slot,
            HashSet<string> plannedFootprints)
        {
            if (slot == null || plannedFootprints == null)
                return;

            int[] footprintBays = GetFootprintBays(slot.Bay);

            for (int i = 0; i < footprintBays.Length; i++)
            {
                plannedFootprints.Add(
                    BuildFootprintKey(
                        slot.BlockName,
                        footprintBays[i],
                        slot.Column,
                        slot.Tier));
            }
        }

        /// <summary>
        /// 从请求贝位开始，寻找距离最近且可使用的40英尺中心贝位。
        /// 保留此方法是为了兼容AlgorithmDischargePlan等已有代码。
        /// </summary>
        public static bool TryAllocateNearest(
            DischargeTargetSlot requested,
            HashSet<string> plannedFootprints,
            out DischargeTargetSlot allocated,
            out string error)
        {
            allocated = null;
            error = null;

            if (requested == null)
            {
                error = "请求箱位为空。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(requested.BlockName))
            {
                error = "请求箱位没有箱区名称。";
                return false;
            }

            if (requested.Column < 1 ||
                requested.Column > MaximumColumn)
            {
                error = string.Format(
                    "列号必须为1到{0}，实际为{1}。",
                    MaximumColumn,
                    requested.Column);
                return false;
            }

            if (requested.Tier < 1 ||
                requested.Tier > MaximumTier)
            {
                error = string.Format(
                    "层号必须为1到{0}，实际为{1}。",
                    MaximumTier,
                    requested.Tier);
                return false;
            }

            if (BlockManager.Instance == null)
            {
                error = "BlockManager尚未初始化。";
                return false;
            }

            BlockBuffer block =
                BlockManager.Instance.FindBlockBuffer(
                    requested.BlockName);

            if (block == null)
            {
                error = "找不到箱区：" + requested.BlockName;
                return false;
            }

            List<int> bayCandidates =
                BuildCandidateCenters(requested.Bay);

            List<int> columnCandidates =
                BuildCandidateValues(
                    requested.Column,
                    1,
                    MaximumColumn);

            List<int> tierCandidates =
                BuildCandidateValues(
                    requested.Tier,
                    1,
                    MaximumTier);

            // 先保持算法请求的层号，再按最近Bay、邻近Column查找。
            // 只有当前层完全没有位置时才尝试其他层，避免无谓堆高。
            for (int tierIndex = 0;
                 tierIndex < tierCandidates.Count;
                 tierIndex++)
            {
                int candidateTier = tierCandidates[tierIndex];

                for (int bayIndex = 0;
                     bayIndex < bayCandidates.Count;
                     bayIndex++)
                {
                    int centerBay = bayCandidates[bayIndex];

                    for (int columnIndex = 0;
                         columnIndex < columnCandidates.Count;
                         columnIndex++)
                    {
                        int candidateColumn =
                            columnCandidates[columnIndex];

                        if (IsReserved(
                                requested.BlockName,
                                centerBay,
                                candidateColumn,
                                candidateTier,
                                plannedFootprints))
                        {
                            continue;
                        }

                        if (IsOccupied(
                                block,
                                centerBay,
                                candidateColumn,
                                candidateTier))
                        {
                            continue;
                        }

                        if (!HasSupport(
                                block,
                                requested.BlockName,
                                centerBay,
                                candidateColumn,
                                candidateTier,
                                plannedFootprints))
                        {
                            continue;
                        }

                        allocated = new DischargeTargetSlot
                        {
                            BlockName =
                                requested.BlockName.Trim().ToUpperInvariant(),
                            Bay = centerBay,
                            Column = candidateColumn,
                            Tier = candidateTier,
                            RawCode = requested.RawCode
                        };

                        Reserve(allocated, plannedFootprints);

                        if (requested.Bay != centerBay ||
                            requested.Column != candidateColumn ||
                            requested.Tier != candidateTier)
                        {
                            Debug.LogWarning(
                                string.Format(
                                    "[FortyFoot] 请求位置不可用：" +
                                    "Bay={0}, Column={1}, Tier={2}；" +
                                    "已调整为Bay={3}, Column={4}, Tier={5}。",
                                    requested.Bay,
                                    requested.Column,
                                    requested.Tier,
                                    centerBay,
                                    candidateColumn,
                                    candidateTier));
                        }

                        return true;
                    }
                }
            }

            error = string.Format(
                "箱区{0}中找不到可用40英尺箱位；" +
                "请求Bay={1}，Column={2}，Tier={3}。",
                requested.BlockName,
                requested.Bay,
                requested.Column,
                requested.Tier);

            return false;
        }

        private static List<int> BuildCandidateCenters(
            int requestedBay)
        {
            List<int> result = new List<int>();

            for (int bay = MinimumCenterBay;
                 bay <= MaximumCenterBay;
                 bay += CenterBayStep)
            {
                result.Add(bay);
            }

            result.Sort(
                delegate (int left, int right)
                {
                    int leftDistance =
                        Mathf.Abs(left - requestedBay);

                    int rightDistance =
                        Mathf.Abs(right - requestedBay);

                    int compare =
                        leftDistance.CompareTo(rightDistance);

                    return compare != 0
                        ? compare
                        : left.CompareTo(right);
                });

            return result;
        }

        private static List<int> BuildCandidateValues(
            int requestedValue,
            int minimum,
            int maximum)
        {
            List<int> result = new List<int>();

            for (int value = minimum;
                 value <= maximum;
                 value++)
            {
                result.Add(value);
            }

            result.Sort(
                delegate (int left, int right)
                {
                    int leftDistance =
                        Mathf.Abs(left - requestedValue);

                    int rightDistance =
                        Mathf.Abs(right - requestedValue);

                    int compare =
                        leftDistance.CompareTo(rightDistance);

                    return compare != 0
                        ? compare
                        : left.CompareTo(right);
                });

            return result;
        }

        private static bool IsReserved(
            string blockName,
            int centerBay,
            int column,
            int tier,
            HashSet<string> plannedFootprints)
        {
            if (plannedFootprints == null)
                return false;

            int[] footprintBays = GetFootprintBays(centerBay);

            for (int i = 0; i < footprintBays.Length; i++)
            {
                string key = BuildFootprintKey(
                    blockName,
                    footprintBays[i],
                    column,
                    tier);

                if (plannedFootprints.Contains(key))
                    return true;
            }

            return false;
        }

        private static bool IsOccupied(
            BlockBuffer block,
            int centerBay,
            int column,
            int tier)
        {
            int[] footprintBays = GetFootprintBays(centerBay);

            for (int i = 0; i < footprintBays.Length; i++)
            {
                if (block.FindContainer(
                        footprintBays[i],
                        column,
                        tier) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasSupport(
            BlockBuffer block,
            string blockName,
            int centerBay,
            int column,
            int tier,
            HashSet<string> plannedFootprints)
        {
            if (tier <= 1)
                return true;

            if (block.FindContainer(
                    centerBay,
                    column,
                    tier - 1) != null)
            {
                return true;
            }

            return plannedFootprints != null &&
                   plannedFootprints.Contains(
                       BuildFootprintKey(
                           blockName,
                           centerBay,
                           column,
                           tier - 1));
        }
    }
}
