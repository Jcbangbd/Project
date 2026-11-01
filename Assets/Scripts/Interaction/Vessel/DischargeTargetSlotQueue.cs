using System;
using System.Collections.Generic;
using UnityEngine;
using Vtp.VR.Integration;

namespace ZPMC.VR.Unity
{
    /// <summary>
    /// 卸船任务的目标箱位。
    ///
    /// 当前项目按照40英尺箱处理：
    /// 一个40英尺箱会占用前后两个20英尺箱位。
    /// Bay表示40英尺箱的中心贝位，例如：
    /// Bay=2  占用 Bay=1 和 Bay=3；
    /// Bay=6  占用 Bay=5 和 Bay=7。
    /// </summary>
    [Serializable]
    public sealed class DischargeTargetSlot
    {
        /// <summary>
        /// 堆场区块名称，例如A40。
        /// </summary>
        public string BlockName;

        /// <summary>
        /// 40英尺箱中心贝位。
        /// 合法值通常为2、6、10、14……
        /// </summary>
        public int Bay;

        /// <summary>
        /// 箱区列号。
        /// </summary>
        public int Column;

        /// <summary>
        /// 箱区层号，从1开始。
        /// </summary>
        public int Tier;

        /// <summary>
        /// 算法返回的原始箱位字符串。
        /// </summary>
        public string RawCode;

        public DischargeTargetSlot()
        {
        }

        public DischargeTargetSlot(
            string blockName,
            int bay,
            int column,
            int tier,
            string rawCode = null)
        {
            BlockName = blockName;
            Bay = bay;
            Column = column;
            Tier = tier;
            RawCode = rawCode;
        }

        /// <summary>
        /// 转换为项目原有的箱位编码。
        /// 示例：A400021001
        /// </summary>
        public string ToYardCode()
        {
            return FortyFootYardSlotAllocator.BuildSlotCode(
                BlockName,
                Bay,
                Column,
                Tier);
        }

        public override string ToString()
        {
            return ToYardCode();
        }
    }

    /// <summary>
    /// 卸船目标箱位队列。
    ///
    /// 主要职责：
    /// 1. 按调度算法返回的顺序保存目标箱位；
    /// 2. 检查目标是不是合法的40英尺箱位；
    /// 3. 检查40英尺箱占用的两个实际箱位是否都为空；
    /// 4. 检查两个实际箱位是否已经被其他任务预订；
    /// 5. 检查上层箱位是否有完整支撑；
    /// 6. 取出任务时同时预订完整的40英尺占用范围。
    /// </summary>
    public static class DischargeTargetSlotQueue
    {
        /// <summary>
        /// 项目箱区最大堆叠层数。
        /// 如果项目允许超过5层，请修改这里。
        /// </summary>
        private const int MaxTier = 5;

        /// <summary>
        /// 当前40英尺箱区名称。
        /// </summary>

        private static readonly object SyncRoot = new object();

        /// <summary>
        /// 等待执行的卸船目标箱位。
        /// </summary>
        private static readonly Queue<DischargeTargetSlot> TargetQueue =
            new Queue<DischargeTargetSlot>();

        /// <summary>
        /// 已经分配给任务、但箱子可能还没有实际落下的箱位。
        ///
        /// 保存的是40英尺箱实际占用的两个20英尺箱位编码，
        /// 防止多个任务在执行期间被分配到重叠位置。
        /// </summary>
        private static readonly HashSet<string> ReservedFootprints =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 当前队列中剩余目标数量。
        /// </summary>
        public static int Count
        {
            get
            {
                lock (SyncRoot)
                {
                    return TargetQueue.Count;
                }
            }
        }

        /// <summary>
        /// 当前是否还有目标箱位。
        /// </summary>
        public static bool HasTarget
        {
            get
            {
                lock (SyncRoot)
                {
                    return TargetQueue.Count > 0;
                }
            }
        }

