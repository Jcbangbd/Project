using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vtp.VR.Rest;

namespace Vtp.VR.Integration
{
    public sealed class ExperimentTaskSelectionResult
    {
        public VrTaskSummaryDto Task;
        public bool IsLocalTestTask;
    }

    /// <summary>
    /// 评分流程第2步：显示教师下发任务列表，由学生明确选择一项任务。
    /// 场景无需手工增加对象，SceneExampleViewer会在运行时创建本页面。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExperimentTaskSelectionController : MonoBehaviour
    {
        private const string FontResourcePath =
            "Fonts/AlibabaPuHuiTi-3-55-Regular SDF";

        private static readonly Color32 Yellow =
            ExperimentUiTheme.Primary;
        private static readonly Color32 CardGray =
            ExperimentUiTheme.Panel;
        private static readonly Color32 RowGray =
            ExperimentUiTheme.PanelRaised;

        private CanvasGroup _group;
        private RectTransform _content;
        private TMP_Text _statusText;
        private Button _reloadButton;
        private TMP_FontAsset _font;
        private VrTaskService _service;
        private Action<ExperimentTaskSelectionResult> _onSelected;
        private string _statuses;
        private string _experimentId;
        private int _pageSize;
        private string _localTaskId;
        private bool _allowLocalTask;
        private bool _loading;

        public void Initialize(
            string statuses,
            string experimentId,
            int pageSize,
            string localTaskId,
            bool allowLocalTask,
            Action<ExperimentTaskSelectionResult> onSelected)
        {
            _statuses = statuses ?? string.Empty;
            _experimentId = experimentId ?? string.Empty;
            _pageSize = Mathf.Max(1, pageSize);
            _localTaskId = string.IsNullOrWhiteSpace(localTaskId)
                ? "kc1"
                : localTaskId.Trim();
            _allowLocalTask = allowLocalTask;
            _onSelected = onSelected;
            _service = new VrTaskService(RestApiClient.EnsureInstance());

            if (_group == null)
                BuildInterface();
        }

        public void Open()
        {
            if (_group == null)
                return;
            gameObject.SetActive(true);
            _group.alpha = 1f;
            _group.interactable = true;
            _group.blocksRaycasts = true;
            Reload();
        }

        public void Hide()
        {
            if (_group == null)
                return;
            gameObject.SetActive(true);
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
        }

        private void Reload()
        {
            if (_loading || _service == null)
                return;

            ClearRows();
            if (!RestSession.IsAuthenticated)
            {
                ShowUnavailable(
                    "登录状态已失效，无法读取教师任务列表。" );
                return;
            }

            _loading = true;
            _reloadButton.interactable = false;
            SetStatus("正在从服务器获取教师下发的实验任务……", ExperimentUiTheme.TextColor);

            _service.GetMyTasks(
                _statuses,
                _experimentId,
                1,
                _pageSize,
                OnTasksReceived,
                delegate(RestApiError error)
                {
                    _loading = false;
                    _reloadButton.interactable = true;
                    ShowUnavailable(
                        "教师任务列表获取失败：" +
                        (error == null ? "未知错误" : error.Message));
                });
        }

        private void OnTasksReceived(VrTaskSummaryDto[] tasks)
        {
            _loading = false;
            _reloadButton.interactable = true;
            ClearRows();

            List<VrTaskSummaryDto> available =
                new List<VrTaskSummaryDto>();
            HashSet<string> ids = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            if (tasks != null)
            {
                for (int index = 0; index < tasks.Length; index++)
                {
                    VrTaskSummaryDto task = tasks[index];
                    if (task == null ||
                        string.IsNullOrWhiteSpace(task.taskId) ||
                        !ids.Add(task.taskId.Trim()))
                        continue;
                    available.Add(task);
                }
            }

            RemoteTaskRuntime.SetAvailableTasks(available.ToArray());
            if (available.Count == 0)
            {
                ShowUnavailable("服务器当前没有可选择的教师实验任务。" );
                return;
            }

            for (int index = 0; index < available.Count; index++)
                AddTaskRow(available[index], false, index);

            SetStatus(
                "共获取 " + available.Count +
                " 项教师任务，请选择一项进入。",
                ExperimentUiTheme.Success);
        }

        private void ShowUnavailable(string message)
        {
            ClearRows();
            // 诊断保留在日志中，体验页面不暴露HTTP、联调及内部测试状态。
            // 仅调整显示，IsLocalTestTask等数据来源标记继续保留。
            Debug.LogWarning("[ExperimentTaskSelection] " + message);
            SetStatus(_allowLocalTask ? string.Empty : "暂未获取到实验任务，请点击重新获取。", ExperimentUiTheme.Muted);

            if (!_allowLocalTask)
                return;

            VrTaskSummaryDto local = new VrTaskSummaryDto
            {
                taskId = _localTaskId,
                title = "集装箱码头作业调度实验",
                taskType = "TEST",
                experimentId = "local-test",
                status = "LOCAL_TEST",
                version = 1,
                deadline = string.Empty
            };
            AddTaskRow(local, true, 0);
        }

        private void AddTaskRow(
            VrTaskSummaryDto task,
            bool local,
            int index)
        {
            string title = string.IsNullOrWhiteSpace(task.title)
                ? "未命名实验任务"
                : task.title.Trim();
            string type = local ? "作业调度" : string.IsNullOrWhiteSpace(task.taskType)
                ? "未标注类型"
                : task.taskType.Trim();
            string status = local ? "可进入" : string.IsNullOrWhiteSpace(task.status)
                ? "-"
                : task.status.Trim();
            string deadline = string.IsNullOrWhiteSpace(task.deadline)
                ? "未设置截止时间"
                : task.deadline.Trim();

            Button button = CreateLayoutButton(
                "Task_" + task.taskId,
                _content,
                title + "\n任务编号：" + task.taskId +
                "    类型：" + type +
                "    状态：" + status +
                "    截止：" + deadline,
                Color.white);
            button.onClick.AddListener(
                delegate
                {
                    if (_loading)
                        return;
                    Hide();
                    if (_onSelected != null)
                    {
                        _onSelected(
                            new ExperimentTaskSelectionResult
                            {
                                Task = task,
                                IsLocalTestTask = local
                            });
                    }
                });
        }

        private void BuildInterface()
        {
            _font = ExperimentUiTheme.Font;
            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null)
                _group = gameObject.AddComponent<CanvasGroup>();

            Image blocker = gameObject.GetComponent<Image>();
            if (blocker == null)
                blocker = gameObject.AddComponent<Image>();
            blocker.color = ExperimentUiTheme.Page;
            blocker.raycastTarget = true;

            RectTransform root = GetComponent<RectTransform>();
            Stretch(root);

            RectTransform card = ExperimentUiTheme.DesignSurface(transform, "TaskSelectionCard");
            ExperimentUiTheme.HeaderBar(card, "实验任务选择");

            TMP_Text title = CreateText(
                "TitleText",
                card,
                "选择教师下发的实验任务",
                40f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            SetTopStretch(title.rectTransform, 138f, 72f, 72f, 64f);

            TMP_Text hint = CreateText(
                "HintText",
                card,
                "查看任务列表，选择一项实验进入。",
                30f,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);
            hint.color = ExperimentUiTheme.Muted;
            SetTopStretch(hint.rectTransform, 214f, 72f, 72f, 48f);

            _content = CreateScrollArea(card, 290f, 172f);
            RectTransform footer = ExperimentUiTheme.FooterBar(card);

            _statusText = CreateText(
                "StatusText",
                footer,
                string.Empty,
                22f,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);
            _statusText.color = ExperimentUiTheme.Muted;
            SetBottomStretch(
                _statusText.rectTransform,
                22f,
                72f,
                500f,
                88f);

            _reloadButton = CreateButton(
                "ReloadButton",
                footer,
                "重新获取任务列表",
                new Vector2(0f, 26f),
                new Vector2(300f, 58f));
            ExperimentUiTheme.FooterButton(_reloadButton.GetComponent<RectTransform>(), 72f, 340f);
            _reloadButton.onClick.AddListener(Reload);
        }

        private RectTransform CreateScrollArea(
            RectTransform parent,
            float top,
            float bottom)
        {
            GameObject rootObject = CreateUiObject(
                "TaskScrollView", parent);
            RectTransform rect =
                rootObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(72f, bottom);
            rect.offsetMax = new Vector2(-72f, -top);
            Image scrollBackground = rootObject.AddComponent<Image>();
            ExperimentUiTheme.SciFiSurface(
                scrollBackground,
                ExperimentUiTheme.Page);
            ScrollRect scroll = rootObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;

            GameObject viewportObject = CreateUiObject(
                "Viewport", rect);
            RectTransform viewport =
                viewportObject.GetComponent<RectTransform>();
            Stretch(viewport);
            viewportObject.AddComponent<RectMask2D>();

            GameObject contentObject = CreateUiObject(
                "Content", viewport);
            RectTransform content =
                contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layout =
                contentObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(1, 1, 1, 1);
            layout.spacing = 18f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            contentObject.AddComponent<ContentSizeFitter>()
                .verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            ExperimentUiTheme.Scrollbar(scroll);
            return content;
        }

        private Button CreateLayoutButton(
            string name,
            Transform parent,
            string text,
            Color color)
        {
            GameObject obj = CreateUiObject(name, parent);
            Image image = obj.AddComponent<Image>();
            image.color = color;
            Button button = obj.AddComponent<Button>();
            button.targetGraphic = image;
            ExperimentUiTheme.StyleChoice(button);
            LayoutElement element = obj.AddComponent<LayoutElement>();
            element.preferredHeight = 144f;
            element.minHeight = 144f;

            TMP_Text label = CreateText(
                "Label", obj.transform, text, 32f,
                FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            Stretch(label.rectTransform);
            label.margin = new Vector4(24f, 8f, 24f, 8f);
            return button;
        }

        private Button CreateButton(
            string name,
            Transform parent,
            string text,
            Vector2 position,
            Vector2 size)
        {
            GameObject obj = CreateUiObject(name, parent);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = obj.AddComponent<Image>();
            image.color = Yellow;
            Button button = obj.AddComponent<Button>();
            button.targetGraphic = image;
            ExperimentUiTheme.StyleButton(button, true);
            TMP_Text label = CreateText(
                "Label", obj.transform, text, 23f,
                FontStyles.Normal, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            return button;
        }

        private TMP_Text CreateText(
            string name,
            Transform parent,
            string value,
            float size,
            FontStyles style,
            TextAlignmentOptions alignment)
        {
            GameObject obj = CreateUiObject(name, parent);
            TMP_Text text = obj.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = ExperimentUiTheme.TextColor;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            if (_font != null)
                text.font = _font;
            return text;
        }

        private void SetStatus(string value, Color color)
        {
            if (_statusText == null)
                return;
            _statusText.text = value ?? string.Empty;
            _statusText.color = color;
            _statusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(value));
        }

        private void ClearRows()
        {
            if (_content == null)
                return;
            for (int index = _content.childCount - 1; index >= 0; index--)
                Destroy(_content.GetChild(index).gameObject);
        }

        private static GameObject CreateUiObject(
            string name,
            Transform parent)
        {
            GameObject obj = new GameObject(
                name,
                typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static void Stretch(RectTransform rect)
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
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void SetBottomStretch(
            RectTransform rect,
            float bottom,
            float left,
            float right,
            float height)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, bottom + height);
        }
    }
}
