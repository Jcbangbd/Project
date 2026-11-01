using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Vtp.VR.Integration;
using Vtp.VR.LocalAlgorithmExe;
using Vtp.VR.Rest;

// 仅隔离工程批处理使用：真实场景、真实算法入口和本地EXE，不登录、不提交成绩、不启动设备推演。
[InitializeOnLoad]
public static class AlgorithmOverlayWorkflowVerification
{
    private const string Pending="AlgorithmOverlayWorkflowVerification.Pending";
    private static IEnumerator _steps;
    private static readonly List<string> Checks=new List<string>();
    private static string Output=>Path.GetFullPath(".codex-tmp/AlgorithmOverlayWorkflow");
    static AlgorithmOverlayWorkflowVerification()
    {
        if(SessionState.GetBool(Pending,false))EditorApplication.playModeStateChanged+=OnPlay;
    }
    public static void Run()
    {
        // 流程已改为参数前置、实验后答题，旧入口复用新的完整闭环验证。
        ExperimentWorkflowVerification.Run();
    }
    private static void OnPlay(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode)return;
        _steps=Workflow();EditorApplication.update+=Tick;
    }
    private static void Tick()
    {
        try
        {
            if(_steps.MoveNext())return;
            File.WriteAllLines(Path.Combine(Output,"checks.txt"),Checks);
            Debug.Log("ALGORITHM_OVERLAY_REAL_WORKFLOW_PASSED "+Checks.Count);Finish(0);
        }
        catch(Exception e)
        {
            Debug.LogException(e);File.WriteAllText(Path.Combine(Output,"failure.txt"),e.ToString());
            File.WriteAllLines(Path.Combine(Output,"checks.txt"),Checks);Finish(1);
        }
    }
    private static void Finish(int code)
    {
        EditorApplication.update-=Tick;SessionState.SetBool(Pending,false);EditorApplication.Exit(code);
    }
    private static IEnumerator Frames(int count=4)
    {int until=Time.frameCount+count;while(Time.frameCount<until)yield return null;}
    private static IEnumerator Workflow()
    {
        for(var wait=Frames();wait.MoveNext();)yield return null;
        var workspace=UnityEngine.Object.FindObjectOfType<SceneDecisionWorkspaceController>(true);
        Check(workspace!=null,"Actual SceneStage1 workspace found");
        var level=UnityEngine.Object.FindObjectsOfType<UMP_LevelInfo>(true).First(x=>x.ScriptMethod=="DischargeShipScript");
        SceneNumericalExampleDto[] all;string error;
        Check(RmgCaseWorkbookBridge.TryReadExamples(Path.Combine(Application.streamingAssetsPath,"RmgAlgorithmEngine/算例数据.xlsx"),"LOCAL_OVERLAY_QA",null,out all,out error),"Bundled local cases readable: "+error);
        var cases=all.Where(x=>{string type;return SceneExampleTaskCaseAdapter.TryInferTaskTypeFromPositions(x,out type)&&type=="卸船";}).ToArray();
        Check(cases.Length==43,"Same 43 discharge cases as the reported workflow");
        StudentAssessmentRuntime.BeginSession("LOCAL_OVERLAY_QA");
        SceneNumericalExampleRuntime.Set("LOCAL_OVERLAY_QA",cases,true,"UI integration test","卸船",all);
        level.OpenLevelPreview();
        float deadline=Time.realtimeSinceStartup+60;
        while(!Get<bool>(workspace,"_entered3D")&&Time.realtimeSinceStartup<deadline)yield return null;
        Check(Get<bool>(workspace,"_entered3D") && CameraManager.Instance!=null,"Original preview transition initialized port scene and devices");
        for(var wait=Frames(6);wait.MoveNext();)yield return null;
        Check(!Resources.FindObjectsOfTypeAll<AlgorithmRunOverlay>().Any(x=>x.gameObject.scene.IsValid()&&x.gameObject.activeInHierarchy),"Idle 3D port is not covered");

        // 回归原故障挂载方式：即便误挂旧Canvas，Begin也必须修正为独立、有效的全屏根Canvas。
        var malformed=UnityEngine.Object.Instantiate(Resources.Load<AlgorithmRunOverlay>("AlgorithmRunOverlay"),workspace.transform,false);
        malformed.transform.localScale=Vector3.zero;
        malformed.GetComponent<RectTransform>().sizeDelta=Vector2.zero;
        malformed.Begin("遗传算法",cases.Length);
        for(var wait=Frames(2);wait.MoveNext();)yield return null;
        ValidateDisplay(malformed,"Legacy-parent zero-transform repair");
        malformed.Finish();UnityEngine.Object.Destroy(malformed.gameObject);
        for(var wait=Frames();wait.MoveNext();)yield return null;

        workspace.OpenAlgorithmSelection();
        for(var wait=Frames();wait.MoveNext();)yield return null;
        var algorithm=Get<AlgorithmAssessmentController>(workspace,"_algorithmAssessmentController");
        Get<Dictionary<string,Image>>(algorithm,"_algorithmImages")["genetic-algorithm-v2"].GetComponent<Button>().onClick.Invoke();
        for(var wait=Frames();wait.MoveNext();)yield return null;
        Check(Get<RectTransform>(algorithm,"_reasonPage").gameObject.activeSelf,"Actual toolbar flow opens selected algorithm reasons");
        Get<Dictionary<string,Image>>(algorithm,"_reasonImages").Values.First().GetComponent<Button>().onClick.Invoke();
        Get<Button>(algorithm,"_primaryButton").onClick.Invoke();
        for(var wait=Frames();wait.MoveNext();)yield return null;
        var inputs=Get<Dictionary<string,TMP_InputField>>(algorithm,"_parameterInputs");
        inputs["POPULATION_SIZE"].text="10";inputs["MAX_GENERATIONS"].text="3";
        inputs["ELITE_COUNT"].text="1";inputs["TOURNAMENT_SIZE"].text="2";
        inputs["STAGNATION_LIMIT"].text="3";inputs["PRINT_EVERY"].text="1";
        Get<Button>(algorithm,"_primaryButton").onClick.Invoke();
        Check(Get<bool>(workspace,"_algorithmRunning"),"Confirming actual parameters enters production run method");
        var overlay=Get<AlgorithmRunOverlay>(workspace,"_runOverlay");
        ValidateDisplay(overlay,"Before EXE preparation",false);
        Check(!algorithm.PanelGroup.interactable && algorithm.PanelGroup.alpha==0,"Parameter panel closes before the running overlay");
        Capture("01-port-preparing",overlay);
        for(var wait=Frames(2);wait.MoveNext();)yield return null;
        ValidateDisplay(overlay,"First rendered production frames");
        var text=new StringBuilder();Dump(overlay.transform,text);Dump(overlay.statusPanel,text);
        File.WriteAllText(Path.Combine(Output,"running-hierarchy.txt"),text.ToString());

        deadline=Time.realtimeSinceStartup+180;bool sawExe=false;float observed=Time.realtimeSinceStartup;
        while(Get<bool>(workspace,"_algorithmRunning")&&Time.realtimeSinceStartup<deadline)
        {
            if(!overlay.IsVisible)throw new InvalidOperationException("真实算法执行期间遮罩不可见。");
            if(RmgExeCoordinator.Instance!=null&&RmgExeCoordinator.Instance.IsRunning&&!sawExe)
            {
                sawExe=true;Capture("02-port-exe-running",overlay);
                Check(overlay.CurrentStage==1,"Real EXE startup advances the visible stage");
                Check(overlay.statusText.text=="已传入43条“卸船”算例，正在执行遗传算法……","Real progress uses actual count, operation type and selected algorithm without EXE wording");
                Check(overlay.algorithmText.text=="遗传算法 · 43 条卸船任务","Overlay subtitle uses the current operation type");
            }
            yield return null;
        }
        Check(sawExe,"Real local GA EXE was launched through the production coordinator");
        Check(!Get<bool>(workspace,"_algorithmRunning"),"Real algorithm completes within the bounded verification window");
        Check(!overlay.gameObject.activeInHierarchy,"Success closes running overlay");
        var result=Get<ScheduleResultAssessmentController>(workspace,"_scheduleResultAssessmentController");
        var status=Get<TMP_Text>(workspace,"statusText");
        Check(result!=null && Get<CanvasGroup>(result,"_group").alpha>0,"Successful schedule opens result review: "+(status==null?"":status.text));
        Check(SceneExampleScheduleRuntime.HasSchedule && SceneExampleScheduleRuntime.Schedule.operations.Length==43,"Actual schedule contains all 43 operations");
        var quayPlan=AutomaticQuayCraneScheduler.Current;
        Check(quayPlan!=null&&quayPlan.All(t=>t.Operation.quayCraneId==AutomaticQuayCraneScheduler.GetFixedQuayCraneId(t.Target.BlockName)),"Every real operation respects the original fixed yard-block quay binding");
        Check(quayPlan.All(t=>t.Target.BlockName!="A40"),"Bundled discharge case uses only the original three yard blocks");
        Check(quayPlan.All(t=>t.Operation.quayCraneId!=1013),"Fourth quay crane receives no jobs when there is no A40 target");
        File.WriteAllLines(Path.Combine(Output,"fixed-quay-assignments.csv"),new[]{"sequence,jobId,targetBlock,quayCraneId"}.Concat(quayPlan.Select(t=>t.Operation.sequence+","+t.Operation.jobId+","+t.Target.BlockName+","+t.Operation.quayCraneId)));
        Capture("03-port-result-review",null);
        Check(Time.realtimeSinceStartup>observed,"Visibility monitored while the external process was running");

        // 用真实参数校验错误走原失败/重试入口，不启动第二次EXE，不提交或运行设备动画。
        var selection=Get<AlgorithmAssessmentSelection>(workspace,"_pendingAlgorithmSelection");selection.Parameters["POPULATION_SIZE"]=1;
        workspace.RunSelectedAlgorithm();ValidateDisplay(overlay,"Failure retry attempt starts visible",false);
        deadline=Time.realtimeSinceStartup+10;
        while(Get<bool>(workspace,"_algorithmRunning")&&Time.realtimeSinceStartup<deadline)yield return null;
        Check(!Get<bool>(workspace,"_algorithmRunning")&&!overlay.gameObject.activeInHierarchy,"Parameter failure also closes overlay");
        Check(algorithm.PanelGroup.alpha>0&&algorithm.PanelGroup.interactable,"Failure restores retry UI");
        Capture("04-port-error-retry",null);
        UnityEngine.Object.Destroy(workspace.gameObject);
        for(var wait=Frames();wait.MoveNext();)yield return null;
        Check(overlay==null,"Independent overlay is destroyed with its owning workspace");
        Checks.Add("SCOPE: isolated local cases and one GA execution; no authentication, server calls, grade submission or animation start.");
    }
    private static void ValidateDisplay(AlgorithmRunOverlay overlay,string phase,bool inputReady=true)
    {
        Check(overlay!=null&&overlay.IsVisible,phase+": standalone full-screen canvas is visible");
        Check(overlay.transform.parent==null&&overlay.GetComponent<Canvas>().sortingOrder==31000,phase+": no inherited parent scale or sorting");
        var corners=new Vector3[4];((RectTransform)overlay.transform).GetWorldCorners(corners);
        Check(Mathf.Abs(corners[0].x)<3&&Mathf.Abs(corners[0].y)<3&&Mathf.Abs(corners[2].x-Screen.width)<3&&Mathf.Abs(corners[2].y-Screen.height)<3,phase+": canvas spans actual Game view");
        Check(RectTransformUtility.RectangleContainsScreenPoint(overlay.statusPanel,new Vector2(Screen.width/2f,Screen.height/2f),null),phase+": panel covers screen center");
        if(!inputReady)return; // 新激活Graphic的射线深度要在Unity首帧布局后检查；立即检查仍覆盖全屏根节点与中央面板。
        if(EventSystem.current==null)new GameObject("VerificationEventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=new Vector2(Screen.width*.04f,Screen.height*.04f)},hits);
        string detail=string.Join(",",hits.Select(x=>x.gameObject.name+":"+x.sortingOrder));
        if(hits.Count==0)detail="no hits; "+string.Join(",",overlay.GetComponentsInChildren<Graphic>().Select(x=>x.name+" depth="+x.depth+" culled="+x.canvasRenderer.cull+" raycast="+x.raycastTarget));
        Check(hits.Count>0&&(hits[0].gameObject==overlay.gameObject||hits[0].gameObject.transform.IsChildOf(overlay.transform)),phase+": backdrop blocks scene clicks ("+detail+")");
    }
    private static void Capture(string name,AlgorithmRunOverlay overlay)
    {
        var camera=CameraManager.Instance.mainCamera.GetComponent<Camera>();
        var oldTarget=camera.targetTexture;int oldMask=camera.cullingMask;
        var texture=new RenderTexture(1280,720,24);texture.Create();
        var canvases=UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        // 已先验证生产态根Canvas/全屏边界；仅为将Overlay UI纳入Camera.Render截图，临时转为同相机绘制，随后立即恢复。
        try
        {
            camera.targetTexture=texture;camera.cullingMask|=1<<LayerMask.NameToLayer("UI");
            foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=Mathf.Max(camera.nearClipPlane+.5f,1);RefreshScaler(c);}
            Canvas.ForceUpdateCanvases();camera.Render();
            var old=RenderTexture.active;RenderTexture.active=texture;
            var bitmap=new Texture2D(1280,720,TextureFormat.RGB24,false);bitmap.ReadPixels(new Rect(0,0,1280,720),0,0);bitmap.Apply();
            File.WriteAllBytes(Path.Combine(Output,name+".png"),bitmap.EncodeToPNG());
            RenderTexture.active=old;UnityEngine.Object.Destroy(bitmap);
        }
        finally
        {
            camera.targetTexture=oldTarget;camera.cullingMask=oldMask;
            foreach(var c in canvases){if(c==null)continue;c.renderMode=RenderMode.ScreenSpaceOverlay;c.worldCamera=null;RefreshScaler(c);}
            Canvas.ForceUpdateCanvases();texture.Release();UnityEngine.Object.Destroy(texture);
        }
    }
    private static void RefreshScaler(Canvas canvas)
    {var scaler=canvas.GetComponent<CanvasScaler>();if(scaler!=null){scaler.enabled=false;scaler.enabled=true;}}
    public static void Inspect()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SceneStage1.unity",OpenSceneMode.Single);
        var workspace=UnityEngine.Object.FindObjectOfType<SceneDecisionWorkspaceController>(true);
        var child=UnityEngine.Object.Instantiate(Resources.Load<AlgorithmRunOverlay>("AlgorithmRunOverlay"),workspace.transform,false);
        child.Begin("遗传算法",43);Canvas.ForceUpdateCanvases();var text=new StringBuilder();Dump(child.transform,text);Dump(child.statusPanel,text);
        Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"after-hierarchy.txt"),text.ToString());Debug.Log("OVERLAY_HIERARCHY_INSPECTED");
    }
    private static void Dump(Transform target,StringBuilder report)
    {
        for(var t=target;t!=null;t=t.parent)
        {
            var rect=t as RectTransform;var canvas=t.GetComponent<Canvas>();
            report.AppendLine(t.name+" active="+t.gameObject.activeInHierarchy+" scale="+t.localScale+" pos="+t.position+
                (rect==null?"":" rect="+rect.rect)+
                (canvas==null?"":" root="+canvas.isRootCanvas+" mode="+canvas.renderMode+" order="+canvas.sortingOrder));
        }
    }
    private static T Get<T>(object instance,string name)=>(T)instance.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(instance);
    private static void Check(bool condition,string message)
    {if(!condition)throw new InvalidOperationException(message);Checks.Add("PASS: "+message);Debug.Log("OVERLAY_CHECK "+message);}
}
