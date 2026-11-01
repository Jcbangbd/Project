using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Vtp.VR.Integration;
using Vtp.VR.Rest;

/// <summary>
/// REST 登录版 AccountManager。
/// 用本文件替换原 Scripts/Managers/AccountManager.cs，不能与原类同时存在。
/// </summary>
public class AccountManager : MonoBehaviour
{
    [Header("登录界面")]
    public GameObject loginPanel;
    public TMP_InputField userNameLoginInput;
    public TMP_InputField passwordLoginInput;
    public Button loginButton;

    [Header("修改密码界面")]
    public GameObject alterPanel;
    public TMP_InputField userNameAlterInput;
    public TMP_InputField oldPasswordInput;
    public TMP_InputField newPasswordInput;
    public TMP_InputField confirmPasswordInput;
    public Button openAlterButton;
    public Button submitAlterButton;
    public Button backToLoginButton;

    [Header("状态显示")]
    public TMP_Text statusText;
    public float tipShowTime = 3f;
    public GameObject loadingPanel;
    public TMP_Text loadingText;

    [Header("远程任务")]
    public string gameSceneName = "SceneStage1";
    public string taskStatuses = "PUBLISHED,ACKNOWLEDGED";
    public string experimentId = "";
    public int taskPageSize = 20;
    [Tooltip("当前版本没有独立任务选择 UI，因此默认加载服务器返回的第一项。")]
    public bool autoLoadFirstTask = true;

    [Header("服务器算例接口")]
    [Tooltip("findSceneNumericalExample接口的taskId参数。")]
    public string courseTaskId = "kc1";

    [Tooltip("远程算例转本地轨道吊调度的协调器。")]
    public SceneExampleAlgorithmCoordinator
        sceneExampleAlgorithmCoordinator;


    [Header("任务与算法流水线")]
    [Tooltip("指向登录场景LocalAlgorithmSystem上的TaskExecutionCoordinator。")]
    public TaskExecutionCoordinator taskExecutionCoordinator;

    [Header("登录后场景")]
    public string taskSelectionSceneName = "SceneStage1";

    private bool _isProcessing;
    private RestApiClient _client;
    private AuthService _authService;
    private VrTaskService _taskService;
    private Coroutine _tipCoroutine;
    private readonly Dictionary<GameObject, bool>
        _loginReloadButtonStates =
            new Dictionary<GameObject, bool>();

    private static readonly Vector2 LoginFeedbackSize =
        new Vector2(720f, 48f);
    private const float LoginFeedbackBottomMargin = 34f;

    private SceneNumericalExampleService  _sceneNumericalExampleService;

    private void Awake()
    {
        PortLoginView.Install(this);
        _client = RestApiClient.EnsureInstance();
        _authService = new AuthService(_client);
        _taskService = new VrTaskService(_client);
        _sceneNumericalExampleService = new SceneNumericalExampleService(_client);

        if (taskExecutionCoordinator == null)
        {
            taskExecutionCoordinator =
                FindObjectOfType<TaskExecutionCoordinator>();
        }

        if (sceneExampleAlgorithmCoordinator == null)
        {
            sceneExampleAlgorithmCoordinator =
                FindObjectOfType<
                    SceneExampleAlgorithmCoordinator>();
        }

        if (loginButton != null)
            loginButton.onClick.AddListener(OnLoginButtonClick);
        if (openAlterButton != null)
            openAlterButton.onClick.AddListener(OnOpenAlterPanelClick);
        if (submitAlterButton != null)
            submitAlterButton.onClick.AddListener(OnSubmitChangePasswordClick);
        if (backToLoginButton != null)
            backToLoginButton.onClick.AddListener(OnBackToLoginClick);

        ConfigureLoginInput(userNameLoginInput);
        ConfigureLoginInput(passwordLoginInput);

        if (loginPanel != null)
            loginPanel.SetActive(true);
        if (alterPanel != null)
            alterPanel.SetActive(false);
        if (statusText != null)
            statusText.gameObject.SetActive(false);
        SetLoading(false, null);
    }

