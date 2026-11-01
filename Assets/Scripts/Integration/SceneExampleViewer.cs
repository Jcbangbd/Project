using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vtp.VR.LocalAlgorithmExe;
using Vtp.VR.Rest;
using ZPMC.VR.Unity;

namespace Vtp.VR.Integration
{
    /// <summary>
    /// 实验算例查看控制器。
    ///
    /// 功能：
    /// 1. 点击“决策仿真实验”后打开算例界面；
    /// 2. 从远程 RESTful 接口获取算例；
    /// 3. 将算例显示到 ScrollView；
    /// 4. 将算例保存到 SceneNumericalExampleRuntime；
    /// 5. 本阶段不运行调度算法、不加载三维场景。
    /// </summary>
    public sealed class SceneExampleViewer : MonoBehaviour
    {
        [Header("页面")]
        [Tooltip("图片1中的实验介绍页面")]
        [SerializeField]
        private GameObject introductionPanel;

        [Tooltip("新建的算例查看页面")]
        [SerializeField]
        private GameObject exampleViewerPanel;

        [Header("按钮")]
        [Tooltip("图片1中的“决策仿真实验”按钮")]
        [SerializeField]
        private Button decisionSimulationButton;

        [Tooltip("算例页面的返回按钮")]
        [SerializeField]
        private Button backButton;

        [Tooltip("重新请求算例按钮")]
        [SerializeField]
        private Button refreshButton;

        [Tooltip("进入三维场景按钮，本阶段只控制是否可点击")]
        [SerializeField]
        private Button enter3DButton;

        [Header("文字")]
        [SerializeField]
        private TMP_Text titleText;

        [SerializeField]
        private TMP_Text summaryText;

        [SerializeField]
        private TMP_Text statusText;

        [Header("算例列表")]
        [Tooltip("ScrollView/Viewport/Content")]
        [SerializeField]
        private RectTransform content;

        [Tooltip("算例列表行预制体")]
        [SerializeField]
        private GameObject rowPrefab;

        [Header("任务配置")]
        [Tooltip("暂时用于测试。以后应由教师下发的任务编号赋值")]
        [SerializeField]
        private string testTaskId = "kc1";

        [Tooltip("当前介绍页对应的作业类型。岸桥卸船页保持为‘卸船’。")]
        [SerializeField]
        private string experimentTaskType = "卸船";

        [Header("教师任务选择")]
        [SerializeField]
        private string taskStatuses = "PUBLISHED,ACKNOWLEDGED";

        [SerializeField]
        private string taskExperimentId = string.Empty;

        [SerializeField]
        [Min(1)]
        private int taskPageSize = 20;

        [Tooltip("服务器任务列表不可用时，是否显示本地测试任务。正式评分版本应关闭。")]
        [SerializeField]
        private bool allowLocalTestTask = true;

        [Header("进入三维场景")]
        [Tooltip("拖入 Canvas/DischargeShip 上的 UMP_LevelInfo")]
        [SerializeField]
        private UMP_LevelInfo dischargeLevelInfo;

        [Header("本地测试算例回退")]
        [Tooltip("服务器无算例、请求失败或未登录时，是否使用本地算例")]
        [SerializeField]
        private bool useLocalFallbackWhenServerUnavailable = true;

        [Tooltip("首次算例请求等待秒数。超过该时间就切换到项目内测试算例，避免断网时等待全局重试结束。")]
        [SerializeField]
        private float initialRequestWaitSeconds = 8f;

        private readonly List<GameObject> _createdRows =
            new List<GameObject>();

        private SceneNumericalExampleService _exampleService;

        private string _selectedTaskId;
        private bool _isLoading;
        private bool _hasLoadedExamples;
        private ExperimentExampleSelectionCanvasView
            _exampleSelectionCanvasView;
        private bool _preExperimentQuestionnaireCompleted;
        private PreExperimentQuestionnaireController
            _preExperimentQuestionnaire;
        private Coroutine _loadTimeoutCoroutine;
        private int _loadRequestVersion;
        private ExperimentTaskSelectionController
            _taskSelectionController;
        private bool _selectedTaskIsLocal;
        private bool _continueToExamplesAfterTaskSelection;
        private bool _initialTaskSelectionOpened;
        private ExperimentIntroductionView _introductionView;
        private ScrollRect _exampleScrollRect;
        private Scrollbar _exampleVerticalScrollbar;

        private const float ExampleScrollSensitivity = 72f;

        private static readonly Color NormalColor =
            ExperimentUiTheme.TextColor;
        private static readonly Color SuccessColor =
            ExperimentUiTheme.Success;
        private static readonly Color ErrorColor =
            ExperimentUiTheme.Error;

        private void Awake()
        {
            // 确保全局 REST 客户端已经创建。
            RestApiClient client = RestApiClient.EnsureInstance();

            _exampleService =
                new SceneNumericalExampleService(client);

            // 任务必须由评分流程第2步明确选择，不能再自动使用kc1。
            _selectedTaskId = string.Empty;
            _selectedTaskIsLocal = false;

            _hasLoadedExamples = false;
            _preExperimentQuestionnaireCompleted = false;
            PreExperimentAssessmentRuntime.Clear();

            BindExampleSelectionCanvasView();

            // 本组件随后会隐藏算例页自身；如果把任务列表放到Start中，
            // 当组件正好挂在该页上时Start不会执行。必须在Awake阶段、
            // 页面隐藏前直接创建并打开教师任务列表。
            OpenInitialTaskSelection();

            // introductionPanel 在当前场景实际绑定的是主页面上的
            // “岸桥卸船流程”入口。这里绝不能关闭它，否则主流程菜单会少一项。

            if (exampleViewerPanel != null)
            {
                exampleViewerPanel.SetActive(false);
            }

            if (enter3DButton != null)
            {
                enter3DButton.interactable = false;
            }

            ConfigureExamplePageLayout();
            ConfigureViewerControls();
            ConfigureExampleScrollView();

            SetStatus(string.Empty, NormalColor);
        }

