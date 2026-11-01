using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using Vtp.VR.Integration;
using ZPMC.VR.Unity;

public class UMP_LevelInfo : MonoBehaviour
{
    public string VesselFreight;
    public string ScriptMethod;
    [Space(7)]
    public string EventAnimation = "LevelEnter";
    [SerializeField]
    private string LevelName = null;
    public bool hasSubtitle = false;
    [SerializeField]
    private DubControl dubbing = null;
    [SerializeField]
    private SceneDecisionWorkspaceController decisionWorkspace;

    private bool _deferScriptStart;
    private bool _scriptStarted;

    private class PendingDischargeGroup
    {
        public int StsId;

        public List<CellCode> SourceList;
    }

    /*public void OpenLevel()
    {
        SceneManager.LoadScene(LevelName);
        UMP_Manager.Instance.UserLoadingScene(this);
    }*/
    public void OpenLevel()
    {
        _deferScriptStart = false;
        _scriptStarted = false;

        SceneManager.LoadScene(LevelName);
        UMP_Manager.Instance.UserLoadingScene(this);
    }

    /// <summary>
    /// 进入三维场景并初始化设备，但暂时不生成任务。
    /// </summary>
    public void OpenLevelPreview()
    {
        _deferScriptStart = true;
        _scriptStarted = false;

        SceneDecisionWorkspaceController workspace =
            GetDecisionWorkspace();

        if (workspace != null)
        {
            workspace.PrepareForSceneLoad(this);
        }

        SceneManager.LoadScene(LevelName);
        UMP_Manager.Instance.UserLoadingScene(this);
    }

    /// <summary>
    /// 算法计算成功后才调用该方法。
    /// </summary>
    public void StartDeferredScript()
    {
        if (_scriptStarted)
        {
            Debug.LogWarning(
                "[UMP_LevelInfo] 实验脚本已经启动，" +
                "忽略本次重复调用。");
            return;
        }

        StartScriptOnce();
    }