    private void Update()
    {
        if (_isProcessing ||
            loginPanel == null ||
            !loginPanel.activeInHierarchy)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            bool reverse =
                Input.GetKey(KeyCode.LeftShift) ||
                Input.GetKey(KeyCode.RightShift);
            MoveLoginFocus(reverse ? -1 : 1);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            InvokeSelectedLoginButton();
        }
    }

    private void MoveLoginFocus(int direction)
    {
        Selectable[] order =
        {
            userNameLoginInput,
            passwordLoginInput,
            loginButton,
            openAlterButton
        };

        GameObject selected = EventSystem.current == null
            ? null
            : EventSystem.current.currentSelectedGameObject;
        int currentIndex = -1;
        for (int index = 0; index < order.Length; index++)
        {
            if (order[index] != null &&
                order[index].gameObject == selected)
            {
                currentIndex = index;
                break;
            }
        }

        if (currentIndex < 0 && direction < 0)
            currentIndex = 0;

        for (int offset = 1; offset <= order.Length; offset++)
        {
            int nextIndex =
                (currentIndex + direction * offset +
                 order.Length * 2) % order.Length;
            Selectable next = order[nextIndex];
            if (next == null || !next.IsInteractable() ||
                !next.gameObject.activeInHierarchy)
            {
                continue;
            }

            TMP_InputField input = next as TMP_InputField;
            if (input != null)
                FocusInput(input);
            else
                next.Select();
            return;
        }
    }

    private void InvokeSelectedLoginButton()
    {
        if (EventSystem.current == null)
            return;

        GameObject selected =
            EventSystem.current.currentSelectedGameObject;
        Button button = selected == null
            ? null
            : selected.GetComponent<Button>();
        if (button == null || !button.IsInteractable() ||
            (button != loginButton && button != openAlterButton))
        {
            return;
        }

        button.onClick.Invoke();
    }

    private void OnDestroy()
    {
        if (loginButton != null)
            loginButton.onClick.RemoveListener(OnLoginButtonClick);
        if (openAlterButton != null)
            openAlterButton.onClick.RemoveListener(OnOpenAlterPanelClick);
        if (submitAlterButton != null)
            submitAlterButton.onClick.RemoveListener(OnSubmitChangePasswordClick);
        if (backToLoginButton != null)
            backToLoginButton.onClick.RemoveListener(OnBackToLoginClick);
    }

    public void OnLoginButtonClick()
    {
        if (_isProcessing)
            return;

        string userName = userNameLoginInput == null
            ? string.Empty
            : userNameLoginInput.text.Trim();
        string password = passwordLoginInput == null
            ? string.Empty
            : passwordLoginInput.text;

        if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
        {
            ShowTip("用户名和密码不能为空。", Color.red);
            return;
        }

        _isProcessing = true;
        SetInteractable(false);
        SetLoading(true, "服务器正在连接……");

        _authService.Login(
        userName,
        password,
        delegate (LoginResultDto login)
        {
            if (passwordLoginInput != null)
                passwordLoginInput.text = string.Empty;

            // 评分流程第1步：只有服务器认证成功后才建立本次学生实验记录。
            StudentAssessmentRuntime.BeginSession(userName);

            Debug.Log(
                "[Login] REST登录成功，Token已保存，进入任务选择场景。");

            _isProcessing = false;
            SetInteractable(true);
            SetLoading(false, null);

            SceneManager.LoadScene(taskSelectionSceneName);
    },
    HandleOperationError);
    }

    private void LoadRemoteTasks()
    {
        string taskId =
            (courseTaskId ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(taskId))
        {
            HandleOperationError(
                RestApiError.Client(
                    "教师任务编号不能为空。"));

            return;
        }

        SetLoading(
            true,
            "正在获取教师下发的算例……");

        _sceneNumericalExampleService.GetByTaskId(
            taskId,

            delegate (
                SceneNumericalExampleDto[] examples)
            {
                if (examples == null ||
                    examples.Length == 0)
                {
                    FinishWithoutScene(
                        "登录成功，但任务 " +
                        taskId +
                        " 没有算例数据。",
                        Color.yellow);

                    return;
                }

                SceneNumericalExampleRuntime.Set(
                    taskId,
                    examples);

                Debug.Log(
                    "[SceneExample] 获取算例成功，" +
                    "taskId=" +
                    taskId +
                    "，数量=" +
                    examples.Length);

                for (int index = 0;
                     index < examples.Length;
                     index++)
                {
                    SceneNumericalExampleDto item =
                        examples[index];

                    if (item == null)
                    {
                        continue;
                    }

                    Debug.Log(
                        "[SceneExample] " +
                        "序号=" +
                        item.no +
                        "，轨道吊=" +
                        item.argm +
                        "，类型=" +
                        item.type +
                        "，起点位置=" +
                        item.startPointPosition +
                        "，起点列=" +
                        item.startList +
                        "，终点位置=" +
                        item.endPointPosition +
                        "，终点列=" +
                        item.endList +
                        "，预计耗时=" +
                        item.time_consuming);
                }

                if (sceneExampleAlgorithmCoordinator == null)
                {
                    HandleOperationError(
                        RestApiError.Client(
                            "场景中没有 SceneExampleAlgorithmCoordinator。"));
                    return;
                }

                SetLoading(
                    true,
                    "算例已获取，正在计算轨道吊任务序列……");

                //Debug.Log( "[SceneExample] 本次传入算法的完整算例数量=" + examples.Length);

                const int maxTaskCount = 24;

                int selectedCount = Mathf.Min(
                    maxTaskCount,
                    examples.Length);

                SceneNumericalExampleDto[] selectedExamples =
                    new SceneNumericalExampleDto[selectedCount];

                Array.Copy(
                    examples,
                    selectedExamples,
                    selectedCount);

                Debug.Log(
                    "[SceneExample] 服务器返回算例数量=" +
                    examples.Length +
                    "，本次传入算法数量=" +
                    selectedExamples.Length);

                sceneExampleAlgorithmCoordinator.Run(
                    taskId,
                    selectedExamples,
                    delegate (SchedulingResultDto schedule)
                    {
                        SetLoading(
                            true,
                            "轨道吊任务序列已生成，正在进入实验场景……");
                        OpenGameScene();
                    },
                    delegate (string progress)
                    {
                        SetLoading(true, progress);
                    },
                    delegate (string error)
                    {
                        HandleOperationError(
                            RestApiError.Client(error));
                    });
            },

            HandleOperationError);
    }

    /// <summary>
    /// 后续增加任务选择 UI 时，按钮可直接调用本方法。
    /// </summary>
    public void SelectTask(string taskId)
    {
        if (string.IsNullOrEmpty(taskId))
        {
            HandleOperationError(RestApiError.Client("选择的 taskId 为空。"));
            return;
        }

        _isProcessing = true;
        SetInteractable(false);

        if (taskExecutionCoordinator == null)
        {
            HandleOperationError(
                RestApiError.Client(
                    "场景中没有TaskExecutionCoordinator。"));
            return;
        }

        SetLoading(true, "正在准备任务、算法和调度序列……");
        taskExecutionCoordinator.PrepareTask(
            taskId,
            delegate
            {
                SetLoading(true, "调度序列已生成，正在进入实验……");
                OpenGameScene();
            },
            delegate(string progress)
            {
                SetLoading(true, progress);
            },
            HandleOperationError);
    }

    private void OpenGameScene()
    {
        string scene = RemoteTaskRuntime.CurrentCase == null
            ? null
            : RemoteTaskRuntime.CurrentCase.sceneId;
        if (string.IsNullOrEmpty(scene))
            scene = gameSceneName;

        _isProcessing = false;
        SceneManager.LoadScene(scene);
    }

    public void OnOpenAlterPanelClick()
    {
        if (_isProcessing || alterPanel == null || loginPanel == null)
            return;

        alterPanel.SetActive(true);
        loginPanel.SetActive(false);
        if (userNameAlterInput != null && userNameLoginInput != null)
            userNameAlterInput.text = userNameLoginInput.text;
    }

    public void OnBackToLoginClick()
    {
        if (_isProcessing || alterPanel == null || loginPanel == null)
            return;

        alterPanel.SetActive(false);
        loginPanel.SetActive(true);
    }

    public void OnSubmitChangePasswordClick()
    {
        if (_isProcessing)
            return;

        string userName = userNameAlterInput == null
            ? string.Empty
            : userNameAlterInput.text.Trim();
        string oldPassword = oldPasswordInput == null
            ? string.Empty
            : oldPasswordInput.text;
        string newPassword = newPasswordInput == null
            ? string.Empty
            : newPasswordInput.text;
        string confirmPassword = confirmPasswordInput == null
            ? string.Empty
            : confirmPasswordInput.text;

        if (string.IsNullOrEmpty(userName) ||
            string.IsNullOrEmpty(oldPassword) ||
            string.IsNullOrEmpty(newPassword))
        {
            ShowTip("用户名和密码不能为空。", Color.red);
            return;
        }

        if (newPassword != confirmPassword)
        {
            ShowTip("两次输入的新密码不一致。", Color.red);
            return;
        }

        _isProcessing = true;
        SetInteractable(false);
        SetLoading(true, "正在验证原密码……");

        // REST changePwd 依赖已登录 token，因此先用原密码完成认证。
        _authService.Login(
            userName,
            oldPassword,
            delegate(LoginResultDto ignored)
            {
                SetLoading(true, "正在修改密码……");
                _authService.ChangePassword(
                    oldPassword,
                    newPassword,
                    delegate
                    {
                        _authService.Logout(delegate
                        {
                            ClearPasswordFields();
                            if (alterPanel != null) alterPanel.SetActive(false);
                            if (loginPanel != null) loginPanel.SetActive(true);
                            FinishWithoutScene(
                                "密码修改成功，请使用新密码登录。",
                                Color.green);
                        });
                    },
                    HandleOperationError);
            },
            HandleOperationError);
    }

    public void OnReconnectButtonClick()
    {
        if (_isProcessing)
            return;
        OnLoginButtonClick();
    }

    private void ClearPasswordFields()
    {
        if (oldPasswordInput != null) oldPasswordInput.text = string.Empty;
        if (newPasswordInput != null) newPasswordInput.text = string.Empty;
        if (confirmPasswordInput != null) confirmPasswordInput.text = string.Empty;
        if (passwordLoginInput != null) passwordLoginInput.text = string.Empty;
    }

    private void HandleOperationError(RestApiError error)
    {
        _isProcessing = false;
        SetInteractable(true);
        SetLoading(false, null);
        ShowTip("操作失败：" + error.Message, Color.red);
    }

    private void FinishWithoutScene(string message, Color color)
    {
        _isProcessing = false;
        SetInteractable(true);
        SetLoading(false, null);
        ShowTip(message, color);
    }

    private void SetLoading(bool visible, string message)
    {
        if (visible && statusText != null)
        {
            statusText.gameObject.SetActive(false);

            if (_tipCoroutine != null)
            {
                StopCoroutine(_tipCoroutine);
                _tipCoroutine = null;
            }
        }

        if (loadingPanel != null)
            loadingPanel.SetActive(visible);

        // 登录请求期间隐藏旧登录场景中单独存在的“重载”按钮。
        // 该按钮没有稳定的Inspector引用，因此按按钮文字和对象名识别，
        // 请求结束后再恢复它原来的显示状态。
        SetLoginReloadButtonsHidden(visible);

        if (loadingText != null && message != null)
        {
            loadingText.text = message;
            PositionFeedbackAtScreenBottom(loadingText);
        }
    }

    private void SetLoginReloadButtonsHidden(bool hidden)
    {
        if (hidden)
        {
            _loginReloadButtonStates.Clear();
            Button[] buttons = FindObjectsOfType<Button>(true);
            for (int index = 0; index < buttons.Length; index++)
            {
                Button button = buttons[index];
                if (!IsLoginReloadButton(button))
                    continue;

                GameObject buttonObject = button.gameObject;
                _loginReloadButtonStates[buttonObject] =
                    buttonObject.activeSelf;
                buttonObject.SetActive(false);
            }

            return;
        }

        foreach (KeyValuePair<GameObject, bool> pair
                 in _loginReloadButtonStates)
        {
            if (pair.Key != null)
                pair.Key.SetActive(pair.Value);
        }

        _loginReloadButtonStates.Clear();
    }

    private static bool IsLoginReloadButton(Button button)
    {
        if (button == null)
            return false;

        TMP_Text tmpLabel =
            button.GetComponentInChildren<TMP_Text>(true);
        if (tmpLabel != null &&
            string.Equals(
                (tmpLabel.text ?? string.Empty).Trim(),
                "重载",
                StringComparison.Ordinal))
        {
            return true;
        }

        Text legacyLabel =
            button.GetComponentInChildren<Text>(true);
        if (legacyLabel != null &&
            string.Equals(
                (legacyLabel.text ?? string.Empty).Trim(),
                "重载",
                StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(
                   button.gameObject.name,
                   "ReloadButton",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   button.gameObject.name,
                   "ReconnectButton",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static void ConfigureLoginInput(TMP_InputField input)
    {
        if (input == null)
            return;

        input.lineType = TMP_InputField.LineType.SingleLine;
        // 不单独指定光标颜色：TMP 会实时继承输入文字颜色，
        // 因此用户名和密码使用不同字色时，光标也会分别保持一致。
        input.customCaretColor = false;
        input.caretWidth = 2;
        input.caretBlinkRate = 0.8f;
    }

    private static void FocusInput(TMP_InputField input)
    {
        if (input == null || !input.IsInteractable())
            return;

        input.Select();
        input.ActivateInputField();
        input.MoveTextEnd(false);
    }

    private void PositionFeedbackAtScreenBottom(TMP_Text feedbackText)
    {
        if (feedbackText == null)
            return;

        RectTransform feedbackRect = feedbackText.rectTransform;
        RectTransform feedbackParent =
            feedbackRect.parent as RectTransform;

        if (feedbackParent == null)
            return;

        Canvas.ForceUpdateCanvases();

        Canvas canvas = feedbackText.canvas;
        Camera eventCamera =
            canvas != null &&
            canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                feedbackParent,
                new Vector2(
                    Screen.width * 0.5f,
                    LoginFeedbackBottomMargin),
                eventCamera,
                out localPoint))
        {
            return;
        }

        feedbackRect.anchorMin = new Vector2(0.5f, 0.5f);
        feedbackRect.anchorMax = new Vector2(0.5f, 0.5f);
        feedbackRect.pivot = new Vector2(0.5f, 0f);
        feedbackRect.sizeDelta = LoginFeedbackSize;
        feedbackRect.localPosition = new Vector3(
            localPoint.x,
            localPoint.y,
            feedbackRect.localPosition.z);

        feedbackText.alignment = TextAlignmentOptions.Center;
        feedbackText.overflowMode = TextOverflowModes.Overflow;
        feedbackText.raycastTarget = false;
        feedbackRect.SetAsLastSibling();
    }

    private void SetInteractable(bool interactable)
    {
        if (loginButton != null) loginButton.interactable = interactable;
        if (userNameLoginInput != null)
            userNameLoginInput.interactable = interactable;
        if (passwordLoginInput != null)
            passwordLoginInput.interactable = interactable;
        if (openAlterButton != null)
            openAlterButton.interactable = interactable;
        if (userNameAlterInput != null)
            userNameAlterInput.interactable = interactable;
        if (oldPasswordInput != null)
            oldPasswordInput.interactable = interactable;
        if (newPasswordInput != null)
            newPasswordInput.interactable = interactable;
        if (confirmPasswordInput != null)
            confirmPasswordInput.interactable = interactable;
        if (submitAlterButton != null)
            submitAlterButton.interactable = interactable;
        if (backToLoginButton != null)
            backToLoginButton.interactable = interactable;
    }

    private void ShowTip(string message, Color color)
    {
        if (statusText == null)
            return;

        statusText.text = message;
        statusText.color = color;
        statusText.gameObject.SetActive(true);
        PositionFeedbackAtScreenBottom(statusText);

        if (_tipCoroutine != null)
            StopCoroutine(_tipCoroutine);

        _tipCoroutine = StartCoroutine(HideTipAfterSeconds(tipShowTime));
    }

    private IEnumerator HideTipAfterSeconds(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (statusText != null)
            statusText.gameObject.SetActive(false);
        _tipCoroutine = null;
    }
}