        /// <summary>
        /// 从独立 Canvas Prefab 获取页面组件。此处只绑定引用，
        /// 页面层级、按钮、滚动区域和提示弹窗均保存在 Prefab 中。
        /// </summary>
        private void BindExampleSelectionCanvasView()
        {
            Canvas canvas = ExperimentAssessmentCanvasHost.GetOrCreate(this);
            if (canvas == null)
                return;

            Transform layer = canvas.transform.Find(
                ExperimentAssessmentCanvasHost.ExampleSelectionLayer);
            if (layer == null)
            {
                Debug.LogError(
                    "[SceneExampleViewer] 独立 Canvas 缺少实验算例选择层，请重新生成 Canvas Prefab。");
                return;
            }

            ExperimentExampleSelectionCanvasView view =
                layer.GetComponentInChildren<
                    ExperimentExampleSelectionCanvasView>(true);
            if (view == null)
            {
                Debug.LogError(
                    "[SceneExampleViewer] 未找到实验算例选择 Canvas 组件。");
                return;
            }

            GameObject legacyPanel = exampleViewerPanel;
            _exampleSelectionCanvasView = view;
            exampleViewerPanel = view.gameObject;
            titleText = view.TitleText;
            summaryText = view.SummaryText;
            statusText = view.StatusText;
            content = view.Content;
            rowPrefab = view.RowPrefab;
            backButton = view.BackButton;
            refreshButton = view.RefreshButton;
            enter3DButton = view.EnterButton;
            _exampleScrollRect = view.ScrollRect;
            _exampleVerticalScrollbar = view.VerticalScrollbar;

            if (legacyPanel != null && legacyPanel != exampleViewerPanel)
                legacyPanel.SetActive(false);
        }

        private IEnumerator Start()
        {
            // 登录成功进入系统后先显示教师任务列表。等一帧是为了让
            // SceneStage1中的Canvas和原实验介绍页面先完成初始化。
            yield return null;
            OpenInitialTaskSelection();
        }

        private void OpenInitialTaskSelection()
        {
            if (_initialTaskSelectionOpened ||
                !RestSession.IsAuthenticated ||
                !string.IsNullOrWhiteSpace(_selectedTaskId))
            {
                return;
            }

            _initialTaskSelectionOpened = true;
            _continueToExamplesAfterTaskSelection = false;
            EnsureTaskSelectionController();
            if (_taskSelectionController != null)
                _taskSelectionController.Open();
        }

        private void OnEnable()
        {
            if (decisionSimulationButton != null)
            {
                decisionSimulationButton.onClick.AddListener(
                    OpenAndLoadExamples);
            }

            if (backButton != null)
            {
                backButton.onClick.AddListener(BackToIntroduction);
            }

            if (refreshButton != null)
            {
                refreshButton.onClick.AddListener(RefreshExamples);
            }

            if (enter3DButton != null)
            {
                enter3DButton.onClick.AddListener(Enter3DScene);
            }
        }

        private void OnDisable()
        {
            StopLoadTimeout();
            _loadRequestVersion++;

            if (decisionSimulationButton != null)
            {
                decisionSimulationButton.onClick.RemoveListener(
                    OpenAndLoadExamples);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(BackToIntroduction);
            }

            if (refreshButton != null)
            {
                refreshButton.onClick.RemoveListener(
                    RefreshExamples);
            }

            if (enter3DButton != null)
            {
                enter3DButton.onClick.RemoveListener(Enter3DScene);
            }
        }

        /// <summary>
        /// 以后在任务列表中选择任务时调用。
        /// 参数必须是服务器接口需要的 taskId。
        /// </summary>
        public void SetSelectedTask(string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                Debug.LogError(
                    "[SceneExampleViewer] taskId 不能为空。");
                return;
            }

            string newTaskId = taskId.Trim();

            if (_selectedTaskId == newTaskId)
            {
                return;
            }

            _selectedTaskId = newTaskId;
            _hasLoadedExamples = false;
            ResetQuestionnaireState();

            SceneNumericalExampleRuntime.Clear();

            if (enter3DButton != null)
            {
                enter3DButton.interactable = false;
            }

            ClearRows();

            Debug.Log(
                $"[SceneExampleViewer] 已选择任务：{_selectedTaskId}");
        }

        /// <summary>
        /// 供其他四个实验入口设置自己的作业类型。
        /// 可用值：卸船、装船、翻箱、卸车、装车。
        /// </summary>
        public void SetExperimentTaskType(string taskType)
        {
            string normalized;
            if (!SceneExampleTaskCaseAdapter.TryNormalizeTaskType(
                    taskType,
                    out normalized))
            {
                Debug.LogError(
                    "[SceneExampleViewer] 无法识别实验作业类型：" +
                    taskType);
                return;
            }

            if (string.Equals(
                    experimentTaskType,
                    normalized,
                    StringComparison.Ordinal))
            {
                return;
            }

            experimentTaskType = normalized;
            _hasLoadedExamples = false;
            ResetQuestionnaireState();
            SceneNumericalExampleRuntime.Clear();
            ClearRows();

            if (enter3DButton != null)
                enter3DButton.interactable = false;
        }

