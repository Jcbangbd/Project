using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Vtp.VR.Integration;
using Vtp.VR.LocalAlgorithmExe;
using Vtp.VR.Rest;

// 仅编辑器使用。空测试场景、模拟数据，不访问服务器、不运行算法、不提交成绩。
[InitializeOnLoad]
public static class ExperimentUiVisualVerification
{
    private const string SessionKey = "ExperimentUiVisualVerification.Pending";
    private static IEnumerator _steps;
    private static Camera _camera;
    private static RectTransform _canvas;
    private static RenderTexture _target;
    private static readonly List<string> Checks = new List<string>();
    private static string Output => Path.GetFullPath(".codex-tmp/UIReferenceAlignmentVerification");

    static ExperimentUiVisualVerification()
    {
        if (SessionState.GetBool(SessionKey, false))
            EditorApplication.playModeStateChanged += OnPlayMode;
    }

    [MenuItem("Tools/Experiment UI/Verify in empty test scene")]
    public static void Run()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Run via Unity batch mode to avoid replacing an open scene.");
        Directory.CreateDirectory(Output);
        foreach (string spriteName in new[] { "UISprite", "Knob", "Checkmark", "DropdownArrow" })
        {
            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/" + spriteName + ".psd");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long fileId);
            Debug.Log("UI_NATIVE_SPRITE " + spriteName + " " + guid + " " + fileId);
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(SessionKey, true);
        EditorApplication.playModeStateChanged -= OnPlayMode;
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        _steps = Verify();
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (_steps.MoveNext()) return;
            File.WriteAllLines(Path.Combine(Output, "checks.txt"), Checks);
            Debug.Log("EXPERIMENT_UI_VERIFICATION_PASSED " + Checks.Count);
            Finish(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            File.WriteAllText(Path.Combine(Output, "failure.txt"), exception.ToString());
            Finish(1);
        }
    }

