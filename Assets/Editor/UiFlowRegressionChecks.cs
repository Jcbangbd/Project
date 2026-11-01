using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vtp.VR.Assessment;
using Vtp.VR.Integration;
using Vtp.VR.Rest;
using ZPMC.VR.Unity;

public static class UiFlowRegressionChecks
{
    public static void ValidateSceneBindings()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/2.unity",OpenSceneMode.Single);
        var account=UnityEngine.Object.FindObjectOfType<AccountManager>(true);
        Check(account!=null,"Actual login scene contains AccountManager");
        PortLoginView.Install(account);
        Check(account.loginPanel.GetComponentInParent<PortLoginView>()!=null &&
            account.userNameLoginInput!=null && account.passwordLoginInput!=null && account.loginButton!=null,
            "Actual login scene binds native prefab inputs and buttons");
        EditorSceneManager.OpenScene("Assets/Scenes/SceneStage1.unity",OpenSceneMode.Single);
        var canvas=GameObject.Find("Canvas");
        var intro=UnityEngine.Object.Instantiate(Resources.Load<ExperimentIntroductionView>("ExperimentIntroductionView"));
        intro.Initialize(canvas.transform,()=>{});
        var learn=(UnityEngine.UI.Button[])typeof(ExperimentIntroductionView).GetField("_legacyLearn",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(intro);
        var experiments=(UnityEngine.UI.Button[])typeof(ExperimentIntroductionView).GetField("_legacyExperiment",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(intro);
        for(int i=0;i<5;i++)Check(learn[i]!=null && experiments[i]!=null,"Original flow actions bound: "+i);
        for(int i=0;i<5;i++)
        {
            intro.tabs[i].onClick.Invoke();
            Check(intro.flow.heading.text==intro.flow.catalog.entries[i].title,"Native tab click updates shared flow: "+i);
        }
        Check(canvas.transform.Find("Play/Down").GetComponent<CanvasGroup>().alpha==0,"Old institutional logo area is hidden");
        var menu=canvas.transform.Find("Play").gameObject;
        menu.SetActive(true);
        ExperimentIntroductionView.HideAll();
        Check(!intro.gameObject.activeSelf,"Introduction closes when leaving the original menu");
        ExperimentIntroductionView.RestoreForMenu(menu);
        Check(intro.gameObject.activeSelf,"Reopening the original menu restores the native introduction");
        Debug.Log("UI_FLOW_SCENE_BINDINGS_PASSED");
    }
    public static void Run()
    {
        var cases=new SceneNumericalExampleDto[10];
        float[] starts={0,22,51,22,22};float[] ends={22,0,22,51,22};
        for(int i=0;i<cases.Length;i++)cases[i]=new SceneNumericalExampleDto {id="case"+i,no=(i+1).ToString(),startPointPosition=starts[i%5],endPointPosition=ends[i%5]};
        SceneNumericalExampleDto[] sample;string error;
        Check(QuestionCaseSampler.TryCreate(cases,17,out sample,out error),"Complete cases can be sampled");
        var types=new HashSet<string>();foreach(var item in sample){string type;SceneExampleTaskCaseAdapter.TryInferTaskTypeFromPositions(item,out type);types.Add(type);}
        Check(sample.Length==5 && types.Count==5,"Exactly one example per operation type");
        SceneNumericalExampleDto[] repeated;QuestionCaseSampler.TryCreate(cases,17,out repeated,out error);
        for(int i=0;i<5;i++)Check(repeated[i].id==sample[i].id,"Seeded sampling is stable");
        Check(!QuestionCaseSampler.TryCreate(new[]{cases[0]},17,out repeated,out error) && error.Contains("装船"),"Incomplete teacher case fails with missing types");
        var answers=new List<PreExperimentCaseTaskTypeAnswerDto>();
        var ids=new Dictionary<string,string>{{"卸船","DISCHARGE"},{"装船","LOAD_SHIP"},{"卸车","UNLOAD_TRUCK"},{"装车","LOAD_TRUCK"},{"翻箱","REHANDLE"}};
        for(int i=0;i<sample.Length;i++){string type;SceneExampleTaskCaseAdapter.TryInferTaskTypeFromPositions(sample[i],out type);answers.Add(new PreExperimentCaseTaskTypeAnswerDto {rowIndex=i,exampleId=sample[i].id,exampleNo=sample[i].no,selectedOptionId=ids[type]});}
        StudentAssessmentRuntime.BeginSession("LOCAL_UI_QA");
        PreExperimentAssessmentRuntime.Begin("QA",sample);
        PreExperimentAssessmentRuntime.SaveCaseTaskTypes("5","算例作业类型勾选",answers);
        foreach(string item in new[]{"6","7","8","9","10","11","12"})PreExperimentAssessmentRuntime.SaveSelection(item,item,new string[0]);
        PreExperimentAssessmentRuntime.Complete();
        Check(Score("5")==5,"All five correct yields five points");
        answers[0].selectedOptionId="INVALID";
        PreExperimentAssessmentRuntime.SaveCaseTaskTypes("5","算例作业类型勾选",answers);
        PreExperimentAssessmentRuntime.Complete();Check(Score("5")==0,"One wrong yields zero");
        answers.RemoveAt(0);PreExperimentAssessmentRuntime.SaveCaseTaskTypes("5","算例作业类型勾选",answers);
        PreExperimentAssessmentRuntime.Complete();Check(Score("5")==0,"One omitted yields zero");
        Check(StudentAssessmentRuntime.BuildReport().maximumScore==100,"Total maximum remains 100");

        var operations=new List<ScheduledOperationDto>();var targets=new List<DischargeTargetSlot>();var positions=new List<Vector3>();
        for(int i=0;i<4;i++){operations.Add(new ScheduledOperationDto {jobId="j"+i,sequence=i+1,craneId="RMG1"});targets.Add(new DischargeTargetSlot(i%2==0?"A35":"A36",2+i*4,1,1));positions.Add(Vector3.zero);}
        var resources=new List<AutomaticQuayCraneScheduler.Resource>();
        for(int i=0;i<2;i++)resources.Add(new AutomaticQuayCraneScheduler.Resource {Id=1010+i,Position=new Vector3(i,0,0),Sources=new List<CellCode>{new CellCode{NestName="V",Bay=i*16+2,Column=0,Tier=86},new CellCode{NestName="V",Bay=i*16+2,Column=1,Tier=86}}});
        List<AutomaticDischargeTask> plan;
        Check(AutomaticQuayCraneScheduler.TryAssign(operations,targets,positions,resources,out plan,out error),"Automatic quay assignment succeeds with adequate capacity");
        Check(plan.Count==4 && resources[0].Used==2 && resources[1].Used==2,"Assignments respect capacity and use both quay cranes");
        Check(operations[0].craneId=="RMG1" && operations[0].quayCraneId>=1010,"Quay IDs remain distinct from yard crane IDs");
        var insufficient=new List<AutomaticQuayCraneScheduler.Resource>();
        Check(!AutomaticQuayCraneScheduler.TryAssign(operations,targets,positions,insufficient,out plan,out error),"Unavailable quay resources block the run");
        string[] blocks={"A35","A36","A37","A40"};
        var fixedResources=new List<AutomaticQuayCraneScheduler.Resource>();
        for(int i=0;i<4;i++)
        {
            Check(AutomaticQuayCraneScheduler.GetFixedQuayCraneId(blocks[i])==1010+i,"Original fixed block binding restored: "+blocks[i]);
            fixedResources.Add(new AutomaticQuayCraneScheduler.Resource{Id=1010+i,AvailableAt=i==3?-10000:0,Position=Vector3.zero,Sources=new List<CellCode>{new CellCode{NestName="Fixed",Bay=i+1,Column=1,Tier=1},new CellCode{NestName="Fixed",Bay=i+1,Column=2,Tier=1}}});
        }
        var fixedOperations=new List<ScheduledOperationDto>();var fixedTargets=new List<DischargeTargetSlot>();var fixedPositions=new List<Vector3>();
        for(int i=0;i<3;i++){fixedOperations.Add(new ScheduledOperationDto{jobId="fixed"+i,sequence=i+1,craneId="RMG1"});fixedTargets.Add(new DischargeTargetSlot(blocks[i],1,1,1));fixedPositions.Add(Vector3.zero);}
        Check(AutomaticQuayCraneScheduler.TryAssign(fixedOperations,fixedTargets,fixedPositions,fixedResources,out plan,out error),"Three yard blocks have adequate fixed resources");
        for(int i=0;i<3;i++)Check(fixedOperations[i].quayCraneId==1010+i,"Each block uses only its original crane");
        Check(fixedResources[3].Used==0,"Fourth crane remains idle even when it would be the earliest-finishing candidate");
        Check(AutomaticQuayCraneScheduler.TryAssign(new[]{new ScheduledOperationDto{jobId="a40",craneId="RMG2"}},new[]{new DischargeTargetSlot("A40",1,1,1)},new[]{Vector3.zero},fixedResources,out plan,out error)&&plan[0].Operation.quayCraneId==1013,"A40 jobs explicitly use the fourth crane");
        Check(!AutomaticQuayCraneScheduler.TryAssign(new[]{new ScheduledOperationDto{jobId="wrong",quayCraneId=1013}},new[]{new DischargeTargetSlot("A35",1,1,1)},new[]{Vector3.zero},fixedResources,out plan,out error),"Conflicting explicit crane cannot cross the fixed block boundary");
        foreach(string name in new[]{"PortLoginView","ExperimentIntroductionView","OperationFlowView","FlowImageZoom","AlgorithmRunOverlay","PreExperimentQuestionnaire","ExperimentAssessmentCanvas"})
        {
            var prefab=Resources.Load<GameObject>(name);Check(prefab!=null,"Native prefab exists: "+name);
            foreach(var t in prefab.GetComponentsInChildren<Transform>(true))
                Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"No missing component: "+name+"/"+t.name);
        }
        Debug.Log("UI_FLOW_REGRESSION_CHECKS_PASSED");
    }
    private static int Score(string id){foreach(var item in StudentAssessmentRuntime.BuildReport().items)if(item.itemId==id)return item.score;return -1;}
    private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
}