        /// <summary>
        /// 点击“决策仿真实验”后执行。
        /// </summary>
        public void OpenAndLoadExamples()
        {
            if (_introductionView != null) _introductionView.Hide();
            if (_isLoading)
            {
                return;
            }

            // 没有选定任务时必须先完成评分流程第2步。选择任务后只回到
            // 实验介绍页，不直接越过介绍进入算例页。
            if (string.IsNullOrWhiteSpace(_selectedTaskId))
            {
                _continueToExamplesAfterTaskSelection = false;
                EnsureTaskSelectionController();
                if (_taskSelectionController != null)
                    _taskSelectionController.Open();
                return;
            }

            StudentAssessmentRuntime.MarkIntroductionViewed();

            ShowExampleViewer();
            ConfigureExamplePageLayout();
            ConfigureViewerControls();
            ConfigureExampleScrollView();

            if (_selectedTaskIsLocal)
            {
                if (!TryUseLocalFallback(
                        _selectedTaskId,
                        "当前选择的是本地测试任务"))
                {
                    ShowError("未能读取实验算例，请重新获取。" );
                }
                return;
            }

            if (!RestSession.IsAuthenticated)
            {
                if (TryUseLocalFallback(_selectedTaskId,
                        "当前未登录远程服务器"))
                {
                    return;
                }

                ShowError(
                    "登录状态已失效，请返回登录页面重新登录。");
                return;
            }

            if (string.IsNullOrWhiteSpace(_selectedTaskId))
            {
                ShowError(
                    "当前没有任务编号，无法获取实验算例。");
                return;
            }

            LoadExamples();
        }

        /// <summary>
        /// 点击“重新获取”按钮后执行。
        /// </summary>
        public void RefreshExamples()
        {
            if (_isLoading)
            {
                return;
            }

            // 完成答题后答案已与本次算例绑定，禁止重新获取以免产生冲突。
            if (_preExperimentQuestionnaireCompleted)
            {
                SetStatus(
                    "答题已经完成，本次算例已锁定，不能重新获取。",
                    ErrorColor);
                ConfigureViewerControls();
                return;
            }

            if (!RestSession.IsAuthenticated)
            {
                if (TryUseLocalFallback(_selectedTaskId,
                        "当前未登录远程服务器"))
                {
                    return;
                }

                ShowError("登录状态已失效，请重新登录。");
                return;
            }

            if (string.IsNullOrWhiteSpace(_selectedTaskId))
            {
                ShowError("当前任务编号为空。");
                return;
            }

            ResetQuestionnaireState();
            LoadExamples();
        }

        private void LoadExamples()
        {
            StopLoadTimeout();
            int requestVersion = ++_loadRequestVersion;
            _hasLoadedExamples = false;

            SceneNumericalExampleRuntime.Clear();
            ClearRows();

            SetLoading(true);

            if (titleText != null)
            {
                titleText.text = "实验算例";
            }

            if (summaryText != null)
            {
                summaryText.text =
                    $"任务编号：{_selectedTaskId}";
            }

            SetStatus(
                "正在从服务器获取实验算例，请稍候……",
                NormalColor);

            string requestedTaskId = _selectedTaskId;

            _loadTimeoutCoroutine = StartCoroutine(
                WaitForInitialExampleRequest(
                    requestedTaskId,
                    requestVersion));

            Debug.Log(
                "[SceneExampleViewer] 正在请求算例，" +
                $"taskId={requestedTaskId}");

            _exampleService.GetByTaskId(
                requestedTaskId,

                examples =>
                {
                    if (requestVersion != _loadRequestVersion)
                        return;
                    StopLoadTimeout();
                    OnExamplesReceived(
                        requestedTaskId,
                        examples);
                },

                error =>
                {
                    if (requestVersion != _loadRequestVersion)
                        return;
                    StopLoadTimeout();
                    OnExamplesFailed(
                        requestedTaskId,
                        error);
                });
        }

        private IEnumerator WaitForInitialExampleRequest(
            string taskId,
            int requestVersion)
        {
            yield return new WaitForSecondsRealtime(
                Mathf.Max(3f, initialRequestWaitSeconds));

            _loadTimeoutCoroutine = null;
            if (requestVersion != _loadRequestVersion || !_isLoading)
                yield break;

            // 让迟到的服务器回调失效，避免本地回退显示后又被旧请求覆盖。
            _loadRequestVersion++;
            SetLoading(false);

            if (TryUseLocalFallback(
                    taskId,
                    "服务器算例请求等待时间过长"))
            {
                yield break;
            }

            ShowError(
                "算例加载超时，请点击重新获取。" );
        }

        private void StopLoadTimeout()
        {
            if (_loadTimeoutCoroutine == null)
                return;

            StopCoroutine(_loadTimeoutCoroutine);
            _loadTimeoutCoroutine = null;
        }

