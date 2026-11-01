using System;
using System.Collections.Generic;
using UnityEngine;
using Vtp.VR.Rest;
using ZPMC.VR.Unity;

namespace Vtp.VR.Integration
{
    [Serializable]
    public sealed class QuayAssignmentRecord
    {
        public string jobId;
        public int sequence;
        public int quayCraneId;
        public string yardCraneId;
        public string sourceVessel;
        public int sourceBay, sourceColumn, sourceTier;
        public string targetCode;
    }

    public sealed class AutomaticDischargeTask
    {
        public ScheduledOperationDto Operation;
        public CellCode Source;
        public DischargeTargetSlot Target;
    }

    /// <summary>
    /// 所选RMG求解完成后的岸桥资源分配阶段，恢复原场景的固定箱区绑定。
    /// 独立记录岸桥，不改变EXE的RMG输出和任务序列。
    /// </summary>
    public static class AutomaticQuayCraneScheduler
    {
        public const string Method = "fixed-yard-block/v2";
        public static List<AutomaticDischargeTask> Current { get; private set; }
        public static void Clear() { Current = null; }

        public static int GetFixedQuayCraneId(string block)
        {
            switch (block)
            {
                case "A35": return 1010;
                case "A36": return 1011;
                case "A37": return 1012;
                case "A40": return 1013;
                default: return 0;
            }
        }

        public sealed class Resource
        {
            public int Id;
            public List<CellCode> Sources;
            public Vector3 Position;
            public double AvailableAt;
            public int Used;
        }

        public static bool TryPrepare(SchedulingResultDto schedule, out string error)
        {
            Clear();
            List<DischargeTargetSlot> targets;
            if (!AlgorithmDischargePlan.TryBuildTargets(out targets, out error)) return false;
            var resources = new List<Resource>();
            var ids = new HashSet<int>();
            foreach (var info in UnityEngine.Object.FindObjectsOfType<InfoSTS>(true))
            {
                if (!info.IsDischargeResource || info.IndxSTS < 1010 || info.IndxSTS > 1013 || !ids.Add(info.IndxSTS)) continue;
                var equipment = EquipManager.Instance == null ? null : EquipManager.Instance.FindEquipSTS(info.IndxSTS);
                if (equipment == null || !equipment.gameObject.activeInHierarchy) continue;
                var sources = info.GetAutomaticSourceCodes();
                if (sources.Count > 0) resources.Add(new Resource { Id = info.IndxSTS,
                    Sources = sources, Position = equipment.transform.position });
            }
            resources.Sort((a,b) => a.Id.CompareTo(b.Id));
            var operations = new List<ScheduledOperationDto>();
            foreach (var operation in schedule.operations)
                if (operation.taskType == "卸船") operations.Add(operation);
            operations.Sort((a,b) => a.sequence.CompareTo(b.sequence));
            if (operations.Count != targets.Count) { error = "卸船任务和目标箱位数量不一致。"; return false; }
            var positions = new List<Vector3>();
            foreach (var target in targets)
            {
                var block = BlockManager.Instance == null ? null : BlockManager.Instance.FindBlockBuffer(target.BlockName);
                if (block == null || !ScriptSTS.SupportsDischargeTarget(target.BlockName))
                { error = "目标箱区没有可执行的场桥路线：" + target.BlockName; return false; }
                positions.Add(block.GetWorldPosition(target.Bay, target.Column));
            }
            List<AutomaticDischargeTask> plan;
            if (!TryAssign(operations, targets, positions, resources, out plan, out error)) return false;
            Current = plan;
            foreach (var task in plan)
            {
                task.Operation.sourceVessel = task.Source.NestName;
                task.Operation.sourceShipBay = task.Source.Bay;
                task.Operation.sourceShipColumn = task.Source.Column;
                task.Operation.sourceShipTier = task.Source.Tier;
            }
            return true;
        }

        public static bool TryAssign(IList<ScheduledOperationDto> operations, IList<DischargeTargetSlot> targets,
            IList<Vector3> targetPositions, IList<Resource> resources, out List<AutomaticDischargeTask> plan, out string error)
        {
            plan = new List<AutomaticDischargeTask>();
            error = null;
            if (operations == null || targets == null || targetPositions == null || resources == null ||
                operations.Count != targets.Count || operations.Count != targetPositions.Count)
            { error = "岸桥自动分配的输入数量不一致。"; return false; }
            var usedSources = new HashSet<string>();
            for (int i = 0; i < operations.Count; i++)
            {
                int requiredCrane = GetFixedQuayCraneId(targets[i].BlockName);
                if (requiredCrane == 0)
                { error = "目标箱区未配置固定岸桥：" + targets[i].BlockName; plan.Clear(); return false; }
                Resource best = null;
                double bestFinish = double.MaxValue;
                foreach (var resource in resources)
                {
                    // 每个箱区只使用原来对应的一台岸桥，不能借用第四台分担其他箱区。
                    if (resource.Id != requiredCrane) continue;
                    if (resource.Sources == null || resource.Used >= resource.Sources.Count) continue;
                    if (operations[i].quayCraneId > 0 && operations[i].quayCraneId != resource.Id) continue;
                    double travel = Vector3.Distance(resource.Position, targetPositions[i]) / 5d;
                    double finish = resource.AvailableAt + 60d + travel;
                    if (finish < bestFinish) { best = resource; bestFinish = finish; }
                }
                if (best == null)
                { error = "箱区 " + targets[i].BlockName + " 对应的岸桥 " + requiredCrane + " 不可用、源箱不足或与指定岸桥冲突。任务：" + operations[i].jobId; plan.Clear(); return false; }
                var source = best.Sources[best.Used++];
                string key = source.NestName + "/" + source.Bay + "/" + source.Column + "/" + source.Tier;
                if (!usedSources.Add(key)) { error = "岸桥源箱重复：" + key; plan.Clear(); return false; }
                best.AvailableAt = bestFinish;
                plan.Add(new AutomaticDischargeTask { Operation = operations[i], Source = source, Target = targets[i] });
            }
            foreach (var task in plan)
                foreach (var resource in resources)
                    if (resource.Sources.Contains(task.Source)) { task.Operation.quayCraneId = resource.Id; break; }
            return true;
        }

        public static QuayAssignmentRecord[] BuildRecords()
        {
            if (Current == null) return new QuayAssignmentRecord[0];
            return Current.ConvertAll(t => new QuayAssignmentRecord { jobId=t.Operation.jobId,
                sequence=t.Operation.sequence, quayCraneId=t.Operation.quayCraneId,
                yardCraneId=t.Operation.craneId, sourceVessel=t.Source.NestName,
                sourceBay=t.Source.Bay, sourceColumn=t.Source.Column, sourceTier=t.Source.Tier,
                targetCode=t.Target.ToYardCode() }).ToArray();
        }
    }
}
