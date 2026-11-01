using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Vtp.VR.Integration;
using Vtp.VR.LocalAlgorithmExe;
using Vtp.VR.Rest;

[InitializeOnLoad]
public static class ExperimentWorkflowVerification
{
    private const string Pending = "ExperimentWorkflowVerification.Pending";
    private static IEnumerator _steps;
    private static readonly List<string> Checks = new List<string>();
    private static string Output => Path.GetFullPath(".codex-tmp/ExperimentWorkflowVerification");
    static ExperimentWorkflowVerification()
    { if (SessionState.GetBool(Pending, false)) EditorApplication.playModeStateChanged += OnPlay; }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("仅在隔离工程验证。");
        Directory.CreateDirectory(Output); Checks.Clear();
        EditorSceneManager.OpenScene("Assets/Scenes/SceneStage1.unity", OpenSceneMode.Single);
        SessionState.SetBool(Pending, true);
        EditorApplication.playModeStateChanged -= OnPlay; EditorApplication.playModeStateChanged += OnPlay;
        EditorApplication.isPlaying = true;
    }
    private static void OnPlay(PlayModeStateChange state)
    { if (state == PlayModeStateChange.EnteredPlayMode) { _steps = Verify(); EditorApplication.update += Tick; } }
    private static void Tick()
    {
        try
        {
            if (_steps.MoveNext()) return;
            File.WriteAllLines(Path.Combine(Output, "checks.txt"), Checks);
            Debug.Log("EXPERIMENT_WORKFLOW_VERIFICATION_PASSED " + Checks.Count); Finish(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error); File.WriteAllText(Path.Combine(Output, "failure.txt"), error.ToString());
            File.WriteAllLines(Path.Combine(Output, "checks.txt"), Checks); Finish(1);
        }
    }
    private static void Finish(int code)
    { SessionState.SetBool(Pending, false); EditorApplication.update -= Tick; EditorApplication.Exit(code); }
    private static IEnumerator Frames(int count = 3)
    { int end = Time.frameCount + count; while (Time.frameCount < end) yield return null; }
    private static IEnumerator Verify()
    {
        for (var wait = Frames(); wait.MoveNext();) yield return null;
        var workspace = UnityEngine.Object.FindObjectOfType<SceneDecisionWorkspaceController>(true);
        var viewer = UnityEngine.Object.FindObjectOfType<SceneExampleViewer>(true);
        Check(workspace != null && viewer != null, "Actual workspace and case viewer found");
        Check(!RestSession.IsAuthenticated, "No production account is authenticated during verification");
        StudentAssessmentRuntime.BeginSession("LOCAL_WORKFLOW_QA");
        string taskId = "WORKFLOW_QA_20261028_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        Invoke(viewer, "OnExperimentTaskSelected", new ExperimentTaskSelectionResult {
            Task = new VrTaskSummaryDto { taskId = taskId, title = "隔离流程验证", version = 1 }, IsLocalTestTask = true });
        var intro = Get<ExperimentIntroductionView>(viewer, "_introductionView");
        Check(intro != null && intro.gameObject.activeInHierarchy, "Selecting a task opens introduction, not questionnaire");
        intro.experimentButton.onClick.Invoke();
        for (var wait = Frames(); wait.MoveNext();) yield return null;
        var caseView = Get<ExperimentExampleSelectionCanvasView>(viewer, "_exampleSelectionCanvasView");
        Check(caseView.gameObject.activeInHierarchy && SceneNumericalExampleRuntime.Examples.Length == 43, "Introduction opens the current 43-case discharge example page");
        Check(caseView.EnterButton.GetComponentInChildren<TMP_Text>().text == "设置参数", "Case action advances to parameter setup");
        Check(!viewer.OpenPostExperimentQuestionnaire(), "Questionnaire is forbidden before experiment completion");
        caseView.EnterButton.onClick.Invoke();
        for (var wait = Frames(); wait.MoveNext();) yield return null;
        var algorithm = Get<AlgorithmAssessmentController>(workspace, "_algorithmAssessmentController");
        Check(Get<RectTransform>(algorithm, "_parameterPage").gameObject.activeInHierarchy, "Parameters are displayed before algorithm selection or 3D load");
        Check(!Get<bool>(workspace, "_entered3D") && Get<string>(algorithm, "_selectedAlgorithmKey") == "", "Editing default GA parameters does not select GA or enter the experiment");
        Check(!caseView.gameObject.activeInHierarchy, "Case page closes before parameter page opens");
        var inputs = Get<Dictionary<string, TMP_InputField>>(algorithm, "_parameterInputs");
        string saved = inputs["POPULATION_SIZE"].text;
        inputs["POPULATION_SIZE"].text = "";
        Get<Button>(algorithm, "_primaryButton").onClick.Invoke();
        Check(!algorithm.ParametersPrepared, "Invalid parameter blocks advancement");
        inputs["POPULATION_SIZE"].text = "10"; inputs["MAX_GENERATIONS"].text = "3";
        inputs["ELITE_COUNT"].text = "1"; inputs["TOURNAMENT_SIZE"].text = "2";
        inputs["STAGNATION_LIMIT"].text = "3"; inputs["PRINT_EVERY"].text = "1";
        var tabs = Get<RectTransform>(algorithm, "_parameterPresetTabs");
        tabs.Find("Preset_pso").GetComponent<Button>().onClick.Invoke();
        Check(Get<string>(algorithm, "_selectedAlgorithmKey") == "", "Editing PSO preset still does not select the execution algorithm");
        tabs.Find("Preset_alns").GetComponent<Button>().onClick.Invoke();
        tabs.Find("Preset_ga").GetComponent<Button>().onClick.Invoke();
        Check(Get<Dictionary<string, TMP_InputField>>(algorithm, "_parameterInputs")["POPULATION_SIZE"].text == "10", "Parameter tab switches preserve independently edited configurations");
        for (var wait = Frames(); wait.MoveNext();) yield return null;
        Capture("01-parameters-before-algorithm");
        Get<Button>(algorithm, "_primaryButton").onClick.Invoke();
        Check(algorithm.ParametersPrepared && Get<RectTransform>(algorithm, "_introductionPage").gameObject.activeInHierarchy, "Saving parameters advances to algorithm selection");
        Check(!Get<bool>(workspace, "_entered3D") && Get<string>(algorithm, "_selectedAlgorithmKey") == "", "Saving parameters starts neither an algorithm nor a 3D experiment");
        Get<Dictionary<string, Image>>(algorithm, "_algorithmImages")["genetic-algorithm-v2"].GetComponent<Button>().onClick.Invoke();
        Get<Dictionary<string, Image>>(algorithm, "_reasonImages").Values.First().GetComponent<Button>().onClick.Invoke();
        Check(Get<Button>(algorithm, "_primaryButton").GetComponentInChildren<TMP_Text>().text == "确认算法并进入实验", "Algorithm confirmation no longer sends the user back to parameter entry");
        Capture("02-confirm-selected-algorithm");
        Get<Button>(algorithm, "_primaryButton").onClick.Invoke();
        var selected = Get<AlgorithmAssessmentSelection>(workspace, "_pendingAlgorithmSelection");
        Check(selected.ExeAlgorithmKey == "ga" && (int)selected.Parameters["POPULATION_SIZE"] == 10, "Only selected GA and its previously saved parameters enter the request");
        float deadline = Time.realtimeSinceStartup + 180;
        bool sawRunning = false;
        while (Get<RmgExeExecutionResult>(workspace, "_postExperimentExecution") == null && Time.realtimeSinceStartup < deadline)
        {
            var overlay = Get<AlgorithmRunOverlay>(workspace, "_runOverlay");
            if (overlay != null && overlay.IsVisible && !sawRunning)
            { sawRunning = true; Capture("03-real-algorithm-running"); }
            yield return null;
        }
        Check(sawRunning && Get<RmgExeExecutionResult>(workspace, "_postExperimentExecution") != null, "Selected algorithm runs once after 3D initialization and returns a real schedule");
        Check(!workspace.ExperimentCompleted && !viewer.OpenPostExperimentQuestionnaire(), "Algorithm completion alone does not unlock the questionnaire");
        var resultPage = Get<ScheduleResultAssessmentController>(workspace, "_scheduleResultAssessmentController");
        Check(resultPage != null && Get<CanvasGroup>(resultPage, "_group").alpha == 1, "Algorithm success opens schedule review before device simulation");
        Check(!Get<bool>(Get<UMP_LevelInfo>(workspace, "_levelInfo"), "_scriptStarted") && Get<object>(workspace, "_completionMonitor") == null,
            "Neither device script nor completion monitor starts before schedule review submission");
        Check(Time.timeScale == 0 && !Get<bool>(workspace, "_scheduleResultConfirmed"), "Scene remains locked while schedule review is pending");
        resultPage.GetComponentsInChildren<Button>().First(b => b.name == "SubmitButton").onClick.Invoke();
        Check(Get<CanvasGroup>(resultPage, "_group").alpha == 1 && !Get<bool>(workspace, "_scheduleResultConfirmed"),
            "Incomplete review cannot start three-dimensional scheduling");
        Check(PreExperimentAssessmentRuntime.Answers.Length == 0, "Post-experiment eight-question answers are not collected early");
        Capture("04-schedule-review-before-simulation");
        var metrics = Get<object>(resultPage, "_metrics");
        Get<TMP_InputField>(resultPage, "_cmaxInput").text = Convert.ToDouble(Get<object>(metrics, "Cmax")).ToString("R", CultureInfo.InvariantCulture);
        Get<TMP_InputField>(resultPage, "_rmg1Input").text = Get<object>(metrics, "Rmg1Count").ToString();
        Get<TMP_InputField>(resultPage, "_rmg2Input").text = Get<object>(metrics, "Rmg2Count").ToString();
        Get<TMP_InputField>(resultPage, "_sharedInput").text = Get<object>(metrics, "SharedCount").ToString();
        Get<Toggle>(resultPage, "_completeToggle").isOn = true;
        resultPage.GetComponentsInChildren<Button>().First(b => b.name == "SubmitButton").onClick.Invoke();
        Check(Get<bool>(workspace, "_scheduleResultConfirmed") && Get<CanvasGroup>(resultPage, "_group").alpha == 0,
            "Submitting review closes the page and starts only the current three-dimensional schedule");
        string reportDirectory = Path.Combine(Application.persistentDataPath, "AssessmentRecords");
        Check(!Directory.Exists(reportDirectory) || Directory.GetFiles(reportDirectory, taskId + "_*.json").Length == 0,
            "Schedule review records its score without saving or submitting an incomplete total report");
        deadline = Time.realtimeSinceStartup + 20;
        int total = 0, completed = 0; float endTime = 0;
        while ((TaskManager.Instance == null || !TaskManager.Instance.TryGetCompletion(out total, out completed, out endTime)) && Time.realtimeSinceStartup < deadline) yield return null;
        Check(total == 43, "Original device script actually imports all 43 simulation tasks");
        int revision = TaskManager.Instance.TaskRevision;
        resultPage.GetComponentsInChildren<Button>().First(b => b.name == "SubmitButton").onClick.Invoke();
        Check(TaskManager.Instance.TaskRevision == revision, "Repeated review submission does not restart or duplicate device tasks");
        Check(AutomaticQuayCraneScheduler.Current.All(t => t.Operation.quayCraneId != 1013), "Fixed three-yard-block crane assignment is unchanged");
        Debug.Log("WORKFLOW_QA_SIMULATION_END_TIME " + endTime);
        Capture("04-simulation-before-questionnaire");
        // 只加速隔离验证会话的仿真时钟；不修改项目默认倍速、不伪造完成状态。
        TimeHelper.Instance.TimeScale = 512;
        Time.timeScale = 8;
        deadline = Time.realtimeSinceStartup + 45;
        float nextDiagnostic = 0;
        while (!workspace.ExperimentCompleted && Time.realtimeSinceStartup < deadline)
        {
            if (Time.realtimeSinceStartup > nextDiagnostic)
            {
                TaskManager.Instance.TryGetCompletion(out total, out completed, out endTime);
                Debug.Log("WORKFLOW_SIM_PROGRESS time=" + TimeHelper.Instance.RectifiedTime + " total=" + total + " completed=" + completed +
                    " queue=" + ZPMC.VR.Unity.EventScripter.GetGlobalInstance().recordMarshal.Count + " taskActive=" + TaskManager.Instance.gameObject.activeInHierarchy +
                    " paused=" + Get<bool>(TimeHelper.Instance, "isPaused") + " scale=" + Time.timeScale + " clockScale=" + Get<float>(TimeHelper.Instance, "timeScale"));
                nextDiagnostic = Time.realtimeSinceStartup + 5;
            }
            yield return null;
        }
        Check(workspace.ExperimentCompleted, "Actual simulation reaches the completion detector within bounded verification");
        TaskManager.Instance.TryGetCompletion(out total, out completed, out endTime);
        Check(completed == total && total == 43 && ZPMC.VR.Unity.EventScripter.GetGlobalInstance().recordMarshal.IsEmpty, "Completion requires all original task states finished and all queued device records consumed");
        Check(TimeHelper.Instance.RectifiedTime >= TaskManager.Instance.MotionEndTime, "Completion waits for the last device motion, not only the final delivery task");
        var endView = Get<ExperimentCompletionView>(workspace, "_completionView");
        Check(endView != null && endView.gameObject.activeInHierarchy, "Saved native completion prefab opens with Start questionnaire action");
        Capture("05-experiment-completed");
        endView.startQuestionnaire.onClick.Invoke();
        var question = Get<PreExperimentQuestionnaireController>(viewer, "_preExperimentQuestionnaire");
        Check(question != null && question.IsOpen && !endView.gameObject.activeInHierarchy, "Start questionnaire closes completion dialog and opens question one");
        for (var wait = Frames(); wait.MoveNext();) yield return null;
        Capture("06-post-experiment-questionnaire");
        var sample = Get<SceneNumericalExampleDto[]>(question, "_item5Examples");
        var controls = Get<RectTransform>(question, "_answerContent").GetComponentsInChildren<Button>().Where(b => b.name == "TaskTypeDropdown").ToArray();
        var ids = new Dictionary<string, string> { { "卸船", "DISCHARGE" }, { "装船", "LOAD_SHIP" }, { "卸车", "UNLOAD_TRUCK" }, { "装车", "LOAD_TRUCK" }, { "翻箱", "REHANDLE" } };
        Check(controls.Length == 5, "Post-experiment question one still contains one example of each five operation types");
        for (int index = 0; index < controls.Length; index++)
        {
            controls[index].onClick.Invoke();
            string type; SceneExampleTaskCaseAdapter.TryInferTaskTypeFromPositions(sample[index], out type);
            var popup = Get<RectTransform>(question, "_dropdownPopupRoot");
            popup.GetComponentsInChildren<Button>().First(b => b.name == "Option_" + ids[type]).onClick.Invoke();
        }
        var next = Get<Button>(question, "_nextButton"); next.onClick.Invoke();
        for (int index = 1; index < 8; index++)
        {
            for (var wait = Frames(); wait.MoveNext();) yield return null;
            var answers = Get<RectTransform>(question, "_answerContent");
            var numeric = answers.GetComponentsInChildren<TMP_InputField>();
            if (numeric.Length > 0) foreach (var input in numeric) input.text = "1";
            else answers.GetComponentsInChildren<Button>().First(b => b.name.StartsWith("Option_")).onClick.Invoke();
            next.onClick.Invoke();
        }
        Check(PreExperimentAssessmentRuntime.IsCompleted && PreExperimentAssessmentRuntime.Answers.Length == 8, "All eight questions retain their answers and original scoring pipeline");
        var completion = Get<RectTransform>(question, "_completionPage");
        Check(completion.GetComponentInChildren<Button>().GetComponentInChildren<TMP_Text>().text == "提交答题并返回实验介绍", "Eight-question completion directly offers final submit and return");
        Check(Get<CanvasGroup>(resultPage, "_group").alpha == 0, "Schedule review is not reopened after the eight questions");
        Capture("07-all-answers-completed");
        completion.GetComponentInChildren<Button>().onClick.Invoke();
        for (var wait = Frames(); wait.MoveNext();) yield return null;
        Check(intro.gameObject.activeInHierarchy && !Get<bool>(workspace, "_entered3D"), "Final submit returns to the original introduction instead of starting another experiment");
        Check(Get<CanvasGroup>(resultPage, "_group").alpha == 0 && !endView.gameObject.activeInHierarchy, "Answer and completion overlays are closed after returning");
        var reports = Directory.GetFiles(reportDirectory, taskId + "_*.json");
        Check(reports.Length == 1, "Only one final report is saved, after all post-experiment answers");
        string reportJson = File.ReadAllText(reports[0]);
        var report = JsonUtility.FromJson<StudentAssessmentReportDto>(reportJson);
        Check(report.studentNumber == "LOCAL_WORKFLOW_QA" && report.taskId == taskId && report.preExperimentAnswers.Length == 8, "Saved report retains this isolated task, all per-question answers and final score");
        Check(report.items.First(i => i.itemId == "5").score == 5 && report.maximumScore == 100, "Five-correct first-question scoring and total maximum remain unchanged");
        Check(report.items.First(i => i.itemId == "16").score > 0, "Final report retains the schedule-review score collected before simulation");
        // 将本次随机ID创建的单个QA成绩移回工程证据目录，不触碰任何原有成绩。
        if (!Path.GetFullPath(reports[0]).StartsWith(Path.GetFullPath(reportDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("QA报告路径越界。");
        File.Copy(reports[0], Path.Combine(Output, "saved-score-report.json"), true);
        File.Delete(reports[0]);
        Capture("08-returned-to-introduction");
        Checks.Add("SCOPE: local-only QA task, one real GA run, accelerated original device clock, no authentication or official server submission; only this run's random-ID score file moved back into project evidence.");
    }
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
    private static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static void Check(bool value, string description)
    { if (!value) throw new InvalidOperationException(description); Checks.Add("PASS: " + description); }
    private static void Capture(string name)
    {
        var camera = UnityEngine.Object.FindObjectsOfType<Camera>().FirstOrDefault(c => c.enabled);
        if (camera == null) throw new InvalidOperationException("缺少截图相机。");
        var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var texture = new RenderTexture(1280, 720, 24); texture.Create(); var oldTarget = camera.targetTexture; float oldAspect = camera.aspect;
        try
        {
            camera.targetTexture = texture; camera.aspect = 16f / 9;
            foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = Mathf.Max(camera.nearClipPlane + .5f, 1); Refresh(canvas); }
            Canvas.ForceUpdateCanvases(); camera.Render(); var previous = RenderTexture.active; RenderTexture.active = texture;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(Output, name + ".png"), image.EncodeToPNG()); RenderTexture.active = previous; UnityEngine.Object.Destroy(image);
        }
        finally
        {
            camera.targetTexture = oldTarget; camera.aspect = oldAspect;
            foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; Refresh(canvas); }
            Canvas.ForceUpdateCanvases(); texture.Release(); UnityEngine.Object.Destroy(texture);
        }
    }
    private static void Refresh(Canvas canvas) { var scaler = canvas.GetComponent<CanvasScaler>(); if (scaler != null) { scaler.enabled = false; scaler.enabled = true; } }
}