        private void OnExamplesReceived(
    string taskId,
    SceneNumericalExampleDto[] examples)
        {
            SetLoading(false);

            // 服务器接口调用成功，但没有任何算例。
            if (examples == null || examples.Length == 0)
            {
                if (TryUseLocalFallback(
                        taskId,
                        "服务器没有返回该任务的算例"))
                {
                    return;
                }

                ShowError(
                    $"服务器没有返回任务 {taskId} 的算例数据。");
                return;
            }

            string taskType = GetExperimentTaskType();
            SceneNumericalExampleDto[] filteredExamples =
                FilterExamplesByTaskType(
                    examples,
                    taskType);

            if (filteredExamples.Length == 0)
            {
                if (TryUseLocalFallback(
                        taskId,
                        "服务器算例中没有“" +
                        taskType +
                        "”任务"))
                {
                    return;
                }

                ShowError(
                    "服务器返回了 " +
                    examples.Length +
                    " 条算例，但没有当前实验需要的“" +
                    taskType +
                    "”任务。");
                return;
            }

            string sourceMessage =
                "当前使用服务器算例：原始 " +
                examples.Length +
                " 条，已筛选“" +
                taskType +
                "”任务 " +
                filteredExamples.Length +
                " 条。";

            ApplyExamples(
                taskId,
                filteredExamples,
                sourceMessage,
                SuccessColor,
                false,
                sourceMessage,
                examples);

            Debug.Log(
                "[SceneExampleViewer] 服务器算例获取成功，" +
                "taskId=" +
                taskId +
                "，原始数量=" +
                examples.Length +
                "，类型=" +
                taskType +
                "，筛选后数量=" +
                filteredExamples.Length);
        }

        private void OnExamplesFailed(
    string taskId,
    RestApiError error)
        {
            SetLoading(false);

            string message =
                error == null
                    ? "未知网络错误"
                    : error.Message;

            // 404、500、网络中断等情况均尝试本地回退。
            if (TryUseLocalFallback(
                    taskId,
                    "远程请求失败：" + message))
            {
                return;
            }

            ShowError(
                $"获取任务 {taskId} 算例失败：{message}");

            Debug.LogError(
                "[SceneExampleViewer] 获取算例失败，" +
                "taskId=" +
                taskId +
                "，原因=" +
                message);
        }

        private string GetExperimentTaskType()
        {
            string normalized;
            if (SceneExampleTaskCaseAdapter.TryNormalizeTaskType(
                    experimentTaskType,
                    out normalized))
            {
                return normalized;
            }

            // 当前控制器挂在岸桥卸船实验入口，旧场景没有序列化
            // 新字段时也必须保持原有含义。
            return "卸船";
        }

        private static SceneNumericalExampleDto[]
            FilterExamplesByTaskType(
                SceneNumericalExampleDto[] examples,
                string requiredTaskType)
        {
            if (examples == null || examples.Length == 0)
                return new SceneNumericalExampleDto[0];

            List<SceneNumericalExampleDto> result =
                new List<SceneNumericalExampleDto>();

            for (int index = 0; index < examples.Length; index++)
            {
                SceneNumericalExampleDto example = examples[index];
                if (example == null)
                    continue;

                string taskType;
                if (SceneExampleTaskCaseAdapter.TryNormalizeTaskType(
                        example.type,
                        out taskType) &&
                    string.Equals(
                        taskType,
                        requiredTaskType,
                        StringComparison.Ordinal))
                {
                    result.Add(example);
                }
            }

            return result.ToArray();
        }

        // ==========================================================
        // 临时本地算例回退
        //
        // 服务器恢复后：
        // 1. 在 Inspector 取消勾选
        //    Use Local Fallback When Server Unavailable；
        // 2. 不需要修改其他流程。
        // ==========================================================

        private bool TryUseLocalFallback(
            string taskId,
            string reason)
        {
            if (!useLocalFallbackWhenServerUnavailable)
            {
                return false;
            }

            string normalizedTaskId =
                string.IsNullOrWhiteSpace(taskId)
                    ? "local-kc1"
                    : taskId.Trim();

            string taskType = GetExperimentTaskType();
            string workbookPath = Path.Combine(
                Application.streamingAssetsPath,
                "RmgAlgorithmEngine",
                "算例数据.xlsx");

            SceneNumericalExampleDto[] allLocalExamples;
            string localReadError;
            if (!RmgCaseWorkbookBridge.TryReadExamples(
                    workbookPath,
                    normalizedTaskId,
                    null,
                    out allLocalExamples,
                    out localReadError))
            {
                Debug.LogError(
                    "[SceneExampleViewer] ZIP本地测试算例读取失败：" +
                    localReadError);
                return false;
            }

            SceneNumericalExampleDto[] localExamples =
                FilterExamplesByTaskType(
                    allLocalExamples,
                    taskType);

            if (localExamples == null ||
                localExamples.Length == 0)
            {
                return false;
            }

            string fallbackMessage =
                "服务器算例不可用，当前使用 ZIP 本地测试算例；" +
                "已筛选“" +
                taskType +
                "”任务 " +
                localExamples.Length +
                " 条。";

            ApplyExamples(
                normalizedTaskId,
                localExamples,
                fallbackMessage,
                ErrorColor,
                true,
                fallbackMessage,
                allLocalExamples);

            Debug.LogWarning(
                "[SceneExampleViewer] 已切换到本地测试算例。" +
                "原因=" +
                reason +
                "，taskId=" +
                normalizedTaskId +
                "，数量=" +
                localExamples.Length);

            return true;
        }