        /// <summary>
        /// 清空目标队列和所有临时预订。
        ///
        /// 加载新的算例或重新登录时应调用此方法，
        /// 防止上一个算例的箱位预订影响新算例。
        /// </summary>
        public static void Clear()
        {
            lock (SyncRoot)
            {
                TargetQueue.Clear();
                ReservedFootprints.Clear();
            }

            Debug.Log("[DischargeTargetSlotQueue] 已清空目标队列和40英尺箱位预订。");
        }

        /// <summary>
        /// 加载调度算法返回的目标箱位。
        ///
        /// 调用该方法会自动清除旧队列和旧的临时预订。
        /// 目标顺序会按照传入顺序保存。
        /// </summary>
        public static void LoadTargets(IEnumerable<DischargeTargetSlot> targets)
        {
            lock (SyncRoot)
            {
                TargetQueue.Clear();
                ReservedFootprints.Clear();

                if (targets == null)
                {
                    Debug.LogWarning(
                        "[DischargeTargetSlotQueue] 目标箱位集合为空，未加载任何箱位。");
                    return;
                }

                int loadedCount = 0;

                foreach (DischargeTargetSlot target in targets)
                {
                    if (target == null)
                    {
                        Debug.LogWarning(
                            "[DischargeTargetSlotQueue] 跳过了一个空目标箱位。");
                        continue;
                    }

                    TargetQueue.Enqueue(target);
                    loadedCount++;
                }

                Debug.Log(
                    $"[DischargeTargetSlotQueue] 已加载 {loadedCount} 个卸船目标箱位。");
            }
        }

        /// <summary>
        /// 加载单个目标箱位。
        /// </summary>
        public static void Enqueue(DischargeTargetSlot target)
        {
            if (target == null)
            {
                Debug.LogWarning(
                    "[DischargeTargetSlotQueue] 无法加入空目标箱位。");
                return;
            }

            lock (SyncRoot)
            {
                TargetQueue.Enqueue(target);
            }

            Debug.Log(
                $"[DischargeTargetSlotQueue] 加入目标箱位：{target}");
        }

        /// <summary>
        /// 尝试取得下一个可以安全使用的40英尺目标箱位。
        ///
        /// 如果队首箱位非法、已被占用、已经被其他任务预订，
        /// 或者上层缺少支撑，会跳过该箱位并继续检查后面的目标。
        ///
        /// 成功返回目标时，会立即预订这个40英尺箱实际占用的
        /// 两个20英尺箱位，防止后续任务与它发生重叠。
        /// </summary>
        /// <summary>
        /// 保留原调用方式。
        /// 如果没有指定箱区，就使用目标原来的BlockName。
        /// </summary>
        public static bool TryGetNextAvailableTarget(
            out DischargeTargetSlot target)
        {
            return TryGetNextAvailableTarget(
                null,
                out target);
        }

