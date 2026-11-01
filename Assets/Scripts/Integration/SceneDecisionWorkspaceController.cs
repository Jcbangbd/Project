using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Vtp.VR.LocalAlgorithmExe;
using Vtp.VR.Rest;
using ZPMC.VR.Unity;

namespace Vtp.VR.Integration
{
    [DisallowMultipleComponent]
    public sealed class SceneDecisionWorkspaceController :
        MonoBehaviour
    {
        public static SceneDecisionWorkspaceController Instance
        {
            get;
            private set;
        }

        private const string GeneticAlgorithmKey =
            "genetic-algorithm-v2";
        private const string ParticleSwarmAlgorithmKey =
            "particle-swarm";
        private const string AlnsAlgorithmKey =
            "alns";

        [Header("三维工具栏")]
        [SerializeField]
        private GameObject sceneToolbar;

        [Header("原有岸桥任务设置面板")]

        [Tooltip("拖入原来的 Canvas/DischargeShip")]
        [SerializeField]
        private GameObject originalDischargeWindow;

        [Tooltip("拖入 Canvas/DischargeShip/PanelSTS")]
        [SerializeField]
        private GameObject originalStsPanel;

        [Header("算法选择")]
        [SerializeField]
        private CanvasGroup algorithmSelectionPanel;

        [SerializeField]
        private TMP_Text selectedAlgorithmText;

        [SerializeField]
        private TMP_Text statusText;

        [SerializeField]
        private Button runAlgorithmButton;

        [Header("计算锁定遮罩")]
        [SerializeField]
        private GameObject busyOverlay;

        [SerializeField]
        private TMP_Text busyText;

        [SerializeField]
        private bool pauseSceneDuringCalculation = true;

        private UMP_LevelInfo _levelInfo;
        private bool _entered3D;
        private bool _algorithmRunning;
        private AlgorithmRunOverlay _runOverlay;

        // S14要求学生主动选择算法，进入场景时不能默认替学生选择。
        private string _selectedAlgorithm = string.Empty;
        private AlgorithmAssessmentSelection
            _pendingAlgorithmSelection;
        private AlgorithmAssessmentController
            _algorithmAssessmentController;

        private float _oldTimeScale = 1f;

        private readonly Dictionary<Behaviour, bool>
            _disabledInputBehaviours =
                new Dictionary<Behaviour, bool>();

        private Button _openStsConfigButton;
        private Button _openAlgorithmButton;
        private Button _openExamplesButton;
        private GameObject _exampleViewerOverlay;
        private RectTransform _exampleListContent;
        private ScrollRect _exampleListScrollRect;
        private Scrollbar _exampleListVerticalScrollbar;
        private TMP_Text _exampleSourceText;
        private ExperimentExampleRuntimeCanvasView
            _exampleRuntimeCanvasView;
        private Coroutine _exampleStatusRefreshCoroutine;
        private bool _exampleViewerInputLocked;
        private readonly Dictionary<Behaviour, bool>
            _exampleViewerDisabledInputBehaviours =
                new Dictionary<Behaviour, bool>();
        private readonly List<ExampleStatusVisual>
            _exampleStatusVisuals =
                new List<ExampleStatusVisual>();
        private GameObject _persistentToolbarTemplate;
        private bool _sceneInputLocked;
        private bool _lockSceneUntilAlgorithmStarts;
        private CanvasGroup _runtimeAlgorithmSelectionPanel;
        private ScheduleResultAssessmentController
            _scheduleResultAssessmentController;
        private string _referenceAlgorithmKey = string.Empty;
        private bool _preparingExperiment;
        private bool _startSelectedAfterSceneLoad;
        private bool _experimentCompleted;
        private bool _postQuestionnaireStarted;
        private bool _scheduleResultConfirmed;
        private RmgExeExecutionResult _postExperimentExecution;
        private Coroutine _completionMonitor;
        private ExperimentCompletionView _completionView;

        public bool ExperimentCompleted => _experimentCompleted;

        public void OpenExperimentPreparation(UMP_LevelInfo levelInfo)
        {
            if (_algorithmRunning || levelInfo == null || !SceneNumericalExampleRuntime.HasExamples) return;
            CancelCompletionMonitor();
            _levelInfo = levelInfo;
            _entered3D = false;
            _preparingExperiment = true;
            _experimentCompleted = _postQuestionnaireStarted = false;
            _scheduleResultConfirmed = false;
            _postExperimentExecution = null;
            _selectedAlgorithm = string.Empty;
            _pendingAlgorithmSelection = null;
            EnsureRuntimeAlgorithmSelectionPanel();
            _algorithmAssessmentController.ResetForNewAttempt();
            _referenceAlgorithmKey = ResolveTaskReferenceAlgorithm(SceneNumericalExampleRuntime.CourseTaskId);
            _algorithmAssessmentController.SetReferenceAlgorithm(_referenceAlgorithmKey);
            StudentAssessmentRuntime.SetAlgorithmReference(_referenceAlgorithmKey, "参考算法采用任务配置；未配置时使用默认参考算法。");
            _algorithmAssessmentController.OpenParameterPreparation();
        }

        private void CancelCompletionMonitor()
        {
            if (_completionMonitor != null) StopCoroutine(_completionMonitor);
            _completionMonitor = null;
            if (_completionView != null) _completionView.Hide();
        }

        private sealed class ExampleStatusVisual
        {
            public int TaskIndex;
            public TMP_Text StatusText;
            public Image RowImage;
            public Color DefaultColor;
            public int LastState = int.MinValue;
        }

        private readonly HashSet<RectTransform>
            _shiftedTopControls =
                new HashSet<RectTransform>();

        public bool WantsReturnToIntroduction
        {
            get
            {
                // 三维准备阶段和动画执行阶段都应由左上角三角按钮
                // 返回学生选择任务后看到的五流程实验介绍页面。
                // 算法EXE正在计算时暂不打断计算。
                return _entered3D && !_algorithmRunning;
            }
        }

        private void Awake()
        {
            // 如果已经存在控制器，销毁整个重复的UI物体。
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            _persistentToolbarTemplate = sceneToolbar;

            // 切换到三维场景后保留 DecisionOverlayCanvas。
            DontDestroyOnLoad(gameObject);

            if (sceneToolbar != null)
            {
                sceneToolbar.SetActive(false);
            }

            ShowOriginalSTSPanel(false);

            SetPanelVisible(
                algorithmSelectionPanel,
                false);

            if (busyOverlay != null)
            {
                busyOverlay.SetActive(false);
            }

            RefreshAlgorithmDisplay();
        }

        private void OnDestroy()
        {
            CancelCompletionMonitor();
            if (_runOverlay != null)
            {
                _runOverlay.Finish();
                Destroy(_runOverlay.gameObject);
                _runOverlay = null;
            }
            if (_exampleRuntimeCanvasView != null &&
                _exampleRuntimeCanvasView.CloseButton != null)
            {
                _exampleRuntimeCanvasView.CloseButton.onClick.RemoveListener(
                    CloseExamplesPanel);
            }

            if (Instance == this)
            {
                StopExampleStatusRefresh();
                RestoreExampleViewerInput();
                RestoreSceneInput();
                Instance = null;
            }
        }

        public void PrepareForSceneLoad(
            UMP_LevelInfo levelInfo)
        {
            CancelCompletionMonitor();
            _experimentCompleted = _postQuestionnaireStarted = false;
            _scheduleResultConfirmed = false;
            _postExperimentExecution = null;
            StopExampleStatusRefresh();
            RestoreExampleViewerInput();
            _lockSceneUntilAlgorithmStarts = false;
            RestoreSceneInput();

            _levelInfo = levelInfo;
            _entered3D = false;
            _algorithmRunning = false;
            AutomaticQuayCraneScheduler.Clear();
            if (!_startSelectedAfterSceneLoad)
            {
                _selectedAlgorithm = string.Empty;
                _pendingAlgorithmSelection = null;
                _referenceAlgorithmKey = string.Empty;
            }

            if (_algorithmAssessmentController != null)
            {
                if (!_startSelectedAfterSceneLoad) _algorithmAssessmentController.ResetForNewAttempt();
                _algorithmAssessmentController.Hide();
            }

            if (_scheduleResultAssessmentController != null)
                _scheduleResultAssessmentController.Hide();

            if (sceneToolbar != null)
            {
                sceneToolbar.SetActive(false);
            }

            ShowOriginalSTSPanel(false);

            SetPanelVisible(
                algorithmSelectionPanel,
                false);

            if (busyOverlay != null)
            {
                busyOverlay.SetActive(false);
            }
        }

        public void Enter3DIdle(
            UMP_LevelInfo levelInfo)
        {
            if (sceneToolbar == null && _persistentToolbarTemplate != null)
            {
                sceneToolbar = _persistentToolbarTemplate;
            }

            _levelInfo = levelInfo;
            _entered3D = true;
            _algorithmRunning = false;
            AutomaticQuayCraneScheduler.Clear();

            if (sceneToolbar != null)
            {
                sceneToolbar.SetActive(true);
            }

            ArrangeToolbarBesideSpeedButton();
            SetDecisionToolbarSetupButtonsVisible(true);
            if (_openAlgorithmButton != null)
                _openAlgorithmButton.interactable = true;
            EnsurePersistentExampleViewer();
            RestoreUnsupportedSceneMaterials();

            EnsureRuntimeAlgorithmSelectionPanel();
            if (!_startSelectedAfterSceneLoad)
            {
                _selectedAlgorithm = string.Empty;
                _pendingAlgorithmSelection = null;
            }
            if (_algorithmAssessmentController != null)
            {
                if (!_startSelectedAfterSceneLoad) _algorithmAssessmentController.ResetForNewAttempt();
            }

            // 进入三维场景后保留原始画面，只锁定相机、人物和场景输入。
            // 不显示 BusyOverlay，避免出现黑色幕布。
            HideIdleBlockingOverlays();

            _lockSceneUntilAlgorithmStarts = true;
            DisableSceneInput();
            StartCoroutine(ApplyIdlePresentationAfterMenuCloses());

            int exampleCount =
                SceneNumericalExampleRuntime.HasExamples
                    ? SceneNumericalExampleRuntime
                        .Examples.Length
                    : 0;

            SetStatus(
                "已进入三维场景，当前算例数量：" +
                exampleCount +
                "。请选择算法；岸桥及源箱将自动分配。",
                Color.white);
            if (_startSelectedAfterSceneLoad) StartCoroutine(StartPreparedAlgorithmAfterMenuCloses());
        }

        private IEnumerator StartPreparedAlgorithmAfterMenuCloses()
        {
            yield return null;
            yield return null;
            if (!_startSelectedAfterSceneLoad || !_entered3D) yield break;
            _startSelectedAfterSceneLoad = false;
            RunSelectedAlgorithm();
        }

        public void OpenSTSConfiguration()
        {
            OpenAlgorithmSelection();
        }

        public void ConfirmSTSConfiguration()
        {
            ShowOriginalSTSPanel(false);
            OpenAlgorithmSelection();
        }

        public void CloseSTSConfiguration()
        {
            ShowOriginalSTSPanel(false);
        }

        public void OpenAlgorithmSelection()
        {
            if (!_entered3D || _algorithmRunning || _postExperimentExecution != null)
            {
                return;
            }

            ShowOriginalSTSPanel(false);

            EnsureRuntimeAlgorithmSelectionPanel();
            EnsureAlgorithmReferenceAndOpen();
            EnsurePersistentExampleViewer();
        }

        private void EnsureAlgorithmReferenceAndOpen()
        {
            if (_algorithmAssessmentController == null)
            {
                SetPanelVisible(algorithmSelectionPanel, true);
                return;
            }

            string taskId = SceneNumericalExampleRuntime.CourseTaskId;
            if (string.IsNullOrWhiteSpace(taskId))
                taskId = "kc1";
            _referenceAlgorithmKey =
                ResolveTaskReferenceAlgorithm(taskId);

            StudentAssessmentRuntime.SetAlgorithmReference(
                _referenceAlgorithmKey,
                "参考算法优先采用任务配置，未配置时使用默认参考算法。");
            _algorithmAssessmentController.SetReferenceAlgorithm(
                _referenceAlgorithmKey);
            if (_algorithmAssessmentController.ParametersPrepared) _algorithmAssessmentController.Open();
            else _algorithmAssessmentController.OpenParameterPreparation();
            SetStatus(
                "请先设置算法参数，再选择本次算法及原因并进入实验。",
                Color.black);
        }

        private static string ResolveTaskReferenceAlgorithm(
            string taskId)
        {
            VrTaskSummaryDto[] tasks = RemoteTaskRuntime.AvailableTasks;
            if (tasks != null)
            {
                for (int index = 0; index < tasks.Length; index++)
                {
                    VrTaskSummaryDto task = tasks[index];
                    if (task == null ||
                        !string.Equals(
                            task.taskId,
                            taskId,
                            StringComparison.OrdinalIgnoreCase) ||
                        task.requiredAlgorithm == null)
                    {
                        continue;
                    }

                    string normalized = NormalizeReferenceAlgorithmKey(
                        task.requiredAlgorithm.algorithmId);
                    if (!string.IsNullOrWhiteSpace(normalized))
                        return normalized;
                }
            }

            // ZIP本地算例只用于联调；沿用项目当前已部署的遗传算法作为参考。
            return GeneticAlgorithmKey;
        }

        private static string NormalizeReferenceAlgorithmKey(string value)
        {
            string normalized = (value ?? string.Empty)
                .Trim()
                .ToLowerInvariant();
            if (normalized == "ga" ||
                normalized.Contains("genetic") ||
                normalized.Contains("遗传"))
                return GeneticAlgorithmKey;
            if (normalized == "pso" ||
                normalized.Contains("particle") ||
                normalized.Contains("粒子"))
                return ParticleSwarmAlgorithmKey;
            if (normalized == "alns" ||
                normalized.Contains("adaptive") ||
                normalized.Contains("邻域"))
                return AlnsAlgorithmKey;
            return string.Empty;
        }

        public void PrepareReturnToIntroduction()
        {
            CancelCompletionMonitor();
            _preparingExperiment = _startSelectedAfterSceneLoad = false;
            _experimentCompleted = _postQuestionnaireStarted = false;
            _scheduleResultConfirmed = false;
            _postExperimentExecution = null;
            _entered3D = false;
            _algorithmRunning = false;
            _lockSceneUntilAlgorithmStarts = false;

            RestoreSceneInput();
            StopExampleStatusRefresh();
            RestoreExampleViewerInput();
            ShowOriginalSTSPanel(false);
            SetPanelVisible(algorithmSelectionPanel, false);

            if (_exampleViewerOverlay != null)
            {
                _exampleViewerOverlay.SetActive(false);
            }

            if (busyOverlay != null)
            {
                busyOverlay.SetActive(false);
            }

            if (_scheduleResultAssessmentController != null)
                _scheduleResultAssessmentController.Hide();

            if (sceneToolbar != null)
            {
                sceneToolbar.SetActive(false);
            }
        }

        public void ShowExperimentIntroduction()
        {
            SceneExampleViewer[] viewers =
                FindObjectsOfType<SceneExampleViewer>(true);

            foreach (SceneExampleViewer viewer in viewers)
            {
                if (viewer == null ||
                    !viewer.gameObject.scene.IsValid())
                {
                    continue;
                }

                viewer.ShowIntroductionFrom3D();
                return;
            }

            Debug.LogError(
                "[SceneDecisionWorkspace] 找不到SceneExampleViewer，" +
                "无法返回实验介绍页面。");
        }

        public void CloseAlgorithmSelection()
        {
            if (_algorithmRunning)
            {
                return;
            }

            if (_algorithmAssessmentController != null)
            {
                _algorithmAssessmentController.Hide();
            }
            else
            {
                SetPanelVisible(
                    algorithmSelectionPanel,
                    false);
            }
            if (_preparingExperiment && !_entered3D)
            {
                _preparingExperiment = false;
                ShowExperimentIntroduction();
            }
        }

        private void OnAlgorithmAssessmentConfirmed(
            AlgorithmAssessmentSelection selection)
        {
            if (selection == null ||
                selection.Parameters == null)
            {
                ShowError("算法选择或参数内容不完整。");
                return;
            }

            _pendingAlgorithmSelection = selection;
            _selectedAlgorithm = selection.AlgorithmKey;
            StudentAssessmentRuntime.RecordAlgorithmSelection(selection);

            SetStatus(
                "已选择" + selection.AlgorithmName +
                "，原因和参数已确认，正在准备执行算法。",
                Color.black);
            if (_preparingExperiment && !_entered3D)
            {
                _preparingExperiment = false;
                _startSelectedAfterSceneLoad = true;
                _levelInfo.OpenLevelPreview();
                return;
            }
            RunSelectedAlgorithm();
        }

        public void SelectAlgorithm(
            string algorithmKey)
        {
            _selectedAlgorithm =
                string.IsNullOrWhiteSpace(algorithmKey)
                    ? string.Empty
                    : algorithmKey.Trim();

            RefreshAlgorithmDisplay();
        }

        public void RunSelectedAlgorithm()
        {
            if (_algorithmRunning || _postExperimentExecution != null)
            {
                return;
            }

            if (!_entered3D)
            {
                ShowError("尚未进入三维场景。");
                return;
            }

            if (!IsSupportedAlgorithm(
                    _selectedAlgorithm))
            {
                ShowError(
                    "请选择遗传算法、粒子群算法或自适应大邻域搜索算法。");
                return;
            }

            if (_pendingAlgorithmSelection == null ||
                _pendingAlgorithmSelection.Parameters == null)
            {
                ShowError(
                    "请先在算法页面完成原因选择和全部参数填写。");
                return;
            }

            string taskId =
                SceneNumericalExampleRuntime.CourseTaskId;

            if (string.IsNullOrWhiteSpace(taskId))
            {
                taskId = "kc1";
            }

            AutomaticQuayCraneScheduler.Clear();

            string exeAlgorithm =
                _pendingAlgorithmSelection.ExeAlgorithmKey;
            RmgExeCoordinator coordinator =
                RmgExeCoordinator.EnsureInstance();

            _algorithmRunning = true;

            // 计算开始后先收起算法选择面板，避免它盖住计算遮罩。
            SetPanelVisible(
                algorithmSelectionPanel,
                false);

            if (!SetBusy(
                true,
                "正在准备本次算例与设备资源……"))
            {
                _algorithmRunning = false;
                return;
            }

            // 先让原生模态框完成一帧显示，再开始工作簿桥接等同步准备。
            // 不添加固定等待时长，也不额外调用其他算法。
            StartCoroutine(RunSelectedAlgorithmAfterOverlay(coordinator, taskId, exeAlgorithm));
        }

        private IEnumerator RunSelectedAlgorithmAfterOverlay(
            RmgExeCoordinator coordinator, string taskId, string exeAlgorithm)
        {
            Canvas.ForceUpdateCanvases();
            yield return null;

            // Coordinator只使用查看页面已经缓存并按实验类型筛选的算例；
            // 它会把同一批数据桥接给EXE，完成后转换为原有调度模型。
            coordinator.Run(
                taskId,
                exeAlgorithm,
                _pendingAlgorithmSelection.Parameters,

                OnAlgorithmSucceeded,

                progress =>
                {
                    if (_runOverlay != null) _runOverlay.Report(progress);
                },

                error =>
                {
                    OnAlgorithmFailed(error);
                });
        }

        private void OnAlgorithmSucceeded(
            RmgExeExecutionResult execution)
        {
            SchedulingResultDto result = execution == null
                ? null
                : execution.Schedule;
            string sourceMessage = execution == null
                ? null
                : execution.SourceMessage;
            int operationCount =
                result != null &&
                result.operations != null
                    ? result.operations.Length
                    : 0;

            _algorithmRunning = false;

            if (operationCount <= 0)
            {
                OnAlgorithmFailed(
                    "算法没有返回有效调度序列。");
                return;
            }

            // RMG序列完成后，根据实际源箱和运输距离自动分配岸桥。
            List<DischargeTargetSlot> dischargeTargets;
            string targetError;
            bool prepared;
            try
            {
                prepared = AutomaticQuayCraneScheduler.TryPrepare(result, out targetError);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                AutomaticQuayCraneScheduler.Clear();
                prepared = false;
                targetError = exception.Message;
            }
            if (!prepared)
            {
                OnAlgorithmFailed(
                    "调度结果校验或岸桥自动分配失败：" +
                    targetError);
                return;
            }

            dischargeTargets = AutomaticQuayCraneScheduler.Current.ConvertAll(task => task.Target);
            StudentAssessmentRuntime.RecordAlgorithmExecution(true, null);

            SetPanelVisible(
                algorithmSelectionPanel,
                false);

            ShowOriginalSTSPanel(false);

            // 作业开始后不再允许修改算法和岸桥设置，
            // 但“查看算例”在整个三维推演期间都应保留。
            ArrangeToolbarBesideSpeedButton();
            SetDecisionToolbarSetupButtonsVisible(false);

            SetStatus(
                "算法完成，本次执行 " +
                dischargeTargets.Count +
                " 条卸船任务，请先核对调度结果；提交后开始三维调度。" +
                (SceneNumericalExampleRuntime.IsLocalFallback || string.IsNullOrWhiteSpace(sourceMessage)
                    ? string.Empty
                    : " " + sourceMessage),
                new Color(0.2f, 0.8f, 0.3f));

            if (_levelInfo == null)
            {
                SetBusy(false, null);
                ShowError(
                    "找不到当前实验的UMP_LevelInfo。");
                return;
            }

            // 执行算法 -> 核对调度结果 -> 三维调度。核对提交前不生成设备作业。
            _postExperimentExecution = execution;
            _experimentCompleted = _postQuestionnaireStarted = false;
            _scheduleResultConfirmed = false;
            _lockSceneUntilAlgorithmStarts = true;
            SetBusy(false, null);
            CancelCompletionMonitor();
            OpenScheduleResultAssessment(execution);
        }

        private IEnumerator WaitForExperimentCompletion(int previousRevision, int expectedCount)
        {
            float preparationDeadline = Time.realtimeSinceStartup + 30;
            float motionEnd = 0;
            bool armed = false;
            var wait = new WaitForSecondsRealtime(.25f);
            while (_entered3D && _postExperimentExecution != null)
            {
                int total, completed; float taskEnd;
                var manager = global::TaskManager.Instance;
                bool currentTasks = manager != null && manager.TaskRevision > previousRevision &&
                    manager.TryGetCompletion(out total, out completed, out taskEnd);
                if (currentTasks)
                {
                    manager.TryGetCompletion(out total, out completed, out taskEnd);
                    if (!armed)
                    {
                        if (total != expectedCount)
                        { ShowError("生成的实验任务数与调度序列不一致，不能进入实验后答题。"); yield break; }
                        motionEnd = manager.MotionEndTime;
                        armed = true;
                    }
                    if (completed == total && TimeHelper.Instance != null &&
                        TimeHelper.Instance.RectifiedTime >= Mathf.Max(taskEnd, motionEnd) &&
                        EventScripter.GetGlobalInstance().recordMarshal.IsEmpty)
                    {
                        yield return null; // 让最后一帧设备更新完成，不在同帧提前冻结。
                        _completionMonitor = null;
                        _experimentCompleted = true;
                        ShowExperimentCompletion();
                        yield break;
                    }
                }
                else if (!armed && Time.realtimeSinceStartup > preparationDeadline)
                { ShowError("实验任务未成功生成，请返回实验介绍后重试。"); yield break; }
                yield return wait;
            }
            _completionMonitor = null;
        }

        public void ShowExperimentCompletion()
        {
            if (!_experimentCompleted || _postExperimentExecution == null) return;
            _postQuestionnaireStarted = false;
            CloseExamplesPanel();
            DisableSceneInput();
            if (_completionView == null)
            {
                var prefab = Resources.Load<ExperimentCompletionView>("ExperimentCompletionView");
                if (prefab == null) { ShowError("缺少实验结束页面 ExperimentCompletionView。"); return; }
                _completionView = Instantiate(prefab, ExperimentAssessmentCanvasHost.GetLayer(
                    ExperimentAssessmentCanvasHost.GetOrCreate(this), "ExperimentCompletionLayer"), false);
            }
            _completionView.Show(_postExperimentExecution.Schedule.operations.Length, BeginPostExperimentQuestionnaire);
        }

        private void BeginPostExperimentQuestionnaire()
        {
            if (!_experimentCompleted || _postQuestionnaireStarted) return;
            foreach (var viewer in FindObjectsOfType<SceneExampleViewer>(true))
                if (viewer.gameObject.scene.IsValid() && viewer.OpenPostExperimentQuestionnaire())
                { _postQuestionnaireStarted = true; return; }
            ShowError("无法打开实验结束答题页面。");
            ShowExperimentCompletion();
        }

        public void CompletePostExperimentAssessment()
        {
            if (!_experimentCompleted || !_postQuestionnaireStarted || !_scheduleResultConfirmed ||
                !PreExperimentAssessmentRuntime.IsCompleted) return;
            string path = StudentAssessmentRuntime.SaveAndSubmit();
            Debug.Log("[Assessment] 调度核对与实验后八题已统一保存，本地记录：" + path);
            PrepareReturnToIntroduction();
            if (UMP_Manager.Instance != null) UMP_Manager.Instance.ChangeWindow(1);
            ShowExperimentIntroduction();
        }

        private void OnAlgorithmFailed(
            string error)
        {
            _algorithmRunning = false;
            StudentAssessmentRuntime.RecordAlgorithmExecution(
                false,
                error);

            // 报错后解除页面锁定，允许重新操作。
            SetBusy(false, null);

            // 算法失败后重新显示选择面板，便于用户查看错误并重试。
            if (_algorithmAssessmentController != null)
            {
                _algorithmAssessmentController.OpenWithError(
                    "算法运行失败：" +
                    (string.IsNullOrWhiteSpace(error)
                        ? "未知错误"
                        : error));
            }
            else
            {
                SetPanelVisible(
                    algorithmSelectionPanel,
                    true);
            }

            ShowError(
                "算法运行失败：" +
                (string.IsNullOrWhiteSpace(error)
                    ? "未知错误"
                    : error));
        }

        private int CountSelectedSourceContainers()
        {
            return GetSelectedSourceContainers().Count;
        }

        private static List<CellCode>
            GetSelectedSourceContainers()
        {
            List<CellCode> result = new List<CellCode>();

            foreach (
                KeyValuePair<int, InfoSTS> pair
                in InfoSTS.DischargeMarshal)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                List<CellCode> selectedCells =
                    pair.Value.BuildCode();

                if (selectedCells != null)
                {
                    result.AddRange(selectedCells);
                }
            }

            return result;
        }