        private void Enter3DScene()
        {
            if (_introductionView != null) _introductionView.Hide();
            if (_isLoading)
            {
                return;
            }

            if (!_hasLoadedExamples ||
                !SceneNumericalExampleRuntime.HasExamples)
            {
                ShowError(
                    "当前没有可用算例，不能进入三维场景。");
                return;
            }

            if (dischargeLevelInfo == null)
            {
                ShowError(
                    "没有设置 Discharge Level Info，" +
                    "请把 Canvas/DischargeShip 拖入该字段。");
                return;
            }

            Debug.Log(
                "[SceneExampleViewer] 准备进入三维场景，" +
                "taskId=" +
                SceneNumericalExampleRuntime.CourseTaskId +
                "，算例数量=" +
                SceneNumericalExampleRuntime.Examples.Length);

            var workspace = SceneDecisionWorkspaceController.Instance;
            if (workspace == null) { ShowError("找不到实验流程控制器。"); return; }
            if (exampleViewerPanel != null) exampleViewerPanel.SetActive(false);
            workspace.OpenExperimentPreparation(dischargeLevelInfo);
        }

        private void ApplyExamples(
            string taskId,
            SceneNumericalExampleDto[] examples,
            string statusMessage,
            Color statusColor,
            bool isLocalFallback = false,
            string sourceMessage = null,
            SceneNumericalExampleDto[] originalExamples = null)
        {
            SetLoading(false);
            ClearRows();

            if (titleText != null)
            {
                titleText.gameObject.SetActive(true);
                titleText.text = "实验算例";
            }

            if (summaryText != null)
            {
                SceneNumericalExampleDto[] displayExamples =
                    originalExamples != null &&
                    originalExamples.Length > 0
                        ? originalExamples
                        : examples;
                summaryText.gameObject.SetActive(true);
                summaryText.text =
                    "任务编号：" +
                    taskId +
                    "    算例任务数量：" +
                    displayExamples.Length +
                    "    当前实验将筛选：" +
                    GetExperimentTaskType() +
                    " " +
                    examples.Length +
                    " 条";
            }

            SceneNumericalExampleRuntime.Set(
                taskId,
                examples,
                isLocalFallback,
                sourceMessage,
                GetExperimentTaskType(),
                originalExamples);

            SceneNumericalExampleDto[] visibleExamples =
                originalExamples != null &&
                originalExamples.Length > 0
                    ? originalExamples
                    : examples;

            // 答题前展示教师下发的完整算例；实际进入当前实验和EXE的
            // 仍然是SceneNumericalExampleRuntime.Examples中的筛选结果。
            BuildRows(visibleExamples);

            StudentAssessmentRuntime.MarkExamplesViewed(
                visibleExamples == null ? 0 : visibleExamples.Length);

            _hasLoadedExamples = true;
            _preExperimentQuestionnaireCompleted = false;
            PreExperimentAssessmentRuntime.Begin(
                taskId,
                visibleExamples);

            // 只隐藏面向联调的来源警告，缓存来源和正式成绩保护不变。
            SetStatus(isLocalFallback ? string.Empty : statusMessage,
                isLocalFallback ? (Color)ExperimentUiTheme.Muted : statusColor);
            HideLocalFallbackNotice();

            if (enter3DButton != null)
            {
                enter3DButton.interactable = true;
            }

            ConfigureCaseActionButton();
        }

        private void BuildRows(
            SceneNumericalExampleDto[] examples)
        {
            ClearRows();

            for (int i = 0; i < examples.Length; i++)
            {
                SceneNumericalExampleDto example = examples[i];

                if (example == null)
                {
                    continue;
                }

                GameObject row = Instantiate(
                    rowPrefab,
                    content);

                row.name = $"ExampleRow_{i + 1}";
                row.SetActive(true);

                Image rowImage = row.GetComponent<Image>();
                if (rowImage != null)
                {
                    rowImage.color = i % 2 == 0
                        ? (Color)ExperimentUiTheme.PanelRaised
                        : (Color)ExperimentUiTheme.Alternate;
                }

                _createdRows.Add(row);

                SetRowText(
                    row,
                    "NoText",
                    string.IsNullOrWhiteSpace(example.no)
                        ? (i + 1).ToString()
                        : example.no);

                SetRowText(
                    row,
                    "CraneText",
                    string.IsNullOrWhiteSpace(example.argm)
                        ? "-"
                        : example.argm);

                SetRowText(
                    row,
                    "TypeText",
                    string.IsNullOrWhiteSpace(example.type)
                        ? "-"
                        : example.type);

                SetRowText(
                    row,
                    "StartText",
                    FormatPosition(
                        example.startPointPosition,
                        example.startList));

                SetRowText(
                    row,
                    "EndText",
                    FormatPosition(
                        example.endPointPosition,
                        example.endList));

                SetRowText(
                    row,
                    "DurationText",
                    example.time_consuming.ToString("0.##"));
            }
            // 所有行创建完成后立即重新计算布局。
            Canvas.ForceUpdateCanvases();

            if (content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            }

            ConfigureExampleScrollView();
            if (_exampleScrollRect != null)
                _exampleScrollRect.verticalNormalizedPosition = 1f;
        }

        private static string FormatPosition(
            float position,
            float list)
        {
            return $"{position:0.##} / {list:0.##}";
        }