        /// <summary>
        /// 获取下一个可用目标箱位。
        ///
        /// requiredBlockName由ScriptSTS传入：
        /// 1010 -> A35
        /// 1011 -> A36
        /// 1012 -> A37
        /// 1013 -> A40
        /// </summary>
        public static bool TryGetNextAvailableTarget(
            string requiredBlockName,
            out DischargeTargetSlot target)
        {
            target = null;

            lock (SyncRoot)
            {
                while (TargetQueue.Count > 0)
                {
                    DischargeTargetSlot candidate =
                        TargetQueue.Dequeue();

                    if (candidate == null)
                    {
                        Debug.LogWarning(
                            "[DischargeTargetSlotQueue] " +
                            "目标箱位为空，已跳过。");

                        continue;
                    }

                    /*
                     * 关键修改：
                     *
                     * 不再使用算法适配阶段固定写入的A40，
                     * 而是使用当前ScriptSTS对应的实际箱区。
                     */
                    if (!string.IsNullOrWhiteSpace(
                            requiredBlockName))
                    {
                        candidate.BlockName =
                            requiredBlockName
                                .Trim()
                                .ToUpperInvariant();
                    }

                    if (!TryValidateBasic(
                            candidate,
                            out string validationError))
                    {
                        Debug.LogWarning(
                            "[DischargeTargetSlotQueue] " +
                            "跳过非法目标：" +
                            candidate +
                            "，原因：" +
                            validationError);

                        continue;
                    }

                    if (!FortyFootYardSlotAllocator
                            .TryGetFootprint(
                                candidate.BlockName,
                                candidate.Bay,
                                candidate.Column,
                                candidate.Tier,
                                out FortyFootSlotFootprint footprint,
                                out string footprintError))
                    {
                        Debug.LogWarning(
                            "[DischargeTargetSlotQueue] " +
                            "无法解析40英尺占位：" +
                            candidate +
                            "，原因：" +
                            footprintError);

                        continue;
                    }

                    if (IsFootprintReserved(footprint))
                    {
                        Debug.LogWarning(
                            "[DischargeTargetSlotQueue] " +
                            "目标箱位已经被其他任务预订：" +
                            candidate +
                            "，实际占位：" +
                            footprint);

                        continue;
                    }

                    if (IsOccupied(
                            footprint,
                            out string occupiedCode))
                    {
                        Debug.LogWarning(
                            "[DischargeTargetSlotQueue] " +
                            "目标箱位已经被占用：" +
                            candidate +
                            "，被占用位置：" +
                            occupiedCode);

                        continue;
                    }

                    if (!HasSupport(
                            candidate,
                            out string supportError))
                    {
                        Debug.LogWarning(
                            "[DischargeTargetSlotQueue] " +
                            "目标箱位缺少下层支撑：" +
                            candidate +
                            "，原因：" +
                            supportError);

                        continue;
                    }

                    ReserveFootprint(footprint);

                    target = candidate;

                    Debug.Log(
                        "[DischargeTargetSlotQueue] " +
                        "取得目标箱位：" +
                        candidate +
                        "，实际预订位置：" +
                        footprint);

                    return true;
                }
            }

            Debug.LogWarning(
                "[DischargeTargetSlotQueue] " +
                "没有可用的卸船目标箱位。");

            return false;
        }

        /// <summary>
        /// 兼容原有调用方式。
        /// </summary>
        public static bool TryDequeue(out DischargeTargetSlot target)
        {
            return TryGetNextAvailableTarget(out target);
        }