        private void RefreshAlgorithmDisplay()
        {
            bool supported =
                IsSupportedAlgorithm(
                    _selectedAlgorithm);

            if (selectedAlgorithmText != null)
            {
                selectedAlgorithmText.text =
                    "当前选择：" +
                    GetAlgorithmDisplayName(
                        _selectedAlgorithm);
            }

            if (runAlgorithmButton != null)
            {
                runAlgorithmButton.interactable =
                    !_algorithmRunning &&
                    supported;
            }

            if (!supported)
            {
                SetStatus(
                    "请选择可用的调度算法。",
                    Color.black);
            }
            else if (_runtimeAlgorithmSelectionPanel != null &&
                     _runtimeAlgorithmSelectionPanel.alpha > 0.5f)
            {
                SetStatus(
                    "已选择" +
                    GetAlgorithmDisplayName(_selectedAlgorithm) +
                    "，请点击“执行所选算法”。",
                    Color.black);
            }
        }

        private static bool IsSupportedAlgorithm(
            string algorithmKey)
        {
            return string.Equals(
                       algorithmKey,
                       GeneticAlgorithmKey,
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       algorithmKey,
                       ParticleSwarmAlgorithmKey,
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       algorithmKey,
                       AlnsAlgorithmKey,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static string ToExeAlgorithmKey(
            string algorithmKey)
        {
            if (string.Equals(
                    algorithmKey,
                    GeneticAlgorithmKey,
                    StringComparison.OrdinalIgnoreCase))
                return "ga";
            if (string.Equals(
                    algorithmKey,
                    ParticleSwarmAlgorithmKey,
                    StringComparison.OrdinalIgnoreCase))
                return "pso";
            if (string.Equals(
                    algorithmKey,
                    AlnsAlgorithmKey,
                    StringComparison.OrdinalIgnoreCase))
                return "alns";
            return string.Empty;
        }

        private static string GetAlgorithmDisplayName(
            string algorithmKey)
        {
            if (string.Equals(
                    algorithmKey,
                    GeneticAlgorithmKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                return "遗传算法";
            }

            if (string.Equals(
                    algorithmKey,
                    ParticleSwarmAlgorithmKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                return "粒子群优化算法";
            }

            if (string.Equals(
                    algorithmKey,
                    AlnsAlgorithmKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                return "自适应大邻域搜索算法";
            }

            return "未选择";
        }

        private bool SetBusy(
            bool busy,
            string message)
        {
            if (busyOverlay != null) busyOverlay.SetActive(false);
            if (_runOverlay == null && busy)
            {
                var prefab = Resources.Load<AlgorithmRunOverlay>("AlgorithmRunOverlay");
                // 必须是独立根Canvas。嵌入旧Canvas会继承零尺寸/零缩放和低排序，导致执行中完全看不到。
                if (prefab != null) _runOverlay = Instantiate(prefab);
            }
            if (busy && _runOverlay == null)
            {
                ShowError("缺少算法运行遮罩预制体 AlgorithmRunOverlay，已停止启动算法。请检查项目资源导入。");
                return false;
            }
            if (_runOverlay != null)
            {
                if (busy)
                {
                    if (!_runOverlay.gameObject.activeSelf)
                        _runOverlay.Begin(GetAlgorithmDisplayName(_selectedAlgorithm), SceneNumericalExampleRuntime.Examples.Length);
                    else if (!_runOverlay.IsVisible)
                        _runOverlay.EnsureFullScreenCanvas();
                    _runOverlay.Report(message);
                    if (!_runOverlay.IsVisible)
                    {
                        _runOverlay.Finish();
                        ShowError("算法运行遮罩未能建立有效的全屏Canvas，已停止启动算法。");
                        return false;
                    }
                }
                else _runOverlay.Finish();
            }

            if (busy)
            {
                DisableSceneInput();
            }
            else if (_lockSceneUntilAlgorithmStarts)
            {
                // 算法尚未成功时，只恢复UI操作，三维相机和人物继续锁定。
                DisableSceneInput();
            }
            else
            {
                RestoreSceneInput();
            }
            return true;
        }

        private void DisableSceneInput()
        {
            if (!_sceneInputLocked)
            {
                _sceneInputLocked = true;
                _disabledInputBehaviours.Clear();

                if (pauseSceneDuringCalculation)
                {
                    _oldTimeScale = Time.timeScale;
                }
            }

            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.DoPause();
            }

            MonoBehaviour[] behaviours =
                FindObjectsOfType<MonoBehaviour>();

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null)
                {
                    continue;
                }

                string typeName =
                    behaviour.GetType().Name;

                bool isInputBehaviour =
                    typeName == "FreeCamera" ||
                    typeName == "FreeCameraSplit" ||
                    typeName == "FirstPersonController" ||
                    typeName == "GameController";

                if (!isInputBehaviour)
                {
                    continue;
                }

                if (!_disabledInputBehaviours.ContainsKey(behaviour))
                {
                    _disabledInputBehaviours[behaviour] =
                        behaviour.enabled;
                }

                behaviour.enabled = false;
            }

            if (pauseSceneDuringCalculation)
            {
                Time.timeScale = 0f;
            }
        }

        /// <summary>
        /// UMP_Manager 会在 UserLoadedScene 返回后关闭旧菜单，并顺带调用一次
        /// CameraManager.UnPause。必须等它执行完再重新锁定，否则镜头会再次下坠。
        /// </summary>
        private IEnumerator ApplyIdlePresentationAfterMenuCloses()
        {
            yield return null;

            if (!_entered3D || !_lockSceneUntilAlgorithmStarts)
            {
                yield break;
            }

            ArrangeToolbarBesideSpeedButton();
            HideIdleBlockingOverlays();
            RestoreUnsupportedSceneMaterials();
            DisableSceneInput();
        }

        /// <summary>
        /// 老项目升级到Unity 2022后，少数地图材质的Shader可能失效，
        /// Unity会把这些物体渲染成纯粉色。这里只替换已经失效的材质，
        /// 正常材质保持不变。
        /// </summary>
        private void RestoreUnsupportedSceneMaterials()
        {
            Shader fallbackShader = Shader.Find("Standard");
            if (fallbackShader == null)
            {
                Debug.LogWarning(
                    "[SceneMaterial] 找不到Standard Shader，无法修复粉色地图材质。");
                return;
            }

            int repairedCount = 0;
            Renderer[] renderers =
                FindObjectsOfType<Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null ||
                    !renderer.gameObject.scene.IsValid())
                {
                    continue;
                }

                Material[] materials = renderer.materials;
                bool rendererChanged = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    Material oldMaterial = materials[i];
                    if (!IsUnsupportedMaterial(oldMaterial))
                    {
                        continue;
                    }

                    Material replacement =
                        CreateFallbackMaterial(
                            oldMaterial,
                            fallbackShader);

                    materials[i] = replacement;
                    rendererChanged = true;
                    repairedCount++;

                    Debug.LogWarning(
                        "[SceneMaterial] 已修复粉色材质，物体=" +
                        renderer.gameObject.name +
                        "，原材质=" +
                        (oldMaterial != null
                            ? oldMaterial.name
                            : "空材质"));
                }

                if (rendererChanged)
                {
                    renderer.materials = materials;
                }
            }