        private static void SetRowText(
            GameObject row,
            string childName,
            string value)
        {
            TMP_Text[] texts =
                row.GetComponentsInChildren<TMP_Text>(true);

            foreach (TMP_Text textComponent in texts)
            {
                if (textComponent.gameObject.name == childName)
                {
                    textComponent.text =
                        string.IsNullOrWhiteSpace(value)
                            ? "-"
                            : value;

                    return;
                }
            }

            Debug.LogWarning(
                $"[SceneExampleViewer] 行预制体中找不到：" +
                childName);
        }

        private void ClearRows()
        {
            foreach (GameObject row in _createdRows)
            {
                if (row != null)
                {
                    row.SetActive(false);
                    Destroy(row);
                }
            }

            _createdRows.Clear();
        }

        private void ShowExampleViewer()
        {
            if(_introductionView!=null)_introductionView.Hide();
            if (introductionPanel != null)
            {
                introductionPanel.SetActive(false);
            }

            if (exampleViewerPanel != null)
            {
                exampleViewerPanel.SetActive(true);
            }

            ConfigureExamplePageLayout();
            ConfigureViewerControls();
        }

        private void BackToIntroduction()
        {
            if (_isLoading)
            {
                return;
            }

            if (exampleViewerPanel != null)
            {
                exampleViewerPanel.SetActive(false);
            }

            if (introductionPanel != null)
            {
                introductionPanel.SetActive(true);
            }
            ShowIntroductionPanel();
        }

        /// <summary>
        /// 从三维准备画面返回本实验的介绍页。
        /// 介绍页的多级父节点可能已被 UMP_Manager 隐藏，必须逐级恢复。
        /// </summary>
        public void ShowIntroductionFrom3D()
        {
            _isLoading = false;

            if (_preExperimentQuestionnaire != null)
                _preExperimentQuestionnaire.Hide();

            if (exampleViewerPanel != null)
            {
                exampleViewerPanel.SetActive(false);
            }

            if (introductionPanel != null)
            {
                Transform current = introductionPanel.transform;
                while (current != null &&
                       current.GetComponent<Canvas>() == null)
                {
                    current.gameObject.SetActive(true);
                    current = current.parent;
                }
            }

            SetLoading(false);
            ShowIntroductionPanel();
        }

        private void SetLoading(bool loading)
        {
            _isLoading = loading;

            if (decisionSimulationButton != null)
            {
                decisionSimulationButton.interactable =
                    !loading;
            }

            if (refreshButton != null)
            {
                refreshButton.interactable =
                    !loading;
            }

            if (backButton != null)
            {
                backButton.interactable =
                    !loading;
            }

            if (enter3DButton != null)
            {
                enter3DButton.interactable =
                    !loading && _hasLoadedExamples;
            }
        }

        public bool OpenPostExperimentQuestionnaire()
        {
            var workspace = SceneDecisionWorkspaceController.Instance;
            if (workspace == null || !workspace.ExperimentCompleted) return false;
            OpenPreExperimentQuestionnaire();
            return _preExperimentQuestionnaire != null && _preExperimentQuestionnaire.gameObject.activeInHierarchy;
        }

        private void OpenPreExperimentQuestionnaire()
        {
            EnsurePreExperimentQuestionnaire();
            if (_preExperimentQuestionnaire == null)
            {
                ShowError("无法打开实验结束答题页面。");
                return;
            }

            SceneNumericalExampleDto[] sourceExamples =
                SceneNumericalExampleRuntime.OriginalExamples;
            if (sourceExamples == null || sourceExamples.Length == 0)
                sourceExamples = SceneNumericalExampleRuntime.Examples;

            // 算例页和答题页必须互斥；QuestionnaireLayer 位于独立层级，
            // 仅在其内部置顶无法盖住仍激活的算例页。
            if (exampleViewerPanel != null)
                exampleViewerPanel.SetActive(false);

            _preExperimentQuestionnaire.Open(
                SceneNumericalExampleRuntime.CourseTaskId,
                sourceExamples);
        }

        private void EnsurePreExperimentQuestionnaire()
        {
            if (_preExperimentQuestionnaire != null)
                return;

            Canvas canvas =
                ExperimentAssessmentCanvasHost.GetOrCreate(this);
            Transform parent =
                ExperimentAssessmentCanvasHost.GetLayer(
                    canvas,
                    ExperimentAssessmentCanvasHost.QuestionnaireLayer);
            if (parent == null)
                return;

            var pagePrefab = Resources.Load<GameObject>("PreExperimentQuestionnaire");
            GameObject pageObject = pagePrefab != null ? Instantiate(pagePrefab, parent, false)
                : CreateUiObject("PreExperimentQuestionnaire", parent);
            RectTransform pageRect =
                pageObject.GetComponent<RectTransform>();
            pageRect.anchorMin = Vector2.zero;
            pageRect.anchorMax = Vector2.one;
            pageRect.offsetMin = Vector2.zero;
            pageRect.offsetMax = Vector2.zero;

            _preExperimentQuestionnaire = pageObject.GetComponent<PreExperimentQuestionnaireController>();
            if (_preExperimentQuestionnaire == null)
                _preExperimentQuestionnaire = pageObject.AddComponent<PreExperimentQuestionnaireController>();
            _preExperimentQuestionnaire.Initialize(
                OnPreExperimentQuestionnaireCompleted, () =>
                {
                    _preExperimentQuestionnaire.Hide();
                    SceneDecisionWorkspaceController.Instance?.ShowExperimentCompletion();
                });
        }