        /// <summary>
        /// 当某个任务取消，并且箱子还没有实际落箱时，
        /// 可以释放此前预订的40英尺占用范围。
        /// </summary>
        public static bool ReleaseReservation(DischargeTargetSlot target)
        {
            if (target == null)
            {
                return false;
            }

            if (!FortyFootYardSlotAllocator.TryGetFootprint(
                    target.BlockName,
                    target.Bay,
                    target.Column,
                    target.Tier,
                    out FortyFootSlotFootprint footprint,
                    out string error))
            {
                Debug.LogWarning(
                    $"[DischargeTargetSlotQueue] 释放预订失败：{error}");
                return false;
            }

            lock (SyncRoot)
            {
                bool removedFirst =
                    ReservedFootprints.Remove(footprint.FirstSlotCode);

                bool removedSecond =
                    ReservedFootprints.Remove(footprint.SecondSlotCode);

                if (removedFirst || removedSecond)
                {
                    Debug.Log(
                        $"[DischargeTargetSlotQueue] 已释放箱位预订：{footprint}");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 判断指定目标是否已经被当前任务队列预订。
        /// </summary>
        public static bool IsReserved(DischargeTargetSlot target)
        {
            if (target == null)
            {
                return false;
            }

            if (!FortyFootYardSlotAllocator.TryGetFootprint(
                    target.BlockName,
                    target.Bay,
                    target.Column,
                    target.Tier,
                    out FortyFootSlotFootprint footprint,
                    out _))
            {
                return false;
            }

            lock (SyncRoot)
            {
                return IsFootprintReserved(footprint);
            }
        }

        /// <summary>
        /// 对目标箱位进行基本格式检查。
        /// </summary>
        private static bool TryValidateBasic(
            DischargeTargetSlot target,
            out string error)
        {
            error = null;

            if (target == null)
            {
                error = "目标箱位为空。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(target.BlockName))
            {
                error = "箱区名称为空。";
                return false;
            }

            target.BlockName = target.BlockName.Trim().ToUpperInvariant();

            if (BlockManager.Instance == null)
            {
                error =
                    "BlockManager.Instance为空，" +
                    "无法检查目标箱区。";

                return false;
            }

            BlockBuffer targetBlock =
                BlockManager.Instance.FindBlockBuffer(
                    target.BlockName);

            if (targetBlock == null)
            {
                error =
                    "当前场景中不存在目标箱区：" +
                    target.BlockName;

                return false;
            }

            if (!FortyFootYardSlotAllocator.IsValidCenterBay(target.Bay))
            {
                error =
                    $"Bay={target.Bay} 不是合法的40英尺中心贝位。" +
                    "合法值应为2、6、10、14……";
                return false;
            }

            if (target.Column <= 0)
            {
                error = $"Column={target.Column} 无效，列号必须大于0。";
                return false;
            }

            if (target.Tier <= 0 || target.Tier > MaxTier)
            {
                error =
                    $"Tier={target.Tier} 无效，允许的层号范围为1到{MaxTier}。";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 检查两个实际箱位中是否至少有一个已经被预订。
        /// </summary>
        private static bool IsFootprintReserved(
            FortyFootSlotFootprint footprint)
        {
            return ReservedFootprints.Contains(footprint.FirstSlotCode) ||
                   ReservedFootprints.Contains(footprint.SecondSlotCode);
        }

        /// <summary>
        /// 检查40英尺箱占用的两个实际箱位是否有箱子。
        /// </summary>
        private static bool IsOccupied(
            FortyFootSlotFootprint footprint,
            out string occupiedCode)
        {
            occupiedCode = null;

            BlockManager blockManager = BlockManager.Instance;

            if (blockManager == null)
            {
                occupiedCode = "BlockManager.Instance为空";
                return true;
            }

            if (blockManager.FindBlockBuffer(footprint.FirstSlotCode) != null)
            {
                occupiedCode = footprint.FirstSlotCode;
                return true;
            }

            if (blockManager.FindBlockBuffer(footprint.SecondSlotCode) != null)
            {
                occupiedCode = footprint.SecondSlotCode;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 检查目标层是否具有完整支撑。
        ///
        /// 第一层不需要支撑。
        /// 第二层及以上要求下方两个实际箱位都已经存在箱子，
        /// 或者已经被本批次更早的任务预订。
        /// </summary>
        private static bool HasSupport(
            DischargeTargetSlot target,
            out string error)
        {
            error = null;

            if (target.Tier <= 1)
            {
                return true;
            }

            int supportTier = target.Tier - 1;

            if (!FortyFootYardSlotAllocator.TryGetFootprint(
                    target.BlockName,
                    target.Bay,
                    target.Column,
                    supportTier,
                    out FortyFootSlotFootprint supportFootprint,
                    out string footprintError))
            {
                error = $"无法计算下层支撑箱位：{footprintError}";
                return false;
            }

            BlockManager blockManager = BlockManager.Instance;

            if (blockManager == null)
            {
                error = "BlockManager.Instance为空，无法检查下层支撑。";
                return false;
            }

            bool firstSupported =
                blockManager.FindBlockBuffer(
                    supportFootprint.FirstSlotCode) != null ||
                ReservedFootprints.Contains(
                    supportFootprint.FirstSlotCode);

            bool secondSupported =
                blockManager.FindBlockBuffer(
                    supportFootprint.SecondSlotCode) != null ||
                ReservedFootprints.Contains(
                    supportFootprint.SecondSlotCode);

            if (!firstSupported || !secondSupported)
            {
                error =
                    $"下层支撑不完整。需要：" +
                    $"{supportFootprint.FirstSlotCode} 和 " +
                    $"{supportFootprint.SecondSlotCode}；" +
                    $"当前支撑状态：first={firstSupported}，" +
                    $"second={secondSupported}。";

                return false;
            }

            return true;
        }

        /// <summary>
        /// 预订40英尺箱占用的两个实际箱位。
        /// </summary>
        private static void ReserveFootprint(
            FortyFootSlotFootprint footprint)
        {
            ReservedFootprints.Add(footprint.FirstSlotCode);
            ReservedFootprints.Add(footprint.SecondSlotCode);
        }
    }
}