            Terrain[] terrains =
                FindObjectsOfType<Terrain>(true);

            foreach (Terrain terrain in terrains)
            {
                if (terrain == null ||
                    !terrain.gameObject.scene.IsValid() ||
                    !IsUnsupportedMaterial(terrain.materialTemplate))
                {
                    continue;
                }

                // materialTemplate为空时，Terrain会使用Unity内置的兼容材质。
                terrain.materialTemplate = null;
                repairedCount++;
                Debug.LogWarning(
                    "[SceneMaterial] 已恢复Terrain默认材质，物体=" +
                    terrain.gameObject.name);
            }

            if (repairedCount > 0)
            {
                Debug.Log(
                    "[SceneMaterial] 共修复 " +
                    repairedCount +
                    " 个失效地图材质。");
            }
        }

        private static bool IsUnsupportedMaterial(
            Material material)
        {
            if (material == null)
            {
                return false;
            }

            Shader shader = material.shader;
            return shader == null ||
                   !shader.isSupported ||
                   string.Equals(
                       shader.name,
                       "Hidden/InternalErrorShader",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static Material CreateFallbackMaterial(
            Material source,
            Shader fallbackShader)
        {
            Material replacement =
                new Material(fallbackShader);

            replacement.name =
                (source != null
                    ? source.name
                    : "MissingMaterial") +
                "_RuntimeFixed";

            if (source == null)
            {
                return replacement;
            }

            if (source.HasProperty("_MainTex"))
            {
                replacement.mainTexture =
                    source.GetTexture("_MainTex");
                replacement.mainTextureOffset =
                    source.GetTextureOffset("_MainTex");
                replacement.mainTextureScale =
                    source.GetTextureScale("_MainTex");
            }

            if (source.HasProperty("_Color"))
            {
                replacement.color =
                    source.GetColor("_Color");
            }

            return replacement;
        }

        /// <summary>
        /// 空闲设置阶段只能隐藏遮挡画面的层，不能关闭整个决策 Canvas，
        /// 否则顶部三个入口按钮也会一起消失。
        /// </summary>
        private void HideIdleBlockingOverlays()
        {
            if (busyOverlay != null)
            {
                busyOverlay.SetActive(false);
            }

            if (_exampleViewerOverlay != null)
            {
                _exampleViewerOverlay.SetActive(false);
            }

            SetPanelVisible(algorithmSelectionPanel, false);

            Transform[] transforms =
                FindObjectsOfType<Transform>(true);
            foreach (Transform item in transforms)
            {
                if (item == null ||
                    !item.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (string.Equals(
                        item.name,
                        "BusyOverlay",
                        StringComparison.OrdinalIgnoreCase))
                {
                    item.gameObject.SetActive(false);
                }
            }
        }

        private void RestoreSceneInput()
        {
            if (!_sceneInputLocked)
            {
                return;
            }

            if (pauseSceneDuringCalculation)
            {
                Time.timeScale = _oldTimeScale;
            }

            foreach (
                KeyValuePair<Behaviour, bool> pair
                in _disabledInputBehaviours)
            {
                if (pair.Key != null)
                {
                    pair.Key.enabled = pair.Value;
                }
            }

            _disabledInputBehaviours.Clear();

            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.UnPause();
            }

            _sceneInputLocked = false;
        }

        private static void SetPanelVisible(
            CanvasGroup panel,
            bool visible)
        {
            if (panel == null)
            {
                return;
            }

            // 保持激活以保证InfoSTS完成初始化。
            panel.gameObject.SetActive(true);
            panel.alpha = visible ? 1f : 0f;
            panel.interactable = visible;
            panel.blocksRaycasts = visible;
        }

        private void ShowOriginalSTSPanel(bool visible)
        {
            if (originalDischargeWindow == null)
            {
                if (visible)
                {
                    Debug.LogError(
                        "[SceneDecisionWorkspace] " +
                        "没有配置岸桥交互窗口。");
                }

                return;
            }

            // 显示岸桥窗口时，保证内部的 PanelSTS 也是启用状态。
            if (visible && originalStsPanel != null)
            {
                originalStsPanel.SetActive(true);
            }

            // 这里只能控制第二个 DischargeShipInteractionWindow。
            originalDischargeWindow.SetActive(visible);

            // 打开岸桥窗口时隐藏悬浮工具栏；
            // 关闭岸桥窗口时重新显示工具栏。
            if (sceneToolbar != null)
            {
                sceneToolbar.SetActive(
                    _entered3D &&
                    !visible &&
                    !_algorithmRunning);
            }
        }

        /// <summary>
        /// 将三个决策按钮放到三维场景顶部，尺寸沿用原工具栏，
        /// 外观统一使用答题页面的浅色 Sci-Fi 按钮主题。
        /// 原 SceneToolbar 带有全屏区域，进入三维场景后将其隐藏，避免黑色幕布。
        /// </summary>
        private void ArrangeToolbarBesideSpeedButton()
        {
            Button speedButton = FindSpeedButton();
            if (speedButton == null)
            {
                Debug.LogWarning(
                    "[SceneDecisionWorkspace] 找不到推演倍速按钮，" +
                    "暂时保留原三维工具栏位置。");
                return;
            }

            RectTransform speedRect =
                speedButton.transform as RectTransform;
            RectTransform speedParent =
                speedButton.transform.parent as RectTransform;
            if (speedRect == null || speedParent == null)
            {
                return;
            }

            // 为右侧FPS区域腾出空间：视图设置、推演倍速及X倍速
            // 作为一个整体左移，原有三个决策按钮仍接在倍速文字后面。
            ShiftOriginalTopControlsLeft(
                speedButton,
                speedParent,
                speedRect);

            if (sceneToolbar != null &&
                sceneToolbar.name == "TopDecisionToolbar")
            {
                ApplyDecisionToolbarButtonStyles();
                sceneToolbar.SetActive(true);
                return;
            }

            GameObject oldToolbar = sceneToolbar;
            if (_persistentToolbarTemplate == null)
            {
                _persistentToolbarTemplate = oldToolbar;
            }

            if (oldToolbar != null)
            {
                oldToolbar.SetActive(false);
            }

            Transform existing =
                speedButton.transform.parent.Find("TopDecisionToolbar");
            GameObject toolbarObject;
            if (existing != null)
            {
                toolbarObject = existing.gameObject;
            }
            else
            {
                toolbarObject =
                    CreateWorkspaceUiObject(
                        "TopDecisionToolbar",
                        speedButton.transform.parent);
            }

            RectTransform toolbarRect =
                toolbarObject.GetComponent<RectTransform>();

            float buttonWidth =
                Mathf.Max(1f, speedRect.rect.width);
            float buttonHeight =
                Mathf.Max(1f, speedRect.rect.height);
            const float spacing = 8f;

            // “推演倍速”右侧还有单独的 X1/X2... 文本。
            // 旧实现只按按钮本体计算，导致新工具栏压在倍速文字上。
            float speedSuffixWidth =
                GetSpeedSuffixWidth(speedParent, speedRect);

            // 某些旧版场景中的倍速文字RectTransform宽度为0，
            // CalculateRelativeRectTransformBounds无法取得它的真实宽度。
            // 固定预留一段空间，可完整容纳X1到X16，并与新按钮留出间隔。
            const float minimumSpeedSuffixWidth = 56f;
            speedSuffixWidth = Mathf.Max(
                speedSuffixWidth,
                minimumSpeedSuffixWidth);

            toolbarRect.anchorMin = speedRect.anchorMin;
            toolbarRect.anchorMax = speedRect.anchorMax;
            toolbarRect.pivot = new Vector2(0f, speedRect.pivot.y);
            toolbarRect.sizeDelta =
                new Vector2(
                    buttonWidth * 3f + spacing * 2f,
                    buttonHeight);
            toolbarRect.anchoredPosition =
                speedRect.anchoredPosition +
                new Vector2(
                    (1f - speedRect.pivot.x) * buttonWidth +
                    speedSuffixWidth +
                    spacing,
                    0f);
            toolbarRect.localScale = Vector3.one;
            toolbarRect.localRotation = Quaternion.identity;

            LayoutGroup[] oldLayouts =
                toolbarObject.GetComponents<LayoutGroup>();
            foreach (LayoutGroup oldLayout in oldLayouts)
            {
                Destroy(oldLayout);
            }

            HorizontalLayoutGroup layout =
                toolbarObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            for (int index = toolbarObject.transform.childCount - 1;
                 index >= 0;
                 index--)
            {
                Destroy(toolbarObject.transform.GetChild(index).gameObject);
            }

            _openStsConfigButton = null;

            _openAlgorithmButton =
                CreateTopToolbarButton(
                    toolbarObject.transform,
                    "OpenAlgorithmButton",
                    "算法选择",
                    speedButton,
                    OpenAlgorithmSelection,
                    buttonWidth,
                    buttonHeight);

            _openExamplesButton =
                CreateTopToolbarButton(
                    toolbarObject.transform,
                    "OpenExamplesButton",
                    "查看算例",
                    speedButton,
                    OpenExamplesPanel,
                    buttonWidth,
                    buttonHeight);

            sceneToolbar = toolbarObject;
            ApplyDecisionToolbarButtonStyles();
            sceneToolbar.SetActive(true);

            if (busyOverlay != null)
            {
                busyOverlay.SetActive(false);
            }
        }

        private void ShiftOriginalTopControlsLeft(
            Button speedButton,
            RectTransform speedParent,
            RectTransform speedRect)
        {
            const float shiftX = -80f;

            ShiftRectOnce(speedRect, shiftX);

            // X1/X2/X4/X8/X16在旧Canvas中是独立文字，必须和按钮一起移动。
            TMP_Text[] tmpTexts =
                speedParent.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text textComponent in tmpTexts)
            {
                if (textComponent == null ||
                    textComponent.transform.IsChildOf(speedRect) ||
                    !IsSpeedSuffix(textComponent.text))
                {
                    continue;
                }

                ShiftRectOnce(
                    textComponent.rectTransform,
                    shiftX);
            }

            Text[] legacyTexts =
                speedParent.GetComponentsInChildren<Text>(true);
            foreach (Text textComponent in legacyTexts)
            {
                if (textComponent == null ||
                    textComponent.transform.IsChildOf(speedRect) ||
                    !IsSpeedSuffix(textComponent.text))
                {
                    continue;
                }

                ShiftRectOnce(
                    textComponent.transform as RectTransform,
                    shiftX);
            }

            Button viewSettingsButton =
                FindTopButtonByLabel("视图设置");
            if (viewSettingsButton != null &&
                viewSettingsButton != speedButton)
            {
                ShiftRectOnce(
                    viewSettingsButton.transform as RectTransform,
                    shiftX);
            }
        }

        private void ShiftRectOnce(
            RectTransform rect,
            float shiftX)
        {
            if (rect == null ||
                !_shiftedTopControls.Add(rect))
            {
                return;
            }

            rect.anchoredPosition +=
                new Vector2(shiftX, 0f);
        }

        private Button FindTopButtonByLabel(
            string label)
        {
            Button[] buttons = FindObjectsOfType<Button>(true);
            foreach (Button button in buttons)
            {
                if (button == null ||
                    !button.gameObject.scene.IsValid() ||
                    button.transform.IsChildOf(transform))
                {
                    continue;
                }

                TMP_Text tmp =
                    button.GetComponentInChildren<TMP_Text>(true);
                if (tmp != null && tmp.text.Contains(label))
                {
                    return button;
                }

                Text legacy =
                    button.GetComponentInChildren<Text>(true);
                if (legacy != null && legacy.text.Contains(label))
                {
                    return button;
                }
            }

            return null;
        }

        private void SetDecisionToolbarSetupButtonsVisible(
            bool setupButtonsVisible)
        {
            if(sceneToolbar!=null) foreach(var button in sceneToolbar.GetComponentsInChildren<Button>(true))
                if(button.name=="OpenSTSConfigButton")button.gameObject.SetActive(false);
            if (sceneToolbar == null ||
                sceneToolbar.name != "TopDecisionToolbar")
            {
                return;
            }

            if (_openStsConfigButton == null)
            {
                Transform child =
                    sceneToolbar.transform.Find("OpenSTSConfigButton");
                if (child != null)
                {
                    _openStsConfigButton = child.GetComponent<Button>();
                }
            }

            if (_openAlgorithmButton == null)
            {
                Transform child =
                    sceneToolbar.transform.Find("OpenAlgorithmButton");
                if (child != null)
                {
                    _openAlgorithmButton = child.GetComponent<Button>();
                }
            }

            if (_openExamplesButton == null)
            {
                Transform child =
                    sceneToolbar.transform.Find("OpenExamplesButton");
                if (child != null)
                {
                    _openExamplesButton = child.GetComponent<Button>();
                }
            }

            if (_openStsConfigButton != null)
            {
                _openStsConfigButton.gameObject.SetActive(false);
            }

            if (_openAlgorithmButton != null)
            {
                _openAlgorithmButton.gameObject.SetActive(
                    setupButtonsVisible);
            }

            if (_openExamplesButton != null)
            {
                _openExamplesButton.gameObject.SetActive(true);
            }

            sceneToolbar.SetActive(true);
        }

        private static float GetSpeedSuffixWidth(
            RectTransform speedParent,
            RectTransform speedRect)
        {
            Bounds speedBounds =
                RectTransformUtility.CalculateRelativeRectTransformBounds(
                    speedParent,
                    speedRect);
            float rightEdge = speedBounds.max.x;

            TMP_Text[] tmpTexts =
                speedParent.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text textComponent in tmpTexts)
            {
                if (textComponent == null ||
                    textComponent.transform.IsChildOf(speedRect) ||
                    !IsSpeedSuffix(textComponent.text))
                {
                    continue;
                }

                Bounds bounds =
                    RectTransformUtility.CalculateRelativeRectTransformBounds(
                        speedParent,
                        textComponent.rectTransform);
                rightEdge = Mathf.Max(rightEdge, bounds.max.x);
            }

            Text[] legacyTexts =
                speedParent.GetComponentsInChildren<Text>(true);
            foreach (Text textComponent in legacyTexts)
            {
                if (textComponent == null ||
                    textComponent.transform.IsChildOf(speedRect) ||
                    !IsSpeedSuffix(textComponent.text))
                {
                    continue;
                }

                RectTransform textRect =
                    textComponent.transform as RectTransform;
                if (textRect == null)
                {
                    continue;
                }

                Bounds bounds =
                    RectTransformUtility.CalculateRelativeRectTransformBounds(
                        speedParent,
                        textRect);
                rightEdge = Mathf.Max(rightEdge, bounds.max.x);
            }

            return Mathf.Max(0f, rightEdge - speedBounds.max.x);
        }

        private static bool IsSpeedSuffix(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim().ToUpperInvariant();
            if (!normalized.StartsWith("X") || normalized.Length < 2)
            {
                return false;
            }

            for (int index = 1; index < normalized.Length; index++)
            {
                if (!char.IsDigit(normalized[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private Button FindSpeedButton()
        {
            Transform[] transforms =
                FindObjectsOfType<Transform>(true);

            // SceneStage1 的 Canvas 会被移动到 DontDestroyOnLoad。
            // 因此这里不能再用 activeScene 过滤，否则永远找不到顶部 Speed。
            foreach (Transform item in transforms)
            {
                if (item == null ||
                    !item.gameObject.scene.IsValid() ||
                    !item.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (item.name == "Speed")
                {
                    Button direct = item.GetComponent<Button>();
                    if (direct != null)
                    {
                        return direct;
                    }

                    Button child = item.GetComponentInChildren<Button>(true);
                    if (child != null)
                    {
                        return child;
                    }
                }
            }

            Button[] buttons = FindObjectsOfType<Button>(true);
            foreach (Button button in buttons)
            {
                if (button == null ||
                    !button.gameObject.scene.IsValid() ||
                    !button.gameObject.activeInHierarchy ||
                    button.transform.IsChildOf(transform))
                {
                    continue;
                }

                TMP_Text tmp =
                    button.GetComponentInChildren<TMP_Text>(true);
                if (tmp != null && tmp.text.Contains("推演倍速"))
                {
                    return button;
                }

                Text legacy =
                    button.GetComponentInChildren<Text>(true);
                if (legacy != null && legacy.text.Contains("推演倍速"))
                {
                    return button;
                }
            }

            return null;
        }

        private Button CreateTopToolbarButton(
            Transform parent,
            string objectName,
            string labelValue,
            Button styleSource,
            UnityAction clickAction,
            float width,
            float height)
        {
            GameObject buttonObject =
                CreateWorkspaceUiObject(objectName, parent);

            Image image = buttonObject.AddComponent<Image>();
            image.color = Color.white;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = styleSource.navigation;
            button.onClick.AddListener(clickAction);

            LayoutElement element = buttonObject.AddComponent<LayoutElement>();
            element.minWidth = width;
            element.preferredWidth = width;
            element.flexibleWidth = 0f;
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleHeight = 0f;

            TMP_Text sourceTmp =
                styleSource.GetComponentInChildren<TMP_Text>(true);
            Text sourceLegacy =
                styleSource.GetComponentInChildren<Text>(true);

            TMP_Text label =
                CreateWorkspaceText(
                    "Text",
                    buttonObject.transform,
                    labelValue,
                    sourceTmp != null
                        ? sourceTmp.fontSize
                        : sourceLegacy != null
                            ? sourceLegacy.fontSize
                            : 20f,
                    ExperimentUiTheme.PrimaryText);
            label.font = ExperimentUiTheme.Font != null
                ? ExperimentUiTheme.Font
                : sourceTmp != null
                    ? sourceTmp.font
                    : label.font;
            label.fontStyle = FontStyles.Bold;
            label.enableWordWrapping = false;
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = Mathf.Clamp(label.fontSize, 15f, 18f);
            StretchToParent(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(8f, 3f);
            label.rectTransform.offsetMax = new Vector2(-8f, -3f);

            ExperimentUiTheme.StyleButton(button, true);

            return button;
        }

        private void ApplyDecisionToolbarButtonStyles()
        {
            if (sceneToolbar == null)
                return;

            _openStsConfigButton = ResolveToolbarButton(
                _openStsConfigButton,
                "OpenSTSConfigButton");
            _openAlgorithmButton = ResolveToolbarButton(
                _openAlgorithmButton,
                "OpenAlgorithmButton");
            _openExamplesButton = ResolveToolbarButton(
                _openExamplesButton,
                "OpenExamplesButton");

            ApplyDecisionToolbarButtonStyle(_openStsConfigButton);
            ApplyDecisionToolbarButtonStyle(_openAlgorithmButton);
            ApplyDecisionToolbarButtonStyle(_openExamplesButton);
        }

        private Button ResolveToolbarButton(
            Button current,
            string childName)
        {
            if (current != null)
                return current;

            Transform child = sceneToolbar.transform.Find(childName);
            return child == null ? null : child.GetComponent<Button>();
        }

        private static void ApplyDecisionToolbarButtonStyle(Button button)
        {
            if (button == null)
                return;

            ExperimentUiTheme.StyleButton(button, true);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
                return;

            if (ExperimentUiTheme.Font != null)
                label.font = ExperimentUiTheme.Font;
            label.fontStyle = FontStyles.Bold;
            label.color = button.interactable
                ? (Color)ExperimentUiTheme.PrimaryText
                : (Color)ExperimentUiTheme.Muted;
            label.enableWordWrapping = false;
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = Mathf.Clamp(label.fontSize, 15f, 18f);
        }

        /// <summary>
        /// 创建S14算法说明/原因选择和S15参数填写页面，
        /// 避免继续依赖场景里内容不完整的旧算法面板。
        /// </summary>
        private void EnsureRuntimeAlgorithmSelectionPanel()
        {
            if (_algorithmAssessmentController != null)
            {
                algorithmSelectionPanel =
                    _algorithmAssessmentController.PanelGroup;
                return;
            }

            CanvasGroup oldPanel = algorithmSelectionPanel;
            if (oldPanel != null)
            {
                SetPanelVisible(oldPanel, false);
            }

            Canvas assessmentCanvas =
                ExperimentAssessmentCanvasHost.GetOrCreate(this);
            if (assessmentCanvas == null)
            {
                Debug.LogError(
                    "[SceneDecisionWorkspace] 无法创建独立评分Canvas。" );
                return;
            }

            GameObject panelObject =
                CreateWorkspaceUiObject(
                    "AlgorithmAssessmentPanel",
                    ExperimentAssessmentCanvasHost.GetLayer(
                        assessmentCanvas,
                        ExperimentAssessmentCanvasHost.AlgorithmLayer));
            RectTransform panelRect =
                panelObject.GetComponent<RectTransform>();
            StretchToParent(panelRect);

            _runtimeAlgorithmSelectionPanel =
                panelObject.AddComponent<CanvasGroup>();
            algorithmSelectionPanel =
                _runtimeAlgorithmSelectionPanel;

            _algorithmAssessmentController =
                panelObject.AddComponent<
                    AlgorithmAssessmentController>();
            _algorithmAssessmentController.Initialize(
                OnAlgorithmAssessmentConfirmed,
                CloseAlgorithmSelection);
            _algorithmAssessmentController.Hide();
        }

        /// <summary>
        /// 算法成功后核对本次EXE原始输入和输出，提交核对后才启动设备作业。
        /// </summary>
        private void OpenScheduleResultAssessment(
            RmgExeExecutionResult execution)
        {
            EnsureScheduleResultAssessmentController();
            if (_scheduleResultAssessmentController == null ||
                execution == null)
            {
                OnAlgorithmFailed(
                    "无法打开调度结果核对页面。");
                return;
            }

            _scheduleResultAssessmentController.Open(
                execution.Input,
                execution.Schedule,
                OnScheduleAssessmentSubmitted);
        }

        private void EnsureScheduleResultAssessmentController()
        {
            if (_scheduleResultAssessmentController != null)
                return;

            Canvas assessmentCanvas =
                ExperimentAssessmentCanvasHost.GetOrCreate(this);
            if (assessmentCanvas == null)
            {
                Debug.LogError(
                    "[SceneDecisionWorkspace] 无法创建独立评分Canvas。" );
                return;
            }

            GameObject panelObject =
                CreateWorkspaceUiObject(
                    "ScheduleResultAssessmentPanel",
                    ExperimentAssessmentCanvasHost.GetLayer(
                        assessmentCanvas,
                        ExperimentAssessmentCanvasHost.ResultLayer));
            StretchToParent(
                panelObject.GetComponent<RectTransform>());
            _scheduleResultAssessmentController =
                panelObject.AddComponent<
                    ScheduleResultAssessmentController>();
            _scheduleResultAssessmentController.Initialize();
        }

        private void OnScheduleAssessmentSubmitted()
        {
            if (!_entered3D || _postExperimentExecution == null || _scheduleResultConfirmed || _experimentCompleted) return;
            if (_levelInfo == null) { ShowError("找不到当前实验的UMP_LevelInfo，不能启动三维调度。"); return; }
            _scheduleResultConfirmed = true;
            _lockSceneUntilAlgorithmStarts = false;
            SetBusy(false, null);
            SetStatus("调度结果核对已提交，正在启动三维调度。", ExperimentUiTheme.Success);
            int previousRevision = global::TaskManager.Instance == null ? -1 : global::TaskManager.Instance.TaskRevision;
            _levelInfo.StartDeferredScript();
            CancelCompletionMonitor();
            _completionMonitor = StartCoroutine(WaitForExperimentCompletion(previousRevision,
                _postExperimentExecution.Schedule.operations.Length));
        }

        private Button CreateAlgorithmChoiceButton(
            Transform parent,
            string objectName,
            string labelValue,
            string algorithmKey,
            float top,
            Color32 color)
        {
            GameObject buttonObject =
                CreateWorkspaceUiObject(objectName, parent);
            RectTransform rect =
                buttonObject.GetComponent<RectTransform>();
            SetTopStretch(rect, top, 55f, 55f, 50f);

            Image image = buttonObject.AddComponent<Image>();
            image.color = color;
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(
                () => SelectAlgorithm(algorithmKey));

            TMP_Text label =
                CreateWorkspaceText(
                    "Text",
                    buttonObject.transform,
                    labelValue,
                    21f,
                    Color.black);
            StretchToParent(label.rectTransform);
            return button;
        }

        private Button CreateAlgorithmActionButton(
            Transform parent,
            string objectName,
            string labelValue,
            Vector2 anchoredPosition,
            UnityAction clickAction,
            Color32 color)
        {
            GameObject buttonObject =
                CreateWorkspaceUiObject(objectName, parent);
            RectTransform rect =
                buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(190f, 48f);
            rect.anchoredPosition = anchoredPosition;

            Image image = buttonObject.AddComponent<Image>();
            image.color = color;
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(clickAction);

            TMP_Text label =
                CreateWorkspaceText(
                    "Text",
                    buttonObject.transform,
                    labelValue,
                    21f,
                    Color.black);
            StretchToParent(label.rectTransform);
            return button;
        }

        /// <summary>
        /// 在持久化的三维工具栏中创建“查看算例”入口，并创建可反复打开的算例面板。
        /// </summary>
        private void EnsurePersistentExampleViewer()
        {
            if (_openExamplesButton == null && sceneToolbar != null)
            {
                Button[] toolbarButtons =
                    sceneToolbar.GetComponentsInChildren<Button>(true);

                GameObject buttonObject;
                if (toolbarButtons.Length > 0)
                {
                    Button template = toolbarButtons[toolbarButtons.Length - 1];
                    buttonObject = Instantiate(
                        template.gameObject,
                        template.transform.parent,
                        false);
                    buttonObject.name = "OpenExamplesButton";
                    _openExamplesButton = buttonObject.GetComponent<Button>();
                    _openExamplesButton.onClick = new Button.ButtonClickedEvent();

                    RectTransform templateRect =
                        template.transform as RectTransform;
                    RectTransform buttonRect =
                        buttonObject.transform as RectTransform;
                    if (templateRect != null && buttonRect != null &&
                        template.transform.parent.GetComponent<LayoutGroup>() == null)
                    {
                        buttonRect.anchoredPosition =
                            templateRect.anchoredPosition +
                            new Vector2(
                                0f,
                                -(templateRect.rect.height + 8f));
                    }
                }
                else
                {
                    buttonObject =
                        CreateWorkspaceUiObject(
                            "OpenExamplesButton",
                            sceneToolbar.transform);
                    RectTransform buttonRect =
                        buttonObject.GetComponent<RectTransform>();
                    buttonRect.anchorMin = new Vector2(1f, 1f);
                    buttonRect.anchorMax = new Vector2(1f, 1f);
                    buttonRect.pivot = new Vector2(1f, 1f);
                    buttonRect.sizeDelta = new Vector2(160f, 42f);
                    buttonRect.anchoredPosition = new Vector2(-12f, -12f);

                    Image buttonImage = buttonObject.AddComponent<Image>();
                    buttonImage.color = Color.white;
                    _openExamplesButton = buttonObject.AddComponent<Button>();
                    _openExamplesButton.targetGraphic = buttonImage;

                    TMP_Text buttonLabel =
                        CreateWorkspaceText(
                            "Text",
                            buttonObject.transform,
                            "查看算例",
                            21f,
                            new Color32(32, 32, 32, 255));
                    StretchToParent(buttonLabel.rectTransform);
                }

                TMP_Text label =
                    buttonObject.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = "查看算例";
                }

                Text legacyLabel =
                    buttonObject.GetComponentInChildren<Text>(true);
                if (legacyLabel != null)
                {
                    legacyLabel.text = "查看算例";
                }

                _openExamplesButton.onClick.AddListener(OpenExamplesPanel);
            }

            if (_exampleViewerOverlay == null)
            {
                BindPersistentExampleViewerCanvas();
            }
        }

        /// <summary>
        /// 绑定独立 Canvas Prefab 中的仿真算例面板。
        /// 页面结构和列表行结构均由 Prefab 提供，控制器只填充数据。
        /// </summary>
        private void BindPersistentExampleViewerCanvas()
        {
            Canvas canvas = ExperimentAssessmentCanvasHost.GetOrCreate(this);
            if (canvas == null)
                return;

            Transform layer = canvas.transform.Find(
                ExperimentAssessmentCanvasHost.RuntimeExampleLayer);
            if (layer == null)
            {
                Debug.LogError(
                    "[SceneDecisionWorkspaceController] 独立 Canvas 缺少仿真算例层，请重新生成 Canvas Prefab。");
                return;
            }

            ExperimentExampleRuntimeCanvasView view =
                layer.GetComponentInChildren<
                    ExperimentExampleRuntimeCanvasView>(true);
            if (view == null)
            {
                Debug.LogError(
                    "[SceneDecisionWorkspaceController] 未找到仿真算例 Canvas 组件。");
                return;
            }

            _exampleRuntimeCanvasView = view;
            _exampleViewerOverlay = view.gameObject;
            _exampleListContent = view.Content;
            _exampleListScrollRect = view.ScrollRect;
            _exampleListVerticalScrollbar = view.VerticalScrollbar;
            _exampleSourceText = view.SourceText;

            if (view.CloseButton != null)
            {
                view.CloseButton.onClick.RemoveListener(CloseExamplesPanel);
                view.CloseButton.onClick.AddListener(CloseExamplesPanel);
            }

            _exampleViewerOverlay.SetActive(false);
        }

        public void OpenExamplesPanel()
        {
            if (!_entered3D || _algorithmRunning)
            {
                return;
            }

            EnsurePersistentExampleViewer();
            RebuildPersistentExampleRows();

            if (_exampleViewerOverlay != null)
            {
                _exampleViewerOverlay.SetActive(true);
                _exampleViewerOverlay.transform.SetAsLastSibling();
            }

            LockInputForExampleViewer();
            StartExampleStatusRefresh();

            if (sceneToolbar != null)
            {
                sceneToolbar.SetActive(false);
            }
        }

        public void CloseExamplesPanel()
        {
            StopExampleStatusRefresh();

            if (_exampleViewerOverlay != null)
            {
                _exampleViewerOverlay.SetActive(false);
            }

            RestoreExampleViewerInput();

            if (sceneToolbar != null)
            {
                sceneToolbar.SetActive(
                    _entered3D && !_algorithmRunning);
            }
        }

        private void LockInputForExampleViewer()
        {
            if (_exampleViewerInputLocked)
                return;

            _exampleViewerInputLocked = true;
            _exampleViewerDisabledInputBehaviours.Clear();

            MonoBehaviour[] behaviours =
                FindObjectsOfType<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null)
                    continue;

                string typeName = behaviour.GetType().Name;
                bool isCameraOrPlayerInput =
                    typeName == "FreeCamera" ||
                    typeName == "FreeCameraSplit" ||
                    typeName == "FirstPersonController" ||
                    typeName == "GameController";
                if (!isCameraOrPlayerInput)
                    continue;

                _exampleViewerDisabledInputBehaviours[behaviour] =
                    behaviour.enabled;
                behaviour.enabled = false;
            }

            // 查看列表时只冻结镜头输入，不暂停Time.timeScale，
            // 因而后台作业和状态刷新仍会继续。
            if (!_sceneInputLocked && CameraManager.Instance != null)
                CameraManager.Instance.DoPause();
        }

        private void RestoreExampleViewerInput()
        {
            if (!_exampleViewerInputLocked)
                return;

            foreach (KeyValuePair<Behaviour, bool> pair
                     in _exampleViewerDisabledInputBehaviours)
            {
                if (pair.Key != null)
                    pair.Key.enabled = pair.Value;
            }

            _exampleViewerDisabledInputBehaviours.Clear();
            _exampleViewerInputLocked = false;

            if (!_sceneInputLocked && CameraManager.Instance != null)
                CameraManager.Instance.UnPause();
        }

        private void StartExampleStatusRefresh()
        {
            StopExampleStatusRefresh();
            RefreshPersistentExampleStatuses();
            _exampleStatusRefreshCoroutine =
                StartCoroutine(RefreshExampleStatusesWhileOpen());
        }

        private void StopExampleStatusRefresh()
        {
            if (_exampleStatusRefreshCoroutine == null)
                return;

            StopCoroutine(_exampleStatusRefreshCoroutine);
            _exampleStatusRefreshCoroutine = null;
        }

        private IEnumerator RefreshExampleStatusesWhileOpen()
        {
            WaitForSecondsRealtime wait =
                new WaitForSecondsRealtime(0.25f);
            while (_exampleViewerOverlay != null &&
                   _exampleViewerOverlay.activeInHierarchy)
            {
                RefreshPersistentExampleStatuses();
                yield return wait;
            }

            _exampleStatusRefreshCoroutine = null;
        }

        private void RebuildPersistentExampleRows()
        {
            if (_exampleListContent == null)
            {
                return;
            }

            for (int index = _exampleListContent.childCount - 1;
                 index >= 0;
                 index--)
            {
                Destroy(_exampleListContent.GetChild(index).gameObject);
            }

            _exampleStatusVisuals.Clear();

            if (_exampleSourceText != null)
            {
                _exampleSourceText.text = "当前实验算例 · 共 " + SceneNumericalExampleRuntime.Examples.Length + " 条作业任务。";
                if (SceneExampleScheduleRuntime.HasSchedule)
                    _exampleSourceText.text +=
                        "  当前显示算法调度序列及实时作业状态。";
                _exampleSourceText.color = ExperimentUiTheme.Muted;
            }

            SchedulingResultDto schedule =
                SceneExampleScheduleRuntime.Schedule;
            if (schedule != null && schedule.operations != null &&
                schedule.operations.Length > 0)
            {
                List<ScheduledOperationDto> operations =
                    new List<ScheduledOperationDto>(schedule.operations);
                operations.Sort((left, right) =>
                {
                    if (ReferenceEquals(left, right)) return 0;
                    if (left == null) return 1;
                    if (right == null) return -1;
                    return left.sequence.CompareTo(right.sequence);
                });

                for (int index = 0; index < operations.Count; index++)
                {
                    ScheduledOperationDto operation = operations[index];
                    if (operation == null)
                        continue;

                    int taskIndex = index + 1;
                    int state = int.MaxValue;
                    bool hasState =
                        global::TaskManager.Instance != null &&
                        global::TaskManager.Instance.TryGetTaskState(
                            taskIndex,
                            out state);
                    string stateLabel = hasState
                        ? GetTaskStateLabel(state)
                        : "等待执行";

                    GameObject row = CreateWorkspaceExampleRow(
                        _exampleListContent,
                        new[]
                        {
                            operation.sequence > 0
                                ? operation.sequence.ToString()
                                : taskIndex.ToString(),
                            operation.craneId ?? string.Empty,
                            operation.taskType ?? string.Empty,
                            operation.fromCode ?? string.Empty,
                            operation.toCode ?? string.Empty,
                            Mathf.Max(
                                0f,
                                operation.plannedEndSeconds -
                                operation.plannedStartSeconds).ToString("0.##"),
                            stateLabel
                        },
                        false,
                        index);
                    RegisterExampleStatusVisual(
                        row,
                        taskIndex,
                        hasState ? state : int.MaxValue);
                }

                LayoutRebuilder.ForceRebuildLayoutImmediate(
                    _exampleListContent);
                ResetPersistentExampleScrollPosition();
                return;
            }

            SceneNumericalExampleDto[] examples =
                SceneNumericalExampleRuntime.Examples;
            if (examples == null || examples.Length == 0)
            {
                CreateWorkspaceExampleRow(
                    _exampleListContent,
                    new[]
                    {
                        "-", "-", "当前没有可查看的算例",
                        "-", "-", "-", "-"
                    },
                    false,
                    0);
                LayoutRebuilder.ForceRebuildLayoutImmediate(
                    _exampleListContent);
                ResetPersistentExampleScrollPosition();
                return;
            }

            for (int i = 0; i < examples.Length; i++)
            {
                SceneNumericalExampleDto example = examples[i];
                if (example == null)
                {
                    continue;
                }

                CreateWorkspaceExampleRow(
                    _exampleListContent,
                    new[]
                    {
                        example.no ?? (i + 1).ToString(),
                        example.argm ?? string.Empty,
                        example.type ?? string.Empty,
                        example.startPointPosition + " / " + example.startList,
                        example.endPointPosition + " / " + example.endList,
                        example.time_consuming.ToString("0.##"),
                        "待调度"
                    },
                    false,
                    i);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(
                _exampleListContent);
            ResetPersistentExampleScrollPosition();
        }

        private void ResetPersistentExampleScrollPosition()
        {
            Canvas.ForceUpdateCanvases();
            if (_exampleListScrollRect != null)
                _exampleListScrollRect.verticalNormalizedPosition = 1f;
        }

        private void RegisterExampleStatusVisual(
            GameObject row,
            int taskIndex,
            int state)
        {
            if (row == null)
                return;

            Transform statusTransform = row.transform.Find("Column6");
            TMP_Text stateText = statusTransform == null
                ? null
                : statusTransform.GetComponent<TMP_Text>();
            Image rowImage = row.GetComponent<Image>();
            if (stateText == null || rowImage == null)
                return;

            ExampleStatusVisual visual = new ExampleStatusVisual
            {
                TaskIndex = taskIndex,
                StatusText = stateText,
                RowImage = rowImage,
                DefaultColor = rowImage.color
            };
            _exampleStatusVisuals.Add(visual);
            ApplyExampleTaskState(visual, state);
        }

        private void RefreshPersistentExampleStatuses()
        {
            for (int index = 0;
                 index < _exampleStatusVisuals.Count;
                 index++)
            {
                ExampleStatusVisual visual =
                    _exampleStatusVisuals[index];
                int state;
                if (global::TaskManager.Instance == null ||
                    !global::TaskManager.Instance.TryGetTaskState(
                        visual.TaskIndex,
                        out state))
                {
                    state = int.MaxValue;
                }

                ApplyExampleTaskState(visual, state);
            }
        }

        private static void ApplyExampleTaskState(
            ExampleStatusVisual visual,
            int state)
        {
            if (visual == null || visual.LastState == state)
                return;

            visual.LastState = state;
            if (visual.StatusText != null)
                visual.StatusText.text = GetTaskStateLabel(state);

            if (visual.RowImage == null)
                return;

            if (state < 0)
                visual.RowImage.color = ExperimentUiTheme.Alternate;
            else if (state == 0)
                visual.RowImage.color = new Color32(20, 70, 65, 255);
            else if (state == int.MaxValue)
                visual.RowImage.color = visual.DefaultColor;
            else
                visual.RowImage.color = new Color32(67, 61, 38, 255);
        }

        private static string GetTaskStateLabel(int state)
        {
            if (state < 0)
                return "已完成";
            if (state == 0)
                return "作业中";
            if (state == int.MaxValue)
                return "等待执行";
            return "未作业";
        }

        private GameObject CreateWorkspaceExampleRow(
            Transform parent,
            string[] values,
            bool isHeader,
            int rowIndex)
        {
            GameObject template = _exampleRuntimeCanvasView != null
                ? _exampleRuntimeCanvasView.RowPrefab
                : null;
            if (template == null)
            {
                Debug.LogError(
                    "[SceneDecisionWorkspaceController] 仿真算例 Canvas 未配置行预制体。");
                return null;
            }

            GameObject row = Instantiate(template, parent, false);
            row.name = isHeader ? "HeaderColumns" : "ExampleRow";
            row.SetActive(true);

            Image rowImage = row.GetComponent<Image>();
            if (rowImage != null)
            {
                rowImage.color = isHeader
                    ? (Color)ExperimentUiTheme.TableHeader
                    : rowIndex % 2 == 0
                        ? (Color)ExperimentUiTheme.PanelRaised
                        : (Color)ExperimentUiTheme.Alternate;
            }

            int count = Mathf.Min(values.Length, 7);
            for (int index = 0; index < count; index++)
            {
                Transform cellTransform =
                    row.transform.Find("Column" + index);
                TMP_Text cell = cellTransform == null
                    ? null
                    : cellTransform.GetComponent<TMP_Text>();
                if (cell != null)
                    cell.text = values[index] ?? string.Empty;
            }

            return row;
        }

        private TMP_Text CreateWorkspaceText(
            string objectName,
            Transform parent,
            string value,
            float fontSize,
            Color color)
        {
            GameObject textObject =
                CreateWorkspaceUiObject(objectName, parent);
            TextMeshProUGUI text =
                textObject.AddComponent<TextMeshProUGUI>();
            text.font =
                statusText != null
                    ? statusText.font
                    : selectedAlgorithmText != null
                        ? selectedAlgorithmText.font
                        : null;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            return text;
        }

        private static GameObject CreateWorkspaceUiObject(
            string objectName,
            Transform parent)
        {
            GameObject result =
                new GameObject(
                    objectName,
                    typeof(RectTransform));
            result.transform.SetParent(parent, false);
            result.layer = parent.gameObject.layer;
            return result;
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetTopStretch(
            RectTransform rect,
            float top,
            float left,
            float right,
            float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -(top + height));
            rect.offsetMax = new Vector2(-right, -top);
        }

        private void ShowError(
            string message)
        {
            SetStatus(
                message,
                new Color(0.95f, 0.2f, 0.2f));

            Debug.LogError(
                "[SceneDecisionWorkspace] " +
                message);
        }

        private void SetStatus(
            string message,
            Color color)
        {
            if (statusText == null)
            {
                return;
            }

            statusText.text = message;
            statusText.color = color;
        }
    }
}