    private static void Finish(int code)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(SessionKey, false);
        EditorApplication.Exit(code);
    }

    private static IEnumerator Frames(int count = 4)
    {
        int until = Time.frameCount + count;
        while (Time.frameCount < until) yield return null;
    }

    private static IEnumerator Verify()
    {
        var events = new GameObject("TestEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        _camera = new GameObject("TestCamera", typeof(Camera)).GetComponent<Camera>();
        _camera.orthographic = true;
        _camera.orthographicSize = 540;
        _camera.transform.position = new Vector3(0, 0, -10);
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = ExperimentUiTheme.Page;
        _camera.nearClipPlane = .1f;
        _camera.farClipPlane = 100;
        _canvas = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(GraphicRaycaster)).GetComponent<RectTransform>();
        _canvas.sizeDelta = new Vector2(1920, 1080);
        Canvas canvas = _canvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = _camera;
        canvas.sortingOrder = 100;
        Resize(1920, 1080);
        ExperimentUiSkin skin = Resources.Load<ExperimentUiSkin>("ExperimentUiSkin");
        Require(skin != null && skin.knob != null && skin.checkmark != null && skin.dropdownArrow != null
            && skin.fontSource != null && skin.assessmentBackground != null
            && skin.assessmentPanel != null && skin.assessmentBorder != null
            && skin.assessmentGlow != null && skin.assessmentDropdown != null
            && skin.assessmentSquare != null && skin.assessmentSquareBorder != null,
            "Build-safe Shift UI, icon and font references available");
        Require(ExperimentUiTheme.Font.HasCharacter('\u8fed', true, true), "Iteration character renders without missing glyph");

        UiFlowRegressionChecks.Run();
        var loginPrefab=Resources.Load<PortLoginView>("PortLoginView");
        var login=UnityEngine.Object.Instantiate(loginPrefab,_canvas,false);
        ExperimentUiTheme.Stretch(login.GetComponent<RectTransform>());
        Require(login.userNameLabel!=null && login.userNameLabel.text=="用户名" && login.passwordLabel!=null && login.passwordLabel.text=="密码", "Login field labels are serialized native components");
        for(IEnumerator wait=Frames();wait.MoveNext();)yield return null;
        Capture("00-login"); login.gameObject.SetActive(false);
        var intro=UnityEngine.Object.Instantiate(Resources.Load<ExperimentIntroductionView>("ExperimentIntroductionView"),_canvas,false);
        intro.transform.SetParent(null,false);
        intro.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceCamera;
        intro.GetComponent<Canvas>().worldCamera=_camera;
        intro.GetComponent<Canvas>().planeDistance=1;
        intro.Select(0);
        for(IEnumerator wait=Frames();wait.MoveNext();)yield return null;
        var diagramLayout=intro.flow.GetComponentInChildren<DischargeFlowDiagram>(true);
        Require(diagramLayout!=null && diagramLayout.stepLayout.GetComponentsInChildren<RawImage>().Length==1 && diagramLayout.stepLayout.GetComponentsInChildren<TMP_Text>().Count(t=>t.name.StartsWith("StepCaption_"))==9,"Blue Harbor diagram displays one transparent blueprint strip with nine editable step captions");
        Capture("00b-introduction");
        Require(intro.flow.description.preferredHeight<=intro.flow.description.GetComponentInParent<ScrollRect>().viewport.rect.height+1,"Unloading introduction text fits without truncating the last line: " + intro.flow.description.preferredHeight + "/" + intro.flow.description.GetComponentInParent<ScrollRect>().viewport.rect.height);
        for(int i=0;i<5;i++)
        {
            intro.Select(i);
            Require(intro.flow.heading.text==intro.flow.catalog.entries[i].title,"Reference introduction switches flow: "+i);
            Require(intro.tabs[i].GetComponent<SciFiReferenceButton>().selected,"Reference tab selected state: "+i);
            Require(diagramLayout.stepLayout.activeSelf==(i==0) && diagramLayout.originalDiagram.gameObject.activeSelf==(i!=0),"Original diagrams remain available for other operation types: "+i);
            for(IEnumerator wait=Frames();wait.MoveNext();)yield return null;
            Capture("00b-introduction-flow-"+i);
        }
        intro.Select(0);Resize(1100,625);
        for(IEnumerator wait=Frames();wait.MoveNext();)yield return null;
        Capture("00b-introduction-1100");Resize(1920,1080);
        Resize(1920,1145);
        for(IEnumerator wait=Frames();wait.MoveNext();)yield return null;
        Capture("00b-introduction-reference-size");Resize(1920,1080);
        intro.flow.zoomButton.onClick.Invoke();
        for(IEnumerator wait=Frames();wait.MoveNext();)yield return null;
        var zoom=UnityEngine.Object.FindObjectOfType<FlowImageZoom>();
        Require(zoom!=null && zoom.image.sprite==intro.flow.catalog.entries[0].diagram,"Redesigned zoom button opens the authoritative original diagram");
        zoom.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceCamera;zoom.GetComponent<Canvas>().worldCamera=_camera;zoom.GetComponent<Canvas>().planeDistance=.5f;
        for(IEnumerator wait=Frames();wait.MoveNext();)yield return null;
        Capture("00b-introduction-zoom");zoom.close.onClick.Invoke();
        for(IEnumerator wait=Frames();wait.MoveNext();)yield return null;
        Require(UnityEngine.Object.FindObjectOfType<FlowImageZoom>()==null && intro.gameObject.activeSelf,"Closing diagram zoom restores the introduction");
        intro.gameObject.SetActive(false);
        var loader=UnityEngine.Object.Instantiate(Resources.Load<AlgorithmRunOverlay>("AlgorithmRunOverlay"),_canvas,false);
        loader.Begin("遗传算法",43);loader.SetStage(1,"正在执行所选算法，请稍候……");
        Require(loader.IsVisible,"Running overlay establishes a valid standalone production canvas before capture");
        Require(loader.stageNodes.Length==3 && loader.stageLinks.Length==2,"Modal retains three stage nodes and connecting track");
        Require(loader.stageChecks[0].gameObject.activeSelf && loader.stageHalos[1].gameObject.activeSelf && !loader.stageChecks[2].gameObject.activeSelf,"Modal distinguishes completed, running, and waiting stages");
        Require(loader.GetComponent<Image>().material.shader.isSupported,"Frosted modal backdrop shader supported");
        Require(loader.GetComponent<CanvasGroup>().ignoreParentGroups,"Modal ignores hidden parent canvas group");
        for(IEnumerator wait=Frames();wait.MoveNext();)yield return null;
        var pointer=new PointerEventData(EventSystem.current){position=new Vector2(Screen.width*.05f,Screen.height*.05f)};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Require(hits.Count>0 && (hits[0].gameObject==loader.gameObject || hits[0].gameObject.transform.IsChildOf(loader.transform)),"Modal backdrop blocks pointer input outside the panel");
        Capture("00c-algorithm-running");
        loader.Report("正在校验调度结果并准备自动分配岸桥……");
        Require(loader.CurrentStage==2 && loader.stageChecks[1].gameObject.activeSelf,"Real validation status advances the stage track");
        loader.Report("正在执行算法");Require(loader.CurrentStage==2,"Late execution messages do not reverse completed stages");
        loader.Finish();Require(!loader.gameObject.activeSelf,"Modal closes when calculation finishes");
        SceneNumericalExampleDto[] examples = new SceneNumericalExampleDto[10];
        float[] starts={0,22,51,22,22}; float[] ends={22,0,22,51,22};
        for (int i = 0; i < examples.Length; i++)
            examples[i] = new SceneNumericalExampleDto { id = "qa" + i, no = (i + 1).ToString(),
                argm = "待算法分配", startPointPosition = starts[i%5],
                startList = 1, endPointPosition = ends[i%5], endList = 1 };
        var question = Panel<PreExperimentQuestionnaireController>("Questionnaire");
        question.Initialize(null);
        question.Open("UI_QA_ONLY", examples);
        for (IEnumerator wait = Frames(); wait.MoveNext();) yield return null;
        Capture("01-questionnaire");
        loader.Begin("遗传算法",43);loader.SetStage(1,"正在执行所选算法，请稍候……");
        float oldScale=Time.timeScale;Time.timeScale=0;
        var rotation=loader.spinner.localRotation;
        float waitUntil=Time.realtimeSinceStartup+1.1f;
        while(Time.realtimeSinceStartup<waitUntil)yield return null;
        Require(loader.elapsedText.text!="已用时 00:00" && loader.spinner.localRotation!=rotation,"Modal time and spinner continue while simulation paused");
        Capture("00d-modal-over-questionnaire");
        Resize(854,494);
        for(IEnumerator wait=Frames();wait.MoveNext();)yield return null;
        Capture("00d-modal-reference-size");
        Resize(1100,625);
        for(IEnumerator wait=Frames();wait.MoveNext();)yield return null;
        Capture("00d-modal-1100");Resize(1920,1080);
        loader.Finish();Time.timeScale=oldScale;
        hits.Clear();EventSystem.current.RaycastAll(pointer,hits);
        Require(hits.Count==0 || !(hits[0].gameObject==loader.gameObject || hits[0].gameObject.transform.IsChildOf(loader.transform)),"Finishing modal releases its pointer blocker");
        Button firstDropdown = FindButton(question.transform, "TaskTypeDropdown");
        Require(firstDropdown != null, "Task-type selector is present");
        firstDropdown.onClick.Invoke();
        for (IEnumerator wait = Frames(); wait.MoveNext();) yield return null;
        RectTransform popupRoot = Get<RectTransform>(question, "_dropdownPopupRoot");
        Require(popupRoot != null && FindButton(popupRoot, "Option_") != null,
            "Task-type options open directly below the selected control");
        Capture("01b-questionnaire-dropdown-open");
        Button blocker = FindButton(popupRoot, "TaskTypeMenuBlocker");
        if (blocker != null) blocker.onClick.Invoke();
        for (IEnumerator wait = Frames(); wait.MoveNext();) yield return null;
        Button next = Get<Button>(question, "_nextButton");
        next.onClick.Invoke();
        Require(Get<int>(question, "_questionIndex") == 0, "Incomplete task types block continuation");
        Button[] selectors = FindButtons(question.transform, "TaskTypeDropdown");
        Require(selectors.Length == 5, "Five operation rows have independent selectors");
        foreach (Button selector in selectors)
        {
            selector.onClick.Invoke();
            Button option = FindButton(popupRoot, "Option_");
            Require(option != null, "Each selector exposes the full task-type option menu");
            option.onClick.Invoke();
        }
        var answers = Get<Dictionary<int, string>>(question, "_item5TaskTypeAnswers");
        Require(answers.Count == 5, "All five operation-type controls record answers");
        Require(question.GetComponentInChildren<OperationFlowView>(true).gameObject.activeSelf,
            "Selecting a type reveals the shared learning flow below the table");
        for(IEnumerator wait=Frames();wait.MoveNext();)yield return null;
        Capture("01c-questionnaire-flow");
        Resize(1100,625);
        for(IEnumerator wait=Frames();wait.MoveNext();)yield return null;
        Capture("01d-questionnaire-1100");Resize(1920,1080);
        next.onClick.Invoke();
        for (IEnumerator wait = Frames(); wait.MoveNext();) yield return null;
        Require(Get<int>(question, "_questionIndex") == 1, "Completed task types navigate to next question");
        Capture("02-choice-question");
        Get<Button>(question, "_previousButton").onClick.Invoke();
        for (IEnumerator wait = Frames(); wait.MoveNext();) yield return null;
        Require(answers.Count == 5, "Previous navigation preserves operation-type answers");
        Capture("03-questionnaire-selected");
        for(int index=1;index<8;index++)
        {
            Invoke(question,"ShowQuestion",index);
            for(IEnumerator wait=Frames();wait.MoveNext();)yield return null;
            foreach(var text in Get<RectTransform>(question,"_answerContent").GetComponentsInChildren<TMP_Text>(true))
                Require(Mathf.Abs(text.fontSize-PreExperimentQuestionnaireController.AnswerFontSize)<.1f,"Question "+(index+1)+" answer text "+text.name+" matches question-one body size");
            Capture("11-question-"+(index+1).ToString("00"));
        }
        question.Hide();

        int confirms = 0;
        AlgorithmAssessmentSelection selection = null;
        var algorithm = Panel<AlgorithmAssessmentController>("AlgorithmAssessment");
        algorithm.Initialize(value => { confirms++; selection = value; }, null);
        algorithm.Open();
        Require(Get<string>(algorithm, "_selectedAlgorithmKey") == "", "No algorithm preselection");
        Require(!Get<Button>(algorithm, "_primaryButton").interactable, "Confirm disabled until algorithm and reason selected");
        for (IEnumerator wait = Frames(); wait.MoveNext();) yield return null;
        Capture("04-algorithm-selection");
        foreach (string key in new[] { "ga", "pso", "alns" })
        {
            algorithm.ResetForNewAttempt();
            algorithm.Open();
            string uiKey = key == "ga" ? "genetic-algorithm-v2" : key == "pso" ? "particle-swarm" : "alns";
            Get<Dictionary<string, Image>>(algorithm, "_algorithmImages")[uiKey].GetComponent<Button>().onClick.Invoke();
            for (IEnumerator wait = Frames(); wait.MoveNext();) yield return null;
            Require(Get<RectTransform>(algorithm, "_reasonPage").gameObject.activeSelf, key + ": clicking opens reasons");
            Capture("05-" + key + "-reasons");
            foreach (Image reason in Get<Dictionary<string, Image>>(algorithm, "_reasonImages").Values)
            { reason.GetComponent<Button>().onClick.Invoke(); break; }
            Require(Get<RectTransform>(algorithm, "_introductionPage").gameObject.activeSelf, key + ": reason returns to algorithm page");
            Get<Button>(algorithm, "_primaryButton").onClick.Invoke();
            for (IEnumerator wait = Frames(); wait.MoveNext();) yield return null;
            var inputs = Get<Dictionary<string, TMP_InputField>>(algorithm, "_parameterInputs");
            Require(inputs.Count == RmgExeParameterFactory.GetDefinitions(key).Length, key + ": all defined parameter inputs present");
            Capture("06-" + key + "-parameters");
            ScrollRect parameterScroll = Get<RectTransform>(algorithm, "_parameterContent").GetComponentInParent<ScrollRect>();
            if (key != "ga")
            {
                Require(parameterScroll.content.rect.height > parameterScroll.viewport.rect.height,
                    key + ": long parameter form is scrollable");
                parameterScroll.verticalNormalizedPosition = 0;
                for (IEnumerator wait = Frames(); wait.MoveNext();) yield return null;
                Capture("06b-" + key + "-parameters-bottom");
                parameterScroll.verticalNormalizedPosition = 1;
            }
            if (key == "ga")
            {
                Resize(1366, 768);
                for (IEnumerator wait = Frames(); wait.MoveNext();) yield return null;
                Capture("07-ga-parameters-1366");
                TMP_InputField focusInput = null;
                foreach (TMP_InputField input in inputs.Values) { focusInput = input; break; }
                focusInput.Select();
                focusInput.ActivateInputField();
                for (IEnumerator wait = Frames(); wait.MoveNext();) yield return null;
                Capture("07b-ga-parameters-focused");
                focusInput.DeactivateInputField();
                Resize(1280, 1024);
                for (IEnumerator wait = Frames(); wait.MoveNext();) yield return null;
                Capture("08-ga-parameters-1280x1024");
                Resize(1920, 1080);
            }
            TMP_InputField first = null;
            foreach (TMP_InputField input in inputs.Values) { first = input; break; }
            string saved = first.text;
            first.text = "";
            Get<Button>(algorithm, "_primaryButton").onClick.Invoke();
            Require(confirms == Array.IndexOf(new[] { "ga", "pso", "alns" }, key), key + ": invalid input blocks confirmation");
            first.text = saved;
            Get<Button>(algorithm, "_primaryButton").onClick.Invoke();
            Require(selection != null && selection.ExeAlgorithmKey == key, key + ": confirmation returns only selected algorithm");
        }
        Require(confirms == 3, "Exactly one confirmation per chosen algorithm");
        algorithm.Hide();

        var task = Panel<ExperimentTaskSelectionController>("TaskSelection");
        Invoke(task, "BuildInterface");
        Invoke(task, "OnTasksReceived", (object)new[] {
            new VrTaskSummaryDto { taskId = "TASK-001", title = "自动化集装箱码头双轨道吊调度实验", taskType = "调度优化", status = "READY", deadline = "2026-09-30 18:00" },
            new VrTaskSummaryDto { taskId = "TASK-002", title = "共享作业区安全距离与负荷均衡实验", taskType = "调度优化", status = "READY", deadline = "2026-10-08 18:00" } });
        Get<CanvasGroup>(task, "_group").alpha = 1;
        for (IEnumerator wait = Frames(); wait.MoveNext();) yield return null;
        Capture("09-task-list");
        task.GetType().GetField("_allowLocalTask",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(task,true);
        task.GetType().GetField("_localTaskId",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(task,"kc1");
        Invoke(task,"ShowUnavailable","HTTP/1.1 404 Not Found");
        for(IEnumerator wait=Frames();wait.MoveNext();)yield return null;
        Require(!Get<TMP_Text>(task,"_statusText").gameObject.activeSelf,"Task experience hides internal fallback diagnostics");
        foreach(var text in Get<RectTransform>(task,"_content").GetComponentsInChildren<TMP_Text>())
            Require(!text.text.Contains("测试")&&!text.text.Contains("TEST"),"Task card presents no testing labels or internal status codes");
        Capture("09b-task-experience");
        task.Hide();

        var viewerObject=new GameObject("OfflineCasePresentationCheck");viewerObject.SetActive(false);
        var viewer=viewerObject.AddComponent<SceneExampleViewer>();
        Invoke(viewer,"BindExampleSelectionCanvasView");
        Invoke(viewer,"ApplyExamples","kc1",examples,"服务器算例不可用 ZIP 本地测试",(Color)ExperimentUiTheme.Error,true,"服务器算例不可用 ZIP 本地测试",examples);
        var caseView=Get<ExperimentExampleSelectionCanvasView>(viewer,"_exampleSelectionCanvasView");caseView.gameObject.SetActive(true);
        for(IEnumerator wait=Frames();wait.MoveNext();)yield return null;
        Require(string.IsNullOrEmpty(caseView.StatusText.text)&&!caseView.FallbackNotice.gameObject.activeSelf,"Case experience has neither fallback red text nor source popup");
        Require(SceneNumericalExampleRuntime.IsLocalFallback,"Hiding diagnostics does not change local-source assessment protection");
        foreach(var text in caseView.GetComponentsInChildren<TMP_Text>())
            Require(!text.text.Contains("测试")&&!text.text.Contains("服务器算例不可用"),"Visible case text contains no testing diagnostics");
        var help=caseView.GetComponentInChildren<PositionMeaningTooltip>(true);
        Require(help!=null&&help.GetComponent<Image>().sprite!=null&&!help.IsOpen,"Position help has a saved circular icon and initially hidden panel");
        var pointerEvent=new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(help.gameObject,pointerEvent,ExecuteEvents.pointerEnterHandler);
        Require(help.IsOpen&&help.Panel.GetComponentInChildren<TMP_Text>().text.Contains("第35贝、第4列"),"Pointer enter explains the full 35/4 position meaning");
        var helpText=help.Panel.GetComponentInChildren<TMP_Text>();
        Require(EditorUtility.IsPersistent(helpText.font),"Position help retains a saved Chinese font asset");
        foreach(char character in PositionMeaningTooltip.Explanation.Where(c=>!char.IsWhiteSpace(c)).Distinct())
            Require(helpText.font.HasCharacter(character,false,false),"Position-help font already covers character "+character);
        for(IEnumerator wait=Frames();wait.MoveNext();)yield return null;
        Capture("12-position-help-open");
        ExecuteEvents.Execute(help.gameObject,pointerEvent,ExecuteEvents.pointerExitHandler);
        Require(!help.IsOpen,"Pointer exit closes position help");
        Capture("12b-cases-experience");
        caseView.gameObject.SetActive(false);

        var result = Panel<ScheduleResultAssessmentController>("ScheduleResult");
        var operations = new ScheduledOperationDto[10];
        for (int i = 0; i < operations.Length; i++) operations[i] = new ScheduledOperationDto {
            sequence = i + 1, jobId = "JOB-" + (i + 1), craneId = i % 2 == 0 ? "RMG1" : "RMG2",
            fromCode = "B" + (i + 1) + "-R1-T1", toCode = "B" + (i + 8) + "-R2-T1",
            plannedStartSeconds = i * 40, plannedEndSeconds = i * 40 + 36 };
        result.Open(null, new SchedulingResultDto { operations = operations }, null);
        for (IEnumerator wait = Frames(); wait.MoveNext();) yield return null;
        Capture("10-result-check");
        Require(result.GetComponentsInChildren<TMP_InputField>().Length == 4, "Result form retains four inputs");
        Require(result.GetComponentsInChildren<Toggle>().Length == 1, "Result form retains completeness checkbox");
        Checks.Add("Screenshots rendered by native Unity uGUI/TMP; no network calls, executable launch or grade submission.");
    }

    private static T Panel<T>(string name) where T : Component
    {
        if(typeof(T)==typeof(PreExperimentQuestionnaireController))
            return UnityEngine.Object.Instantiate(Resources.Load<GameObject>("PreExperimentQuestionnaire"),_canvas,false).GetComponent<T>();
        RectTransform rect = ExperimentUiTheme.Object(name, _canvas).GetComponent<RectTransform>();
        ExperimentUiTheme.Stretch(rect);
        return rect.gameObject.AddComponent<T>();
    }

    private static T Get<T>(object target, string name) => (T)target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static Button FindButton(Transform root, string nameOrPrefix)
    {
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int index = 0; index < buttons.Length; index++)
            if (buttons[index].gameObject.activeInHierarchy && buttons[index].name.StartsWith(nameOrPrefix, StringComparison.Ordinal))
                return buttons[index];
        return null;
    }
    private static Button[] FindButtons(Transform root, string exactName)
    {
        var matches = new List<Button>();
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
            if (string.Equals(button.name, exactName, StringComparison.Ordinal))
                matches.Add(button);
        return matches.ToArray();
    }
    private static void Invoke(object target, string name, params object[] arguments) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
    private static void Require(bool condition, string check)
    {
        if (!condition) throw new InvalidOperationException(check);
        Checks.Add("PASS: " + check);
    }

    private static void Resize(int width, int height)
    {
        if (_target != null) { _target.Release(); UnityEngine.Object.Destroy(_target); }
        _target = new RenderTexture(width, height, 24);
        _target.Create();
        _camera.targetTexture = _target;
        _camera.aspect = (float)width / height;
        _camera.orthographicSize = height / 2f;
        _canvas.sizeDelta = new Vector2(width, height);
        foreach (ExperimentUiViewport viewport in _canvas.GetComponentsInChildren<ExperimentUiViewport>(true)) viewport.Refresh();
    }

    private static void Capture(string name)
    {
        var overlays=new List<Canvas>();
        foreach(var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
            if(canvas.isRootCanvas&&canvas.renderMode==RenderMode.ScreenSpaceOverlay)overlays.Add(canvas);
        RenderTexture previous = RenderTexture.active;
        Texture2D texture=null;
        try
        {
            // 先检查生产态可见性；这里只为离屏截图临时改为相机绘制，结束后恢复真正的Overlay模式。
            foreach(var overlay in overlays)
            {
                var canvas=overlay.GetComponent<Canvas>();
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=_camera;canvas.planeDistance=1;
                var scaler=canvas.GetComponent<CanvasScaler>();
                if(scaler!=null){scaler.enabled=false;scaler.enabled=true;}
            }
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text text in _canvas.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
            _camera.Render();
            RenderTexture.active = _target;
            texture = new Texture2D(_target.width, _target.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, _target.width, _target.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(Output, name + ".png"), texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=previous;
            if(texture!=null)UnityEngine.Object.Destroy(texture);
            foreach(var overlay in overlays)
            {
                if(overlay==null)continue;
                var canvas=overlay.GetComponent<Canvas>();
                canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;
                var scaler=canvas.GetComponent<CanvasScaler>();
                if(scaler!=null){scaler.enabled=false;scaler.enabled=true;}
            }
            Canvas.ForceUpdateCanvases();
        }
    }
}