        private void OnPreExperimentQuestionnaireCompleted()
        {
            _preExperimentQuestionnaireCompleted =
                PreExperimentAssessmentRuntime.IsCompleted;
            if (!_preExperimentQuestionnaireCompleted)
            {
                ShowError("答题记录不完整，请重新完成全部问题。");
                return;
            }

            if (exampleViewerPanel != null)
                exampleViewerPanel.SetActive(false);

            if (_taskSelectionController != null)
                _taskSelectionController.Hide();
            ConfigureCaseActionButton();
            Debug.Log(
                "[SceneExampleViewer] 实验后第1至8题作答完成，统一保存并返回实验介绍。答案数量=" +
                PreExperimentAssessmentRuntime.Answers.Length);
            SceneDecisionWorkspaceController.Instance?.CompletePostExperimentAssessment();
        }

        private void ShowIntroductionPanel()
        {
            if (_introductionView == null)
            {
                var prefab = Resources.Load<ExperimentIntroductionView>("ExperimentIntroductionView");
                var oldCanvas = introductionPanel == null ? GetComponentInParent<Canvas>(true)
                    : introductionPanel.GetComponentInParent<Canvas>(true);
                if (prefab != null && oldCanvas != null)
                {
                    _introductionView = Instantiate(prefab);
                    if(gameObject.scene.name=="DontDestroyOnLoad")DontDestroyOnLoad(_introductionView.gameObject);
                    else UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_introductionView.gameObject, gameObject.scene);
                    _introductionView.Initialize(oldCanvas.transform, OpenAndLoadExamples);
                }
            }
            if (_introductionView != null) _introductionView.Show();
            if (introductionPanel == null)
                return;