    private void StartScriptOnce()
    {
        if (_scriptStarted)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ScriptMethod))
        {
            Debug.LogError(
                "[UMP_LevelInfo] ScriptMethod为空，" +
                "无法启动实验流程。");
            return;
        }

        _scriptStarted = true;
        _deferScriptStart = false;

        Invoke(ScriptMethod, 0f);
    }

    private SceneDecisionWorkspaceController
        GetDecisionWorkspace()
    {
        if (decisionWorkspace == null)
        {
            decisionWorkspace =
                FindObjectOfType<
                    SceneDecisionWorkspaceController>(true);
        }

        return decisionWorkspace;
    }

    public void EventMouse(bool Forward = true)
    {
        Animation a = this.GetComponent<Animation>();
        if (Forward)
        {
            a[EventAnimation].time = 0.0f;
            a[EventAnimation].speed = 1.0f;
            a.CrossFade(EventAnimation);
        }
        else
        {
            if (a[EventAnimation].time == 0.0f)
            {
                a[EventAnimation].time = a[EventAnimation].length;
            }
            a[EventAnimation].speed = -1.0f;
            a.CrossFade(EventAnimation);
        }
    }

    /*public IEnumerator UserLoadedScene()
    {
        yield return null;
        SettingsLoader.fileNameVesselFreight = this.VesselFreight;
        LoadOnBegin();
        DriverSetting setting = (DriverSetting)FileHelper.DeserializeObject(
            FileHelper.GetDataPath() + "/" + "DriverSetting.xml", typeof(DriverSetting));
        setting.BuildDriver(Simulator.Instance);
        DubControl.UniqueInstance = this.dubbing;
        Invoke(ScriptMethod, 0);
    }*/
    public IEnumerator UserLoadedScene()
    {
        yield return null;

        SettingsLoader.fileNameVesselFreight =
            this.VesselFreight;

        LoadOnBegin();

        DriverSetting setting =
            (DriverSetting)FileHelper.DeserializeObject(
                FileHelper.GetDataPath() +
                "/" +
                "DriverSetting.xml",
                typeof(DriverSetting));

        setting.BuildDriver(Simulator.Instance);

        DubControl.UniqueInstance = this.dubbing;

        if (_deferScriptStart)
        {
            // 再等一帧，让箱区、岸桥和摄像机完成初始化。
            yield return null;

            SceneDecisionWorkspaceController workspace =
                GetDecisionWorkspace();

            if (workspace != null)
            {
                PrepareDischargePreviewCamera();
                workspace.Enter3DIdle(this);
            }
            else
            {
                Debug.LogError(
                    "[UMP_LevelInfo] 找不到三维决策控制器。");
            }

            // 此处故意不调用 DischargeShipScript。
            yield break;
        }

        StartScriptOnce();
    }

    /// <summary>
    /// 预览模式仍使用原卸船流程进入时的默认镜头，
    /// 但不创建任务、不启动卸船作业。
    /// </summary>
    private static void PrepareDischargePreviewCamera()
    {
        if (CameraManager.Instance == null)
        {
            return;
        }

        CameraManager.Instance.DefaultMainPosition =
            new Vector3(1137f, 52f, -103f);
        CameraManager.Instance.DefaultMainEulerAngles =
            new Vector3(21f, 62f, 0f);
        CameraManager.Instance.DefaultSubPosition =
            new Vector3(1200.5f, 25.3f, -247f);
        CameraManager.Instance.DefaultSubEulerAngles =
            new Vector3(29f, 23f, 0f);
        CameraManager.Instance.DefaultPersonPosition =
            new Vector3(1180f, 1.6f, -20f);

        CameraManager.Instance.OnBeginDemo();
    }

    public static void LoadOnBegin()
    {
        SettingsLoader.Instance().LoadingSettings();
        SettingsLoader.Instance().InitialSettings();
        EventScripter.GetGlobalInstance().recordMarshal.Clear();
    }

    private IEnumerator
    PrepareRestTargetsThenDischarge()
    {
        // ==================================================
        // 1. REST 客户端存在性
        // ==================================================

        if (RestDischargeTargetClient.Instance == null)
        {
            Debug.LogError(
                "[Discharge] "
                + "RestDischargeTargetClient 未初始化");

            yield break;
        }


        bool success = false;

        string error = null;

        int receivedCount = 0;


        // ==================================================
        // 2. RESTful 获取外部目标箱位
        // ==================================================

        yield return StartCoroutine(
            RestDischargeTargetClient.Instance
                .RefreshDischargeTargets(
                    delegate (
                        int count)
                    {
                        success = true;

                        receivedCount = count;
                    },
                    delegate (
                        string message)
                    {
                        success = false;

                        error = message;
                    }));


        // ==================================================
        // 3. 请求失败
        // ==================================================

        if (!success)
        {
            Debug.LogError(
                "[Discharge] "
                + "获取外部目标箱位失败: "
                + error);

            yield break;
        }


        Debug.Log(
            "[Discharge] REST接收目标数量: "
            + receivedCount);


        // ==================================================
        // 4. 缓存原卸船作业
        //
        // 作业内容仍然来自：
        //
        // InfoSTS.DischargeMarshal
        //
        // 完全不变
        // ==================================================

        List<PendingDischargeGroup>
            pendingGroups =
                new List<PendingDischargeGroup>();


        int requiredCount = 0;


        foreach (
            var item
            in InfoSTS.DischargeMarshal)
        {
            List<CellCode> sourceList =
                item.Value.BuildCode();


            pendingGroups.Add(
                new PendingDischargeGroup
                {
                    StsId =
                        item.Key,

                    SourceList =
                        sourceList
                });


            requiredCount +=
                sourceList.Count;
        }


        // ==================================================
        // 5. 严格检查目标数量
        // ==================================================

        if (receivedCount != requiredCount)
        {
            Debug.LogError(
                string.Format(
                    "[Discharge] 算例数量不一致。"
                    + "原卸船任务={0}, "
                    + "REST目标箱位={1}",
                    requiredCount,
                    receivedCount));

            yield break;
        }


        // ==================================================
        // 6. 开始原来的卸船脚本生成
        //
        // ScriptSTS 自己从 Queue 依次获取目标
        // ==================================================

        foreach (
            PendingDischargeGroup group
            in pendingGroups)
        {
            ScriptSTS sts =
                new ScriptSTS(
                    group.StsId);


            sts.WriteDischargeScript(
                group.SourceList);
        }


        // ==================================================
        // 7. 原任务列表
        // ==================================================

        ScriptSTS.ImportTask();


        Debug.Log(
            "[Discharge] "
            + "已使用REST目标箱位生成卸船作业");
    }

    // ============ 各个流程脚本 ============

    public void DischargeShipScript()
    {
        CameraStayLogger.EnterFlow("DischargeShip");

        CameraManager.Instance.DefaultMainPosition = new Vector3(1137f, 52f, -103f);
        CameraManager.Instance.DefaultMainEulerAngles = new Vector3(21f, 62f, 0);
        CameraManager.Instance.DefaultSubPosition = new Vector3(1200.5f, 25.3f, -247f);
        CameraManager.Instance.DefaultSubEulerAngles = new Vector3(29f, 23f, 0);
        CameraManager.Instance.DefaultPersonPosition = new Vector3(1180f, 1.6f, -20f);
        CameraManager.Instance.OnBeginDemo();

        EventScripter scripter = EventScripter.GetGlobalInstance();
        scripter.Initialize();

        //StartCoroutine(PrepareRestTargetsThenDischarge());
        if(SceneExampleScheduleRuntime.HasSchedule)
        {
            ExecuteAutomaticDischargePlan();
            return;
        }
        //修改   txt读取
        List<DischargeTargetSlot> targetSlots;

        {
            // 没有远程算法结果时保留原来的 txt 测试回退。
            string txtPath =
                System.IO.Path.Combine(
                    Application.streamingAssetsPath,
                    "TargetInput.txt");
            List<string> targetBoxNumbers =
                DischargeTargetTxtReader.ReadBoxNumbers(
                    txtPath);
            targetSlots =
                new List<DischargeTargetSlot>();

            foreach (string boxNumber in targetBoxNumbers)
            {
                DischargeTargetSlot slot;
                string parseError;
                if (!DischargeTargetSlotParser.TryParse(
                        boxNumber,
                        out slot,
                        out parseError))
                {
                    Debug.LogError(
                        string.Format(
                            "目标箱位 {0} 解析失败：{1}",
                            boxNumber,
                            parseError));
                    continue;
                }

                targetSlots.Add(slot);
            }
        }

        if (targetSlots.Count == 0)
        {
            Debug.LogError("没有成功加载任何目标箱位，停止生成卸船任务。");
            return;
        }

        // 这里只生成原动画需要的任务序列，不修改ScriptSTS及设备运动。
        // 先按算法目标箱区拆分任务，再交给对应的原岸桥脚本执行。
        int[] stsOrder = { 1010, 1011, 1012, 1013 };
        Dictionary<int, List<CellCode>> sourceLists =
            new Dictionary<int, List<CellCode>>();
        int preparedTaskCount = 0;

        // 先校验每个岸桥的源箱数量，避免生成到一半才发现不足。
        for (int index = 0; index < stsOrder.Length; index++)
        {
            int stsId = stsOrder[index];
            string blockName = ResolveDischargeBlockForSts(stsId);
            List<DischargeTargetSlot> blockTargets =
                GetTargetsForBlock(targetSlots, blockName);

            if (blockTargets.Count == 0)
            {
                continue;
            }

            InfoSTS stsInfo;
            if (!InfoSTS.DischargeMarshal.TryGetValue(stsId, out stsInfo) ||
                stsInfo == null)
            {
                Debug.LogError(
                    "[Discharge] 找不到箱区" +
                    blockName +
                    "对应的岸桥" +
                    stsId +
                    "。未启动动画。");
                return;
            }

            List<CellCode> selectedSources = stsInfo.BuildCode();
            int selectedCount =
                selectedSources == null ? 0 : selectedSources.Count;
            if (selectedCount < blockTargets.Count)
            {
                Debug.LogError(
                    "[Discharge] 岸桥" +
                    stsId +
                    "源箱不足：箱区=" +
                    blockName +
                    "，算法任务=" +
                    blockTargets.Count +
                    "，已选源箱=" +
                    selectedCount +
                    "。未启动动画。");
                return;
            }

            if (selectedSources.Count > blockTargets.Count)
            {
                selectedSources = selectedSources.GetRange(
                    0,
                    blockTargets.Count);
            }

            sourceLists.Add(stsId, selectedSources);
            preparedTaskCount += blockTargets.Count;
        }

        if (preparedTaskCount != targetSlots.Count)
        {
            Debug.LogError(
                "[Discharge] 存在无法映射到岸桥的算法任务：算法任务=" +
                targetSlots.Count +
                "，已映射=" +
                preparedTaskCount +
                "。未启动动画。");
            return;
        }

        // 沿用原来的ScriptSTS运动逻辑。每次只向原目标队列放入
        // 当前岸桥对应箱区的任务，防止第一个岸桥取走其他箱区任务。
        for (int index = 0; index < stsOrder.Length; index++)
        {
            int stsId = stsOrder[index];
            List<CellCode> sourceList;
            if (!sourceLists.TryGetValue(stsId, out sourceList))
            {
                continue;
            }

            string blockName = ResolveDischargeBlockForSts(stsId);
            List<DischargeTargetSlot> blockTargets =
                GetTargetsForBlock(targetSlots, blockName);

            DischargeTargetSlotQueue.LoadTargets(blockTargets);

            ScriptSTS sts = new ScriptSTS(stsId);
            sts.WriteDischargeScript(sourceList);

            Debug.Log(
                "[Discharge] 已生成岸桥任务序列：岸桥=" +
                stsId +
                "，箱区=" +
                blockName +
                "，任务数=" +
                blockTargets.Count);
        }

        Debug.Log(
            "[Discharge] 已按算法箱区生成原动画任务序列，总数=" +
            preparedTaskCount +
            "。设备运动逻辑保持不变。");

        ScriptSTS.ImportTask();
    }

    private static void ExecuteAutomaticDischargePlan()
    {
        var plan = AutomaticQuayCraneScheduler.Current;
        if (plan == null || plan.Count == 0)
            throw new InvalidOperationException("缺少岸桥自动分配结果，不能启动卸船动画。");
        // 先统一校验，防止构建动画到一半才发现源箱消失。
        foreach (var task in plan)
        {
            var vessel = VesselManager.GlobalInstance().FindVesselBuffer(task.Source.NestName);
            if (vessel == null || vessel.FindContainer(task.Source.Bay, task.Source.Column, task.Source.Tier) == null)
                throw new InvalidOperationException("岸桥自动分配的源箱不存在：" + task.Operation.jobId);
        }
        var scripts = new Dictionary<int, ScriptSTS>();
        var counts = new SortedDictionary<int,int>();
        foreach (var task in plan)
        {
            int id = task.Operation.quayCraneId;
            if (!scripts.ContainsKey(id)) {scripts[id]=new ScriptSTS(id);counts[id]=0;}
            // 沿算法顺序构建动画，并精确消费本任务预分配的目标，不重新映射或跳过。
            scripts[id].WriteDischargeScript(new List<CellCode>{task.Source},false,true,
                new List<DischargeTargetSlot>{task.Target});
            counts[id]++;
        }
        foreach(var pair in counts)Debug.Log("[Discharge] 自动岸桥="+pair.Key+"，任务数="+pair.Value);
        ScriptSTS.ImportTask();
    }

    private static string ResolveDischargeBlockForSts(int stsId)
    {
        switch (stsId)
        {
            case 1010: return "A35";
            case 1011: return "A36";
            case 1012: return "A37";
            case 1013: return "A40";
            default: return string.Empty;
        }
    }

    private static List<DischargeTargetSlot> GetTargetsForBlock(
        IList<DischargeTargetSlot> targets,
        string blockName)
    {
        List<DischargeTargetSlot> result =
            new List<DischargeTargetSlot>();

        if (targets == null || string.IsNullOrWhiteSpace(blockName))
        {
            return result;
        }

        for (int index = 0; index < targets.Count; index++)
        {
            DischargeTargetSlot target = targets[index];
            if (target != null &&
                string.Equals(
                    target.BlockName,
                    blockName,
                    StringComparison.OrdinalIgnoreCase))
            {
                result.Add(target);
            }
        }

        return result;
    }

    public void DischargeShipLearn()
    {
        CameraStayLogger.EnterFlow("DischargeShip_Learn");

        CameraManager.Instance.DefaultMainPosition = new Vector3(1137f, 52f, -103f);
        CameraManager.Instance.DefaultMainEulerAngles = new Vector3(21f, 62f, 0);
        CameraManager.Instance.DefaultSubPosition = new Vector3(1200.5f, 25.3f, -247f);
        CameraManager.Instance.DefaultSubEulerAngles = new Vector3(29f, 23f, 0);
        CameraManager.Instance.DefaultPersonPosition = new Vector3(1180f, 1.6f, -20f);
        CameraManager.Instance.OnBeginDemo();

        EventScripter scripter = EventScripter.GetGlobalInstance();
        scripter.Initialize();

        int[] column = new int[13] { 11, 9, 7, 5, 3, 1, 0, 2, 4, 6, 8, 10, 12 };
        List<CellCode> list = new List<CellCode>(column.Length);
        for (int i = 0; i < column.Length; i++)
        {
            list.Add(new CellCode() { NestName = "Vessel004", Bay = 2, Column = column[i], Tier = 86 });
        }
        ScriptSTS sts = new ScriptSTS(1010);
        sts.WriteDischargeScript(list, true);
    }

    public void LoadShipScript()
    {
        CameraStayLogger.EnterFlow("LoadShip");

        CameraManager.Instance.DefaultMainPosition = new Vector3(1137.059f, 52.2803f, -103.0995f);
        CameraManager.Instance.DefaultMainEulerAngles = new Vector3(20.842f, 61.777f, 0);
        CameraManager.Instance.DefaultSubPosition = new Vector3(1200.478f, 25.2994f, -246.9374f);
        CameraManager.Instance.DefaultSubEulerAngles = new Vector3(29.282f, 22.723f, 0);
        CameraManager.Instance.DefaultPersonPosition = new Vector3(1180f, 1.6f, -20f);
        CameraManager.Instance.OnBeginDemo();

        EventScripter scripter = EventScripter.GetGlobalInstance();
        scripter.Initialize();

        foreach (var item in InfoSTS.LoadMarshal)
        {
            List<CellCode> list = item.Value.BuildCode();
            ScriptSTS sts = new ScriptSTS(item.Key);
            sts.WriteLoadScript(list);
        }
        ScriptSTS.ImportTask();
    }

    public void LoadShipLearn()
    {
        CameraStayLogger.EnterFlow("LoadShipLearn");

        CameraManager.Instance.DefaultMainPosition = new Vector3(1137.059f, 52.2803f, -103.0995f);
        CameraManager.Instance.DefaultMainEulerAngles = new Vector3(20.842f, 61.777f, 0);
        CameraManager.Instance.DefaultSubPosition = new Vector3(1200.478f, 25.2994f, -246.9374f);
        CameraManager.Instance.DefaultSubEulerAngles = new Vector3(29.282f, 22.723f, 0);
        CameraManager.Instance.DefaultPersonPosition = new Vector3(1180f, 1.6f, -20f);
        CameraManager.Instance.OnBeginDemo();

        EventScripter scripter = EventScripter.GetGlobalInstance();
        scripter.Initialize();
        ScriptSTS sts = new ScriptSTS(1010);
        List<CellCode> list = sts.BuildLoadList("A35", 26, 20);
        sts.WriteLoadScript(list, true);
    }

    public void AscDeliverLearn()
    {
        CameraStayLogger.EnterFlow("AscDeliverLearn");

        CameraManager.Instance.DefaultMainPosition = new Vector3(1309.775f, 51.12323f, -553.6481f);
        CameraManager.Instance.DefaultMainEulerAngles = new Vector3(26.247f, -37.367f, 0);
        CameraManager.Instance.DefaultSubPosition = new Vector3(2063.974f, 11.97674f, -475.8177f);
        CameraManager.Instance.DefaultSubEulerAngles = new Vector3(20.1f, 93.57301f, 0);
        CameraManager.Instance.DefaultPersonPosition = new Vector3(1210f, 1.6f, -520f);
        CameraManager.Instance.OnBeginDemo();

        EventScripter scripter = EventScripter.GetGlobalInstance();
        scripter.Initialize();
        ScriptTruck script = new ScriptTruck(2072);
        script.WriteDipatchScript(script.BuildCellDescend("A37", 78, 10), true);
    }

    public void AscDipatchScript()
    {
        CameraStayLogger.EnterFlow("AscDeliver");

        CameraManager.Instance.DefaultMainPosition = new Vector3(1309.775f, 51.12323f, -553.6481f);
        CameraManager.Instance.DefaultMainEulerAngles = new Vector3(26.247f, -37.367f, 0);
        CameraManager.Instance.DefaultSubPosition = new Vector3(2063.974f, 11.97674f, -475.8177f);
        CameraManager.Instance.DefaultSubEulerAngles = new Vector3(20.1f, 93.57301f, 0);
        CameraManager.Instance.DefaultPersonPosition = new Vector3(1210f, 1.6f, -520f);
        CameraManager.Instance.OnBeginDemo();

        InfoTruck ins = InfoTruck.Instance;
        List<CellCode> list = ins.BuildCode();
        ScriptTruck script = new ScriptTruck(ins.IndxASC);
        script.WriteDipatchScript(list);
        ScriptTruck.ImportTask();
    }

    public void AscCollectLearn()
    {
        CameraStayLogger.EnterFlow("AscCollectLearn");

        CameraManager.Instance.DefaultMainPosition = new Vector3(1309.775f, 51.12323f, -553.6481f);
        CameraManager.Instance.DefaultMainEulerAngles = new Vector3(26.247f, -37.367f, 0);
        CameraManager.Instance.DefaultSubPosition = new Vector3(-254.0632f, 11.89093f, -346.4141f);
        CameraManager.Instance.DefaultSubEulerAngles = new Vector3(22.393f, -63.477f, 0);
        CameraManager.Instance.DefaultPersonPosition = new Vector3(1210f, 1.6f, -520f);
        CameraManager.Instance.OnBeginDemo();

        EventScripter scripter = EventScripter.GetGlobalInstance();
        scripter.Initialize();
        ScriptTruck script = new ScriptTruck(2072);
        script.WriteCollectScript(script.BuildCellAscend("A37", 70, 10), true);
    }

    public void AscCollectScript()
    {
        CameraStayLogger.EnterFlow("AscCollect");

        CameraManager.Instance.DefaultMainPosition = new Vector3(1309.775f, 51.12323f, -553.6481f);
        CameraManager.Instance.DefaultMainEulerAngles = new Vector3(26.247f, -37.367f, 0);
        CameraManager.Instance.DefaultSubPosition = new Vector3(-254.0632f, 11.89093f, -346.4141f);
        CameraManager.Instance.DefaultSubEulerAngles = new Vector3(22.393f, -63.477f, 0);
        CameraManager.Instance.DefaultPersonPosition = new Vector3(1210f, 1.6f, -520f);
        CameraManager.Instance.OnBeginDemo();

        UserDriver.Instance.CollectingContainer.Clear();
        InfoTruck ins = InfoTruck.Instance;
        List<CellCode> list = ins.BuildCode();
        ScriptTruck script = new ScriptTruck(ins.IndxASC);
        script.WriteCollectScript(list);
        ScriptTruck.ImportTask();
    }

    public void AscReOrderLearn()
    {
        CameraStayLogger.EnterFlow("AscReOrderLearn");

        CameraManager.Instance.DefaultMainPosition = new Vector3(1210f, 10f, -496f);
        CameraManager.Instance.DefaultMainEulerAngles = new Vector3(0f, 20f, 0);
        CameraManager.Instance.DefaultSubPosition = new Vector3(1215.107f, 43.89703f, -171.6648f);
        CameraManager.Instance.DefaultSubEulerAngles = new Vector3(35.124f, -178.813f, 0);
        CameraManager.Instance.DefaultPersonPosition = new Vector3(1210f, 1.6f, -520f);
        CameraManager.Instance.OnBeginDemo();

        EventScripter scripter = EventScripter.GetGlobalInstance();
        scripter.Initialize();
        ScriptTruck script = new ScriptTruck(2072);
        script.WriteReorderScript(script.BuildCellDescend("A37", 78, 10), true);
    }

    public void AscReorder()
    {
        CameraStayLogger.EnterFlow("AscReorder");

        CameraManager.Instance.DefaultMainPosition = new Vector3(1210f, 10f, -496f);
        CameraManager.Instance.DefaultMainEulerAngles = new Vector3(0f, 20f, 0);
        CameraManager.Instance.DefaultSubPosition = new Vector3(1215.107f, 43.89703f, -171.6648f);
        CameraManager.Instance.DefaultSubEulerAngles = new Vector3(35.124f, -178.813f, 0);
        CameraManager.Instance.DefaultPersonPosition = new Vector3(1210f, 1.6f, -520f);
        CameraManager.Instance.OnBeginDemo();

        InfoTruck ins = InfoTruck.Instance;
        List<CellCode> list = ins.BuildCode();
        ScriptTruck script = new ScriptTruck(ins.IndxASC);
        script.WriteReorderScript(list);
        ScriptTruck.ImportTask();
    }
}
