using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Vtp.VR.LocalAlgorithmExe;

namespace Vtp.VR.Integration
{
    [Serializable]
    public sealed class AlgorithmReasonDefinition
    {
        public string id;
        public string text;
        public bool correctReference;
    }

    public sealed class AlgorithmAssessmentSelection
    {
        public string AlgorithmKey;
        public string ExeAlgorithmKey;
        public string AlgorithmName;
        public string ReasonId;
        public string ReasonText;
        public bool IsReferenceAlgorithm;
        public bool IsReferenceReason;
        public JObject Parameters;
    }

    /// <summary>
    /// S14算法与原因选择、S15算法参数填写的三页式运行时界面。
    /// 界面只负责收集和校验答案，正式正确答案仍应由服务器判定。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class AlgorithmAssessmentController : MonoBehaviour
    {
        private const string PreferredFontResourcePath =
            "Fonts/AlibabaPuHuiTi-3-55-Regular SDF";
        private const string PreferredFontAssetName =
            "AlibabaPuHuiTi-3-55-Regular SDF";

        private static TMP_FontAsset _preferredFontAsset;
        private static bool _preferredFontLookupAttempted;

        private enum Page
        {
            Introduction,
            Reason,
            Parameters
        }

        private sealed class ReasonOption
        {
            public readonly string Id;
            public readonly string Text;
            public readonly bool IsCorrectReference;

            public ReasonOption(
                string id,
                string text,
                bool isCorrectReference)
            {
                Id = id;
                Text = text;
                IsCorrectReference = isCorrectReference;
            }
        }

        private sealed class AlgorithmOption
        {
            public readonly string Key;
            public readonly string ExeKey;
            public readonly string Name;
            public readonly string Introduction;
            public ReasonOption[] Reasons;

            public AlgorithmOption(
                string key,
                string exeKey,
                string name,
                string introduction,
                ReasonOption[] reasons)
            {
                Key = key;
                ExeKey = exeKey;
                Name = name;
                Introduction = introduction;
                Reasons = reasons;
            }
        }

        private static readonly Color32 Yellow =
            ExperimentUiTheme.Primary;
        private static readonly Color32 SelectedYellow =
            ExperimentUiTheme.Selected;
        private static readonly Color32 CardGray =
            ExperimentUiTheme.Panel;
        private static readonly Color32 RowGray =
            ExperimentUiTheme.PanelRaised;
        private static readonly Color32 InputGray =
            ExperimentUiTheme.PanelInput;
        private static readonly Color32 ErrorRed =
            ExperimentUiTheme.Error;

        private readonly Dictionary<string, Image> _algorithmImages =
            new Dictionary<string, Image>(
                StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Image> _reasonImages =
            new Dictionary<string, Image>(
                StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TMP_InputField> _parameterInputs =
            new Dictionary<string, TMP_InputField>(
                StringComparer.OrdinalIgnoreCase);

        private AlgorithmOption[] _algorithms;
        private CanvasGroup _canvasGroup;
        private RectTransform _introductionPage;
        private RectTransform _reasonPage;
        private RectTransform _parameterPage;
        private RectTransform _reasonContent;
        private RectTransform _parameterContent;
        private TMP_Text _headerText;
        private TMP_Text _headerStateText;
        private TMP_Text _footerHintText;
        private TMP_Text _selectionSummaryText;
        private TMP_Text _reasonTitleText;
        private TMP_Text _parameterTitleText;
        private TMP_Text _errorText;
        private Button _primaryButton;
        private TMP_Text _primaryButtonText;
        private Button _secondaryButton;
        private TMP_Text _secondaryButtonText;
        private Button _closeButton;

        private string _selectedAlgorithmKey = string.Empty;
        private string _selectedReasonId = string.Empty;
        private string _referenceAlgorithmKey = string.Empty;
        private Page _page;
        private Action<AlgorithmAssessmentSelection> _onConfirmed;
        private Action _onClosed;
        private bool _initialized;
        private bool _parameterFirstFlow;
        private bool _parametersPrepared;
        private string _parameterEditingKey;
        private RectTransform _parameterPresetTabs;
        private readonly Dictionary<string, JObject> _parameterConfigurations = new Dictionary<string, JObject>();

        public bool ParametersPrepared => _parametersPrepared;

        /// <summary>参数页仅编辑配置，不代表选择了本次要执行的算法。</summary>
        public void OpenParameterPreparation()
        {
            if (!_initialized) Initialize(null, null);
            _parameterFirstFlow = true;
            if (_parameterConfigurations.Count == 0)
                foreach (var option in _algorithms)
                    _parameterConfigurations[option.ExeKey] = RmgExeParameterFactory.CreateDefaults(option.ExeKey);
            gameObject.SetActive(true);
            _canvasGroup.alpha = 1; _canvasGroup.interactable = _canvasGroup.blocksRaycasts = true;
            ShowParameterPreset(_parameterEditingKey ?? _algorithms[0].Key, false);
        }

        private void ShowParameterPreset(string key, bool saveCurrent = true)
        {
            if (saveCurrent && !SaveParameterPreset()) return;
            var option = FindAlgorithm(key);
            if (option == null) return;
            _parameterEditingKey = option.Key;
            BuildParameterRows(option);
            _parameterTitleText.text = "设置算法参数（当前编辑：" + option.Name + "）";
            SetError(string.Empty);
            SetPage(Page.Parameters);
        }

        private bool SaveParameterPreset()
        {
            var option = FindAlgorithm(_parameterEditingKey);
            if (option == null) return true;
            var supplied = new JObject();
            foreach (var pair in _parameterInputs) supplied[pair.Key] = pair.Value.text.Trim();
            JObject normalized; string error;
            if (!RmgExeParameterFactory.TryNormalizeAndValidate(option.ExeKey, supplied, out normalized, out error))
            { SetError(error); return false; }
            _parameterConfigurations[option.ExeKey] = normalized;
            return true;
        }

        private void CompleteParameterPreparation()
        {
            if (!SaveParameterPreset()) return;
            _parametersPrepared = true;
            StudentAssessmentRuntime.MarkAlgorithmIntroductionViewed();
            ShowIntroductionPage();
        }

        private void ConfirmPreparedAlgorithm()
        {
            var option = FindAlgorithm(_selectedAlgorithmKey);
            if (!_parametersPrepared || option == null || !_parameterConfigurations.ContainsKey(option.ExeKey))
            { SetError("请先完成参数设置，再选择本次算法。"); return; }
            ConfirmSelection(option, (JObject)_parameterConfigurations[option.ExeKey].DeepClone());
        }

        public CanvasGroup PanelGroup
        {
            get { return _canvasGroup; }
        }

        public void Initialize(
            Action<AlgorithmAssessmentSelection> onConfirmed,
            Action onClosed)
        {
            _onConfirmed = onConfirmed;
            _onClosed = onClosed;
            if (_initialized)
                return;

            _algorithms = CreateAlgorithmOptions();
            _canvasGroup = GetComponent<CanvasGroup>();
            BuildInterface();
            _initialized = true;
            Hide();
        }

        public void Open()
        {
            if (!_initialized)
                Initialize(null, null);

            gameObject.SetActive(true);
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
            StudentAssessmentRuntime.MarkAlgorithmIntroductionViewed();
            ShowIntroductionPage();
        }

        /// <summary>
        /// 设置固定参数、固定随机种子预运行三种算法后得到的标准算法。
        /// </summary>
        public void SetReferenceAlgorithm(string algorithmKey)
        {
            _referenceAlgorithmKey =
                (algorithmKey ?? string.Empty).Trim();
            UpdateSelectionSummary();
        }

        public void OpenWithError(string message)
        {
            Open();
            SetError(message);
        }

        public void Hide()
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();
            gameObject.SetActive(true);
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        public void ResetForNewAttempt()
        {
            _parameterFirstFlow = false;
            _parametersPrepared = false;
            _parameterEditingKey = null;
            _parameterConfigurations.Clear();
            _selectedAlgorithmKey = string.Empty;
            _selectedReasonId = string.Empty;
            ClearParameterRows();
            RefreshAlgorithmCards();
            SetError(string.Empty);
        }

        /// <summary>
        /// 允许以后用服务器配置动态替换某个算法的原因列表。
        /// 本地参考配置要求恰好一个正确原因；正式得分仍以服务器为准。
        /// </summary>
        public bool TryReplaceReasons(
            string algorithmKey,
            AlgorithmReasonDefinition[] definitions,
            out string error)
        {
            error = null;
            AlgorithmOption algorithm = FindAlgorithm(algorithmKey);
            if (algorithm == null)
            {
                error = "找不到要配置原因的算法。";
                return false;
            }
            if (definitions == null || definitions.Length == 0)
            {
                error = "原因选项不能为空。";
                return false;
            }

            HashSet<string> ids = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            int correctCount = 0;
            ReasonOption[] reasons =
                new ReasonOption[definitions.Length];
            for (int index = 0; index < definitions.Length; index++)
            {
                AlgorithmReasonDefinition definition =
                    definitions[index];
                if (definition == null ||
                    string.IsNullOrWhiteSpace(definition.id) ||
                    string.IsNullOrWhiteSpace(definition.text) ||
                    !ids.Add(definition.id.Trim()))
                {
                    error = "原因选项存在空值或重复ID。";
                    return false;
                }
                if (definition.correctReference)
                    correctCount++;
                reasons[index] = new ReasonOption(
                    definition.id.Trim(),
                    definition.text.Trim(),
                    definition.correctReference);
            }

            if (correctCount != 1)
            {
                error = "每个算法的原因列表必须恰好包含一个参考正确答案。";
                return false;
            }

            algorithm.Reasons = reasons;
            if (string.Equals(
                    algorithm.Key,
                    _selectedAlgorithmKey,
                    StringComparison.OrdinalIgnoreCase) &&
                FindReason(algorithm, _selectedReasonId) == null)
            {
                _selectedReasonId = string.Empty;
            }
            return true;
        }

        private void BuildInterface()
        {
            RectTransform root = GetComponent<RectTransform>();
            Stretch(root);

            Image blocker = GetComponent<Image>();
            if (blocker == null)
                blocker = gameObject.AddComponent<Image>();
            blocker.color = ExperimentUiTheme.Page;
            blocker.raycastTarget = true;

            RectTransform card = ExperimentUiTheme.DesignSurface(transform, "AlgorithmAssessmentCard");
            RectTransform header = ExperimentUiTheme.HeaderBar(card, "求解算法选择");
            _headerText = header.Find("Heading").GetComponent<TMP_Text>();
            _headerStateText = CreateText("StateText", header, "算法介绍", 26f,
                FontStyles.Normal, Color.white, TextAlignmentOptions.MidlineRight);
            SetTopStretch(_headerStateText.rectTransform, 18f, 1320f, 72f, 64f);

            _selectionSummaryText = CreateText(
                "SelectionSummaryText",
                card,
                "尚未选择算法和原因",
                25f,
                FontStyles.Normal,
                Color.black,
                TextAlignmentOptions.MidlineLeft);
            SetBottomStretch(_selectionSummaryText.rectTransform, 152f, 72f, 72f, 64f);

            _introductionPage = CreatePage(
                "AlgorithmIntroductionPage",
                card);
            _reasonPage = CreatePage("AlgorithmReasonPage", card);
            _parameterPage = CreatePage("AlgorithmParameterPage", card);

            BuildIntroductionPage();
            BuildReasonPageShell();
            BuildParameterPageShell();

            _errorText = CreateText(
                "ErrorText",
                card,
                string.Empty,
                21f,
                FontStyles.Normal,
                ErrorRed,
                TextAlignmentOptions.Center);
            SetBottomStretch(_errorText.rectTransform, 128f, 72f, 72f, 32f);
            RectTransform footer = ExperimentUiTheme.FooterBar(card);
            _footerHintText = CreateText("FooterHint", footer, "请先完成算法与原因选择。", 22f,
                FontStyles.Normal, ExperimentUiTheme.Muted, TextAlignmentOptions.MidlineLeft);
            SetTopStretch(_footerHintText.rectTransform, 34f, 72f, 1040f, 64f);

            _secondaryButton = CreateButton(
                "SecondaryButton",
                footer,
                "返回",
                new Vector2(-300f, 22f),
                new Vector2(230f, 60f),
                Color.white,
                out _secondaryButtonText);
            ExperimentUiTheme.FooterButton(_secondaryButton.GetComponent<RectTransform>(), 576f, 250f);

            _primaryButton = CreateButton(
                "PrimaryButton",
                footer,
                "确定，填写参数",
                new Vector2(0f, 22f),
                new Vector2(300f, 60f),
                Yellow,
                out _primaryButtonText);
            ExperimentUiTheme.FooterButton(_primaryButton.GetComponent<RectTransform>(), 192f, 350f);

            _closeButton = CreateButton(
                "CloseButton",
                footer,
                "关闭",
                new Vector2(300f, 22f),
                new Vector2(230f, 60f),
                Color.white,
                out _);
            ExperimentUiTheme.FooterButton(_closeButton.GetComponent<RectTransform>(), 64f, 96f);
            _closeButton.onClick.AddListener(CloseByUser);
        }

        private void BuildIntroductionPage()
        {
            TMP_Text title = CreateText("IntroductionTitle", _introductionPage,
                "选择本次实验的求解算法", 36f, FontStyles.Bold,
                Color.black, TextAlignmentOptions.MidlineLeft);
            SetTopStretch(title.rectTransform, 0f, 0f, 0f, 64f);
            TMP_Text hint = CreateText(
                "InstructionText",
                _introductionPage,
                "点击一种算法查看原因选项。选择原因后会自动返回本页，仍可重新修改。",
                25f,
                FontStyles.Normal,
                ExperimentUiTheme.Muted,
                TextAlignmentOptions.MidlineLeft);
            SetTopStretch(hint.rectTransform, 72f, 0f, 0f, 64f);
            RectTransform content = CreateUiObject("AlgorithmChoices", _introductionPage)
                .GetComponent<RectTransform>();
            SetTopStretch(content, 150f, 0f, 0f, 520f);
            HorizontalLayoutGroup group = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 32f;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = group.childForceExpandHeight = true;

            for (int index = 0; index < _algorithms.Length; index++)
            {
                AlgorithmOption option = _algorithms[index];
                GameObject obj = CreateUiObject(option.Key + "Button", content);
                Image image = obj.AddComponent<Image>();
                Button button = obj.AddComponent<Button>();
                button.targetGraphic = image;
                ExperimentUiTheme.StyleChoice(button);
                obj.AddComponent<LayoutElement>().flexibleWidth = 1f;
                TMP_Text acronym = CreateText("Acronym", obj.transform, option.ExeKey.ToUpperInvariant(),
                    88f, FontStyles.Bold, ExperimentUiTheme.Muted, TextAlignmentOptions.MidlineLeft);
                SetTopStretch(acronym.rectTransform, 32f, 36f, 36f, 100f);
                TMP_Text name = CreateText("AlgorithmName", obj.transform,
                    option.Name.Split('（')[0], 32f, FontStyles.Bold, Color.black, TextAlignmentOptions.MidlineLeft);
                SetTopStretch(name.rectTransform, 148f, 36f, 28f, 78f);
                TMP_Text introduction = CreateText("Introduction", obj.transform, option.Introduction,
                    26f, FontStyles.Normal, ExperimentUiTheme.Muted, TextAlignmentOptions.TopLeft);
                SetTopStretch(introduction.rectTransform, 246f, 36f, 36f, 150f);
                TMP_Text action = CreateText("Action", obj.transform, "点击选择并查看原因",
                    24f, FontStyles.Normal, ExperimentUiTheme.Muted, TextAlignmentOptions.MidlineLeft);
                SetBottomStretch(action.rectTransform, 24f, 36f, 36f, 64f);
                Image actionLine = CreateUiObject("ActionDivider", obj.transform).AddComponent<Image>();
                actionLine.color = ExperimentUiTheme.Border;
                actionLine.raycastTarget = false;
                SetBottomStretch(actionLine.rectTransform, 98f, 36f, 36f, 1f);
                Image arrow = CreateUiObject("ActionArrow", obj.transform).AddComponent<Image>();
                arrow.sprite = ExperimentUiTheme.BuiltinSprite("DropdownArrow");
                arrow.color = ExperimentUiTheme.Muted;
                arrow.raycastTarget = false;
                arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(1, 0);
                arrow.rectTransform.anchoredPosition = new Vector2(-46, 56);
                arrow.rectTransform.sizeDelta = new Vector2(36, 24);
                arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
                string capturedKey = option.Key;
                button.onClick.AddListener(
                    () => SelectAlgorithmAndOpenReasons(capturedKey));
                _algorithmImages.Add(
                    option.Key,
                    button.GetComponent<Image>());
            }
        }

        private void BuildReasonPageShell()
        {
            _reasonTitleText = CreateText(
                "ReasonTitleText",
                _reasonPage,
                "选择算法的原因",
                36f,
                FontStyles.Bold,
                Color.black,
                TextAlignmentOptions.MidlineLeft);
            SetTopStretch(
                _reasonTitleText.rectTransform,
                0f,
                0f,
                0f,
                64f);

            _reasonContent = CreateScrollArea(
                "ReasonScrollView",
                _reasonPage,
                94f,
                4f);
        }

        private void BuildParameterPageShell()
        {
            _parameterTitleText = CreateText(
                "ParameterTitleText",
                _parameterPage,
                "填写算法参数",
                36f,
                FontStyles.Bold,
                Color.black,
                TextAlignmentOptions.MidlineLeft);
            SetTopStretch(
                _parameterTitleText.rectTransform,
                0f,
                0f,
                0f,
                64f);

            TMP_Text runHint = CreateText("RunHint", _parameterPage,
                "确认后仅执行所选算法，生成本次调度序列。", 22f, FontStyles.Normal,
                ExperimentUiTheme.Muted, TextAlignmentOptions.MidlineLeft);
            SetTopStretch(runHint.rectTransform, 134f, 0f, 0f, 34f);
            _parameterPresetTabs = CreateUiObject("ParameterPresetTabs", _parameterPage).GetComponent<RectTransform>();
            SetTopStretch(_parameterPresetTabs, 70f, 0f, 0f, 54f);
            for (int index = 0; index < _algorithms.Length; index++)
            {
                var option = _algorithms[index];
                TMP_Text label;
                var button = CreateButton("Preset_" + option.ExeKey, _parameterPresetTabs, option.Name + "参数",
                    Vector2.zero, new Vector2(510, 50), CardGray, out label);
                var rect = button.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(index / 3f, 0); rect.anchorMax = new Vector2((index + 1) / 3f, 1);
                rect.offsetMin = new Vector2(0, 0); rect.offsetMax = new Vector2(-16, 0);
                string key = option.Key;
                button.onClick.AddListener(() => ShowParameterPreset(key));
            }
            _parameterPresetTabs.gameObject.SetActive(false);
            _parameterContent = CreateScrollArea(
                "ParameterScrollView",
                _parameterPage,
                184f,
                4f,
                true);
        }

        private void SelectAlgorithmAndOpenReasons(string algorithmKey)
        {
            AlgorithmOption option = FindAlgorithm(algorithmKey);
            if (option == null)
                return;

            if (!string.Equals(
                    _selectedAlgorithmKey,
                    option.Key,
                    StringComparison.OrdinalIgnoreCase))
            {
                _selectedAlgorithmKey = option.Key;
                _selectedReasonId = string.Empty;
                ClearParameterRows();
            }

            BuildReasonRows(option);
            _reasonTitleText.text =
                "为什么选择“" + option.Name + "”？";
            SetPage(Page.Reason);
        }

        private void BuildReasonRows(AlgorithmOption option)
        {
            ClearChildren(_reasonContent);
            _reasonImages.Clear();

            ReasonOption[] reasons = option.Reasons ??
                                     new ReasonOption[0];
            for (int index = 0; index < reasons.Length; index++)
            {
                ReasonOption reason = reasons[index];
                Button button = CreateLayoutButton(
                    "Reason_" + reason.Id,
                    _reasonContent,
                    reason.Text,
                    124f,
                    Color.white,
                    23f);
                ExperimentUiTheme.ChoiceIndicator(button.transform, false);
                TMP_Text reasonLabel = button.GetComponentInChildren<TMP_Text>();
                reasonLabel.alignment = TextAlignmentOptions.MidlineLeft;
                reasonLabel.rectTransform.offsetMin = new Vector2(66f, 12f);
                string capturedId = reason.Id;
                button.onClick.AddListener(
                    () => SelectReasonAndReturn(capturedId));
                _reasonImages.Add(
                    reason.Id,
                    button.GetComponent<Image>());
            }
            RefreshReasonCards();
        }

        private void SelectReasonAndReturn(string reasonId)
        {
            _selectedReasonId = reasonId ?? string.Empty;
            RefreshReasonCards();
            RefreshAlgorithmCards();
            UpdateSelectionSummary();
            ShowIntroductionPage();
        }

        private void ShowIntroductionPage()
        {
            RefreshAlgorithmCards();
            UpdateSelectionSummary();
            SetError(string.Empty);
            SetPage(Page.Introduction);
        }

        private void ShowParameterPage()
        {
            AlgorithmOption option =
                FindAlgorithm(_selectedAlgorithmKey);
            if (option == null ||
                string.IsNullOrWhiteSpace(_selectedReasonId))
            {
                SetError("请先选择算法并完成原因选择。");
                return;
            }

            BuildParameterRows(option);
            _parameterTitleText.text =
                "填写本次运行参数";
            SetError(string.Empty);
            SetPage(Page.Parameters);
        }

        private void BuildParameterRows(AlgorithmOption option)
        {
            ClearParameterRows();

            RmgExeParameterDefinition[] definitions;
            try
            {
                definitions =
                    RmgExeParameterFactory.GetDefinitions(
                        option.ExeKey);
            }
            catch (Exception exception)
            {
                SetError(exception.Message);
                return;
            }

            RectTransform left = CreateParameterColumn("SearchSettings", "搜索设置");
            RectTransform right = CreateParameterColumn("ProbabilitySettings", option.ExeKey == "ga"
                ? "概率与复现" : option.ExeKey == "pso" ? "边界与复现" : "算子与复现");
            for (int index = 0; index < definitions.Length; index++)
            {
                RmgExeParameterDefinition definition =
                    definitions[index];
                bool rightColumn = option.ExeKey == "ga"
                    ? definition.Key.Contains("RATE") || definition.Key.Contains("SEED")
                    : index >= (definitions.Length + 1) / 2;
                GameObject rowObject = CreateUiObject(
                    "Parameter_" + definition.Key,
                    rightColumn ? right : left);
                ExperimentUiTheme.Divider(rowObject.transform);
                LayoutElement layout =
                    rowObject.AddComponent<LayoutElement>();
                layout.preferredHeight = 84f;
                layout.minHeight = 84f;

                TMP_Text label = CreateText(
                    "Label",
                    rowObject.transform,
                    definition.DisplayName + "\n<size=20><color=#8FA6B8>范围：" +
                    definition.GetRangeText() + "</color></size>",
                    24f,
                    FontStyles.Normal,
                    Color.black,
                    TextAlignmentOptions.MidlineLeft);
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(0.61f, 1f);
                labelRect.offsetMin = new Vector2(0f, 5f);
                labelRect.offsetMax = new Vector2(-8f, -5f);

                TMP_InputField input = CreateInputField(
                    "Input",
                    rowObject.transform,
                    _parameterFirstFlow && _parameterConfigurations.ContainsKey(option.ExeKey)
                        ? _parameterConfigurations[option.ExeKey][definition.Key]?.ToString() ?? definition.GetDefaultText()
                        : definition.GetDefaultText(),
                    definition.ValueType ==
                        RmgExeParameterValueType.Integer);
                RectTransform inputRect =
                    input.GetComponent<RectTransform>();
                inputRect.anchorMin = new Vector2(0.64f, 0.5f);
                inputRect.anchorMax = new Vector2(1f, 0.5f);
                inputRect.pivot = new Vector2(0.5f, 0.5f);
                inputRect.offsetMin = new Vector2(0f, -31f);
                inputRect.offsetMax = new Vector2(-2f, 31f);

                _parameterInputs.Add(definition.Key, input);
            }
        }

        private RectTransform CreateParameterColumn(string name, string title)
        {
            RectTransform column = CreateUiObject(name, _parameterContent).GetComponent<RectTransform>();
            column.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            VerticalLayoutGroup layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            TMP_Text heading = CreateText("SectionTitle", column, title, 28f,
                FontStyles.Bold, Color.black, TextAlignmentOptions.MidlineLeft);
            heading.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;
            return column;
        }

        private void ConfirmParametersAndRun()
        {
            AlgorithmOption option =
                FindAlgorithm(_selectedAlgorithmKey);
            if (option == null)
            {
                SetError("没有可执行的算法选择。");
                return;
            }

            JObject supplied = new JObject();
            foreach (
                KeyValuePair<string, TMP_InputField> pair
                in _parameterInputs)
            {
                supplied[pair.Key] =
                    pair.Value == null
                        ? string.Empty
                        : pair.Value.text.Trim();
            }

            JObject normalized;
            string validationError;
            if (!RmgExeParameterFactory.TryNormalizeAndValidate(
                    option.ExeKey,
                    supplied,
                    out normalized,
                    out validationError))
            {
                SetError(validationError);
                return;
            }

            ConfirmSelection(option, normalized);
        }

        private void ConfirmSelection(AlgorithmOption option, JObject normalized)
        {
            ReasonOption reason = FindReason(
                option,
                _selectedReasonId);
            if (reason == null)
            {
                SetError("选择原因已经失效，请重新选择。 ");
                return;
            }

            AlgorithmAssessmentSelection selection =
                new AlgorithmAssessmentSelection
                {
                    AlgorithmKey = option.Key,
                    ExeAlgorithmKey = option.ExeKey,
                    AlgorithmName = option.Name,
                    ReasonId = reason.Id,
                    ReasonText = reason.Text,
                    IsReferenceAlgorithm = string.Equals(
                        option.Key,
                        _referenceAlgorithmKey,
                        StringComparison.OrdinalIgnoreCase),
                    IsReferenceReason = reason.IsCorrectReference,
                    Parameters = normalized
                };

            Hide();
            if (_onConfirmed != null)
                _onConfirmed(selection);
        }

        private void SetPage(Page page)
        {
            _page = page;
            _selectionSummaryText.gameObject.SetActive(!(_parameterFirstFlow && page == Page.Parameters));
            if (_parameterPresetTabs != null) _parameterPresetTabs.gameObject.SetActive(_parameterFirstFlow && page == Page.Parameters);
            _footerHintText.text = page == Page.Parameters
                ? "请核对全部参数后确认。" : "请先完成算法与原因选择。";
            _headerStateText.text = page == Page.Introduction ? "算法介绍" :
                page == Page.Reason ? "原因选择" : FindAlgorithm(_selectedAlgorithmKey)?.Name ?? "参数设置";
            if (page == Page.Parameters)
            {
                _parameterPage.offsetMin = new Vector2(72f, 168f);
                SetTopStretch(_selectionSummaryText.rectTransform, 204f, 72f, 72f, 64f);
            }
            else
                SetBottomStretch(_selectionSummaryText.rectTransform, 152f, 72f, 72f, 64f);
            _introductionPage.gameObject.SetActive(
                page == Page.Introduction);
            _reasonPage.gameObject.SetActive(page == Page.Reason);
            _parameterPage.gameObject.SetActive(page == Page.Parameters);

            _primaryButton.onClick.RemoveAllListeners();
            _secondaryButton.onClick.RemoveAllListeners();

            if (page == Page.Introduction)
            {
                _headerText.text = "求解算法选择";
                _primaryButton.gameObject.SetActive(true);
                _primaryButtonText.text = "确定，填写参数";
                _primaryButton.interactable =
                    FindAlgorithm(_selectedAlgorithmKey) != null &&
                    !string.IsNullOrWhiteSpace(_selectedReasonId);
                _primaryButton.onClick.AddListener(ShowParameterPage);
                _secondaryButton.gameObject.SetActive(false);
            }
            else if (page == Page.Reason)
            {
                _headerText.text = "选择算法原因";
                _primaryButton.gameObject.SetActive(false);
                _secondaryButton.gameObject.SetActive(true);
                _secondaryButtonText.text = "返回算法介绍";
                _secondaryButton.onClick.AddListener(ShowIntroductionPage);
            }
            else
            {
                _headerText.text = "填写算法参数";
                _primaryButton.gameObject.SetActive(true);
                _primaryButtonText.text = "确认参数并开始实验";
                _primaryButton.interactable = true;
                _primaryButton.onClick.AddListener(
                    ConfirmParametersAndRun);
                _secondaryButton.gameObject.SetActive(true);
                _secondaryButtonText.text = "返回算法介绍";
                _secondaryButton.onClick.AddListener(
                    ShowIntroductionPage);
            }
            if (_parameterFirstFlow)
            {
                var runHint = _parameterPage.Find("RunHint")?.GetComponent<TMP_Text>();
                if (runHint != null) runHint.text = "分别设置各算法参数，保存后再选择本次实际执行的算法。";
                if (page == Page.Parameters)
                {
                    _headerText.text = "实验参数设置";
                    _headerStateText.text = "先设置参数，再选择算法";
                    _footerHintText.text = "切换上方标签仅编辑参数，不代表选择执行算法；未修改项保留默认值。";
                    _primaryButtonText.text = "保存参数，选择算法";
                    _primaryButton.onClick.RemoveAllListeners();
                    _primaryButton.onClick.AddListener(CompleteParameterPreparation);
                    _secondaryButton.gameObject.SetActive(_parametersPrepared);
                }
                else if (page == Page.Introduction)
                {
                    _footerHintText.text = "参数已保存，请选择本次执行的算法及原因。";
                    _primaryButtonText.text = "确认算法并进入实验";
                    _primaryButton.onClick.RemoveAllListeners();
                    _primaryButton.onClick.AddListener(ConfirmPreparedAlgorithm);
                    _secondaryButton.gameObject.SetActive(true);
                    _secondaryButtonText.text = "返回参数设置";
                    _secondaryButton.onClick.RemoveAllListeners();
                    _secondaryButton.onClick.AddListener(() => OpenParameterPreparation());
                }
            }
            UpdateSelectionSummary();
        }

        private void UpdateSelectionSummary()
        {
            if (_parameterFirstFlow && _page == Page.Parameters)
            {
                _selectionSummaryText.text = "当前仅编辑参数配置，下一步再选择运行算法；未修改项保留默认值。";
                return;
            }
            AlgorithmOption algorithm =
                FindAlgorithm(_selectedAlgorithmKey);
            if (algorithm == null)
            {
                _selectionSummaryText.text = "尚未选择算法和原因";
                return;
            }

            ReasonOption reason =
                FindReason(algorithm, _selectedReasonId);
            _selectionSummaryText.text =
                _page == Page.Parameters && reason != null
                    ? "已选择：" + algorithm.Name + " · 原因已选择"
                    : reason == null
                    ? "已选择：" + algorithm.Name +
                      "；请继续选择原因"
                    : "已选择：" + algorithm.Name +
                      "；原因：" + reason.Text;
        }

        private void RefreshAlgorithmCards()
        {
            foreach (
                KeyValuePair<string, Image> pair
                in _algorithmImages)
            {
                if (pair.Value == null)
                    continue;
                ExperimentUiTheme.SelectedState(pair.Value,
                    string.Equals(
                        pair.Key,
                        _selectedAlgorithmKey,
                        StringComparison.OrdinalIgnoreCase));
            }
        }

        private void RefreshReasonCards()
        {
            foreach (
                KeyValuePair<string, Image> pair
                in _reasonImages)
            {
                if (pair.Value == null)
                    continue;
                ExperimentUiTheme.SelectedState(pair.Value,
                    string.Equals(
                        pair.Key,
                        _selectedReasonId,
                        StringComparison.OrdinalIgnoreCase));
            }
        }

        private void CloseByUser()
        {
            Hide();
            if (_onClosed != null)
                _onClosed();
        }

        private AlgorithmOption FindAlgorithm(string key)
        {
            if (_algorithms == null)
                return null;
            for (int index = 0; index < _algorithms.Length; index++)
            {
                if (string.Equals(
                        _algorithms[index].Key,
                        key,
                        StringComparison.OrdinalIgnoreCase))
                    return _algorithms[index];
            }
            return null;
        }

        private static ReasonOption FindReason(
            AlgorithmOption algorithm,
            string reasonId)
        {
            if (algorithm == null || algorithm.Reasons == null)
                return null;
            for (int index = 0; index < algorithm.Reasons.Length; index++)
            {
                if (string.Equals(
                        algorithm.Reasons[index].Id,
                        reasonId,
                        StringComparison.OrdinalIgnoreCase))
                    return algorithm.Reasons[index];
            }
            return null;
        }

        private void ClearParameterRows()
        {
            _parameterInputs.Clear();
            if (_parameterContent != null)
                ClearChildren(_parameterContent);
        }

        private void SetError(string message)
        {
            if (_errorText != null)
                _errorText.text = message ?? string.Empty;
        }

        private static AlgorithmOption[] CreateAlgorithmOptions()
        {
            return new[]
            {
                new AlgorithmOption(
                    "genetic-algorithm-v2",
                    "ga",
                    "遗传算法（GA）",
                    "以种群表示多组调度方案，通过选择、交叉和变异持续改进，适合离散组合调度和多设备分配。",
                    new[]
                    {
                        new ReasonOption("GA_COMBINATORIAL_SEARCH", "任务顺序和设备分配属于离散组合问题，交叉与变异可以在保持种群多样性的同时扩大搜索范围。", true),
                        new ReasonOption("GA_NO_ENCODING", "遗传算法不需要对任务顺序和设备分配进行编码。", false),
                        new ReasonOption("GA_ALWAYS_EXACT", "遗传算法每次运行都能严格证明当前方案是全局最优解。", false),
                        new ReasonOption("GA_ONLY_CONTINUOUS", "遗传算法只适用于可求导的连续变量问题。", false)
                    }),
                new AlgorithmOption(
                    "particle-swarm",
                    "pso",
                    "粒子群优化算法（PSO）",
                    "由多个粒子并行搜索，通过个体最优和群体最优信息协同更新，可较快探索大规模解空间。",
                    new[]
                    {
                        new ReasonOption("PSO_COLLABORATIVE_SEARCH", "粒子同时利用自身历史最优和群体最优信息，能够在较少控制参数下协同搜索候选调度方案。", true),
                        new ReasonOption("PSO_SINGLE_SOLUTION", "粒子群在整个运行过程中只维护一个候选方案。", false),
                        new ReasonOption("PSO_NO_RANDOMNESS", "粒子群算法完全没有随机性，因此不需要随机种子。", false),
                        new ReasonOption("PSO_GUARANTEED_EXACT", "粒子群在任意参数下都能保证一次得到精确全局最优解。", false)
                    }),
                new AlgorithmOption(
                    "alns",
                    "alns",
                    "自适应大邻域搜索算法（ALNS）",
                    "通过破坏与修复当前方案构造大范围邻域，并根据算子表现动态调整权重，适合复杂约束调度。",
                    new[]
                    {
                        new ReasonOption("ALNS_DESTROY_REPAIR", "破坏与修复算子能重构调度方案，自适应权重有助于在复杂约束下跳出局部最优。", true),
                        new ReasonOption("ALNS_NO_OPERATORS", "ALNS不需要任何破坏算子或修复算子。", false),
                        new ReasonOption("ALNS_FIXED_NEIGHBORHOOD", "ALNS只能在一个固定且很小的邻域中搜索。", false),
                        new ReasonOption("ALNS_NO_ACCEPTANCE", "ALNS只能接受更优解，不能配置任何接受准则。", false)
                    })
            };
        }

        private RectTransform CreatePage(
            string objectName,
            RectTransform card)
        {
            GameObject pageObject = CreateUiObject(objectName, card);
            RectTransform page =
                pageObject.GetComponent<RectTransform>();
            page.anchorMin = new Vector2(0f, 0f);
            page.anchorMax = new Vector2(1f, 1f);
            page.offsetMin = new Vector2(72f, 236f);
            page.offsetMax = new Vector2(-72f, -138f);
            return page;
        }

        private static RectTransform CreateScrollArea(
            string objectName,
            Transform parent,
            float top,
            float bottom,
            bool columns = false)
        {
            GameObject scrollObject =
                CreateUiObject(objectName, parent);
            RectTransform scrollRectTransform =
                scrollObject.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin =
                new Vector2(6f, bottom);
            scrollRectTransform.offsetMax =
                new Vector2(-6f, -top);
            ExperimentUiTheme.SciFiSurface(
                scrollObject.AddComponent<Image>(),
                ExperimentUiTheme.Page);

            ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 32f;

            GameObject viewportObject =
                CreateUiObject("Viewport", scrollObject.transform);
            RectTransform viewport =
                viewportObject.GetComponent<RectTransform>();
            Stretch(viewport);
            viewportObject.AddComponent<Image>().color =
                new Color(1f, 1f, 1f, 0.01f);
            viewportObject.AddComponent<RectMask2D>();

            GameObject contentObject =
                CreateUiObject("Content", viewportObject.transform);
            RectTransform content =
                contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            HorizontalOrVerticalLayoutGroup layout = columns
                ? (HorizontalOrVerticalLayoutGroup)contentObject.AddComponent<HorizontalLayoutGroup>()
                : contentObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(1, 1, 1, 1);
            layout.spacing = columns ? 64f : 16f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter =
                contentObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit =
                ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            ExperimentUiTheme.Scrollbar(scroll);
            return content;
        }

        private static Button CreateLayoutButton(
            string objectName,
            Transform parent,
            string text,
            float height,
            Color color,
            float fontSize)
        {
            GameObject buttonObject =
                CreateUiObject(objectName, parent);
            Image image = buttonObject.AddComponent<Image>();
            image.color = color;
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            ExperimentUiTheme.StyleChoice(button);
            LayoutElement layout =
                buttonObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.minHeight = height;

            TMP_Text label = CreateText(
                "Text",
                buttonObject.transform,
                text,
                fontSize,
                FontStyles.Normal,
                Color.black,
                TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(15f, 5f);
            label.rectTransform.offsetMax = new Vector2(-15f, -5f);
            return button;
        }

        private static Button CreateButton(
            string objectName,
            Transform parent,
            string text,
            Vector2 position,
            Vector2 size,
            Color color,
            out TMP_Text label)
        {
            GameObject buttonObject =
                CreateUiObject(objectName, parent);
            RectTransform rect =
                buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            Image image = buttonObject.AddComponent<Image>();
            image.color = color;
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            ExperimentUiTheme.StyleButton(button, color == (Color)Yellow);

            label = CreateText(
                "Text",
                buttonObject.transform,
                text,
                25f,
                FontStyles.Normal,
                Color.black,
                TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            return button;
        }

        private static TMP_InputField CreateInputField(
            string objectName,
            Transform parent,
            string initialValue,
            bool integer)
        {
            GameObject inputObject =
                CreateUiObject(objectName, parent);
            Image image = inputObject.AddComponent<Image>();
            image.color = Color.white;

            GameObject viewportObject =
                CreateUiObject("Text Area", inputObject.transform);
            RectTransform viewport =
                viewportObject.GetComponent<RectTransform>();
            Stretch(viewport);
            viewport.offsetMin = new Vector2(8f, 2f);
            viewport.offsetMax = new Vector2(-8f, -2f);
            viewportObject.AddComponent<RectMask2D>();

            TMP_Text text = CreateText(
                "Text",
                viewportObject.transform,
                initialValue,
                24f,
                FontStyles.Normal,
                Color.black,
                TextAlignmentOptions.MidlineLeft);
            Stretch(text.rectTransform);

            TMP_Text placeholder = CreateText(
                "Placeholder",
                viewportObject.transform,
                "必填",
                22f,
                FontStyles.Italic,
                new Color32(110, 110, 110, 255),
                TextAlignmentOptions.MidlineLeft);
            Stretch(placeholder.rectTransform);

            TMP_InputField input =
                inputObject.AddComponent<TMP_InputField>();
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.text = initialValue;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = integer
                ? TMP_InputField.ContentType.IntegerNumber
                : TMP_InputField.ContentType.DecimalNumber;
            input.caretColor = Color.black;
            input.selectionColor =
                new Color(0.3f, 0.5f, 0.9f, 0.45f);
            ExperimentUiTheme.StyleInput(input);
            return input;
        }

        private static TMP_Text CreateText(
            string objectName,
            Transform parent,
            string value,
            float fontSize,
            FontStyles style,
            Color color,
            TextAlignmentOptions alignment)
        {
            GameObject textObject =
                CreateUiObject(objectName, parent);
            TMP_Text text =
                textObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset preferredFont = ExperimentUiTheme.Font;
            if (preferredFont != null)
                text.font = preferredFont;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color == Color.black ? (Color)ExperimentUiTheme.TextColor : color;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }

        private static TMP_FontAsset GetPreferredFontAsset()
        {
            if (_preferredFontAsset != null)
                return _preferredFontAsset;

            if (_preferredFontLookupAttempted)
                return null;

            _preferredFontLookupAttempted = true;
            _preferredFontAsset =
                Resources.Load<TMP_FontAsset>(
                    PreferredFontResourcePath);

            // 编辑器中资源刚移动但尚未刷新时，仍尝试从已加载资源中查找。
            if (_preferredFontAsset == null)
            {
                TMP_FontAsset[] loadedFonts =
                    Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                for (int index = 0;
                     index < loadedFonts.Length;
                     index++)
                {
                    TMP_FontAsset candidate = loadedFonts[index];
                    if (candidate != null &&
                        string.Equals(
                            candidate.name,
                            PreferredFontAssetName,
                            StringComparison.Ordinal))
                    {
                        _preferredFontAsset = candidate;
                        break;
                    }
                }
            }

            if (_preferredFontAsset == null)
            {
                Debug.LogError(
                    "[AlgorithmAssessment] 未找到字体资源：" +
                    PreferredFontResourcePath);
            }

            return _preferredFontAsset;
        }

        private static GameObject CreateUiObject(
            string objectName,
            Transform parent)
        {
            GameObject result =
                new GameObject(objectName, typeof(RectTransform));
            result.transform.SetParent(parent, false);
            result.layer = parent.gameObject.layer;
            return result;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
                return;
            for (int index = parent.childCount - 1;
                 index >= 0;
                 index--)
            {
                parent.GetChild(index).gameObject.SetActive(false);
                Destroy(parent.GetChild(index).gameObject);
            }
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
            rect.offsetMin = new Vector2(left, -(top + height));
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