            Transform current = introductionPanel.transform;
            while (current != null &&
                   current.GetComponent<Canvas>() == null)
            {
                current.gameObject.SetActive(true);
                current = current.parent;
            }
        }

        private void EnsureTaskSelectionController()
        {
            if (_taskSelectionController != null)
                return;

            Canvas canvas =
                ExperimentAssessmentCanvasHost.GetOrCreate(this);
            Transform parent =
                ExperimentAssessmentCanvasHost.GetLayer(
                    canvas,
                    ExperimentAssessmentCanvasHost.TaskSelectionLayer);
            if (parent == null)
            {
                Debug.LogError(
                    "[SceneExampleViewer] 找不到任务选择页的Canvas。" );
                return;
            }

            GameObject pageObject = CreateUiObject(
                "ExperimentTaskSelection",
                parent);
            RectTransform pageRect =
                pageObject.GetComponent<RectTransform>();
            pageRect.anchorMin = Vector2.zero;
            pageRect.anchorMax = Vector2.one;
            pageRect.offsetMin = Vector2.zero;
            pageRect.offsetMax = Vector2.zero;
            pageObject.transform.SetAsLastSibling();

            _taskSelectionController =
                pageObject.AddComponent<
                    ExperimentTaskSelectionController>();
            _taskSelectionController.Initialize(
                taskStatuses,
                taskExperimentId,
                taskPageSize,
                testTaskId,
                allowLocalTestTask,
                OnExperimentTaskSelected);
        }

        private void OnExperimentTaskSelected(
            ExperimentTaskSelectionResult selection)
        {
            if (selection == null ||
                selection.Task == null ||
                string.IsNullOrWhiteSpace(selection.Task.taskId))
            {
                Debug.LogError(
                    "[SceneExampleViewer] 选择的教师任务无效。" );
                if (_taskSelectionController != null)
                    _taskSelectionController.Open();
                return;
            }

            _selectedTaskIsLocal = selection.IsLocalTestTask;
            SetSelectedTask(selection.Task.taskId);
            StudentAssessmentRuntime.SelectTask(
                selection.Task,
                selection.IsLocalTestTask);

            if (_continueToExamplesAfterTaskSelection)
            {
                _continueToExamplesAfterTaskSelection = false;
                OpenAndLoadExamples();
                return;
            }

            if (exampleViewerPanel != null)
                exampleViewerPanel.SetActive(false);
            ShowIntroductionPanel();
            SetDecisionButtonLabel("查看算例");

            Debug.Log(
                "[SceneExampleViewer] 已选择实验任务：" +
                selection.Task.taskId +
                (selection.IsLocalTestTask
                    ? "（本地测试）"
                    : "（服务器）"));
        }

        private void SetDecisionButtonLabel(string value)
        {
            if (decisionSimulationButton == null)
                return;
            TMP_Text label =
                decisionSimulationButton.GetComponentInChildren<
                    TMP_Text>(true);
            if (label != null)
                label.text = value;
        }

        private void ResetQuestionnaireState()
        {
            _preExperimentQuestionnaireCompleted = false;
            PreExperimentAssessmentRuntime.Clear();
            if (_preExperimentQuestionnaire != null)
                _preExperimentQuestionnaire.Hide();
            ConfigureViewerControls();
        }

        private void ConfigureCaseActionButton()
        {
            ConfigureViewerControls();

            SetEnterButtonLabel("设置参数");

            if (enter3DButton != null)
                enter3DButton.interactable =
                    !_isLoading && _hasLoadedExamples;
        }

        private void ConfigureViewerControls()
        {
            bool initialAssessment =
                !_preExperimentQuestionnaireCompleted;

            if (backButton != null)
            {
                backButton.gameObject.SetActive(true);
                backButton.interactable =
                    !_isLoading;
            }

            if (refreshButton != null)
            {
                refreshButton.gameObject.SetActive(initialAssessment);
                refreshButton.interactable =
                    initialAssessment && !_isLoading;
            }

            SetEnterButtonLabel("设置参数");
        }

        private void UpdateReviewSummary()
        {
            if (summaryText == null)
                return;

            summaryText.gameObject.SetActive(true);
            summaryText.text =
                "任务编号：" +
                SceneNumericalExampleRuntime.CourseTaskId +
                "    当前流程：" +
                GetExperimentTaskType() +
                "    算例数量：" +
                SceneNumericalExampleRuntime.Examples.Length;
        }

        private void ConfigureExamplePageLayout()
        {
            // 新页面的布局由 ExperimentAssessmentCanvas.prefab 保存，
            // 运行时不再覆写组件在 Inspector 中的锚点和尺寸。
            if (_exampleSelectionCanvasView != null)
                return;

            if (titleText != null)
            {
                RectTransform titleRect = titleText.rectTransform;
                titleRect.anchorMin = new Vector2(0.08f, 1f);
                titleRect.anchorMax = new Vector2(0.92f, 1f);
                titleRect.pivot = new Vector2(0.5f, 1f);
                titleRect.offsetMin = new Vector2(0f, -64f);
                titleRect.offsetMax = new Vector2(0f, -12f);
                titleText.alignment = TextAlignmentOptions.Center;
                titleText.enableWordWrapping = false;
            }

            if (summaryText != null)
            {
                RectTransform summaryRect = summaryText.rectTransform;
                summaryRect.anchorMin = new Vector2(0.08f, 1f);
                summaryRect.anchorMax = new Vector2(0.92f, 1f);
                summaryRect.pivot = new Vector2(0.5f, 1f);
                summaryRect.offsetMin = new Vector2(0f, -108f);
                summaryRect.offsetMax = new Vector2(0f, -70f);
                summaryText.alignment = TextAlignmentOptions.Center;
                summaryText.enableWordWrapping = false;
                summaryText.enableAutoSizing = true;
                summaryText.fontSizeMin = 15f;
            }

            if (statusText != null)
            {
                RectTransform statusRect = statusText.rectTransform;
                statusRect.anchorMin = new Vector2(0.06f, 0f);
                statusRect.anchorMax = new Vector2(0.94f, 0f);
                statusRect.pivot = new Vector2(0.5f, 0f);
                statusRect.offsetMin = new Vector2(0f, 12f);
                statusRect.offsetMax = new Vector2(0f, 54f);
                statusText.alignment = TextAlignmentOptions.Center;
                statusText.enableWordWrapping = false;
                statusText.enableAutoSizing = true;
                statusText.fontSizeMin = 14f;
            }
        }

        private void ConfigureExampleScrollView()
        {
            if (content == null)
                return;

            if (_exampleScrollRect == null)
                _exampleScrollRect =
                    content.GetComponentInParent<ScrollRect>();
            if (_exampleScrollRect == null)
                return;

            _exampleScrollRect.horizontal = false;
            _exampleScrollRect.vertical = true;
            _exampleScrollRect.movementType =
                ScrollRect.MovementType.Clamped;
            _exampleScrollRect.inertia = true;
            _exampleScrollRect.decelerationRate = 0.135f;
            _exampleScrollRect.scrollSensitivity =
                ExampleScrollSensitivity;

            if (_exampleVerticalScrollbar == null)
                _exampleVerticalScrollbar =
                    _exampleScrollRect.verticalScrollbar;

            if (_exampleVerticalScrollbar == null)
            {
                Debug.LogError(
                    "[SceneExampleViewer] 实验算例 Canvas 缺少纵向滚动条引用。");
                return;
            }

            _exampleScrollRect.verticalScrollbar =
                _exampleVerticalScrollbar;
            _exampleScrollRect.verticalScrollbarVisibility =
                ScrollRect.ScrollbarVisibility.Permanent;
            _exampleScrollRect.verticalScrollbarSpacing = 6f;

            RectTransform viewport = _exampleScrollRect.viewport;
            if (viewport != null)
            {
                Vector2 offsetMax = viewport.offsetMax;
                offsetMax.x = Mathf.Min(offsetMax.x, -28f);
                viewport.offsetMax = offsetMax;
            }
        }

        private void SetEnterButtonLabel(string value)
        {
            if (enter3DButton == null)
                return;

            TMP_Text label =
                enter3DButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = value;
        }

        private void ShowError(string message)
        {
            _hasLoadedExamples = false;

            if (enter3DButton != null)
            {
                enter3DButton.interactable = false;
            }

            SetStatus(message, ErrorColor);
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
            statusText.gameObject.SetActive(true);
            ConfigureExamplePageLayout();
            statusText.transform.SetAsLastSibling();
        }

        /// <summary>显示 Canvas Prefab 中预先配置的算例来源提示框。</summary>
        private void ShowLocalFallbackNotice(string message)
        {
            if (_exampleSelectionCanvasView != null &&
                _exampleSelectionCanvasView.FallbackNotice != null)
                _exampleSelectionCanvasView.FallbackNotice.Show(message);
        }

        private void HideLocalFallbackNotice()
        {
            if (_exampleSelectionCanvasView != null &&
                _exampleSelectionCanvasView.FallbackNotice != null)
                _exampleSelectionCanvasView.FallbackNotice.Hide();
        }

        private static GameObject CreateUiObject(
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
    }
}
