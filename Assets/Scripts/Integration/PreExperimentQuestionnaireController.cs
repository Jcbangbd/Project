using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vtp.VR.Rest;

namespace Vtp.VR.Integration
{
    /// <summary>
    /// 评分细则第5至12项的全屏逐题答题页面。
    /// 一次只显示一题，答案只记录，不在客户端泄露正确答案或即时得分。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PreExperimentQuestionnaireController : MonoBehaviour
    {
        private const string FontResourcePath =
            "Fonts/AlibabaPuHuiTi-3-55-Regular SDF";

        private enum QuestionKind
        {
            SingleChoice,
            MultipleChoice,
            Numeric
        }

        private sealed class OptionDefinition
        {
            public readonly string Id;
            public readonly string Label;

            public OptionDefinition(string id, string label)
            {
                Id = id;
                Label = label;
            }
        }

        private sealed class NumericFieldDefinition
        {
            public readonly string Key;
            public readonly string Label;
            public readonly string Unit;

            public NumericFieldDefinition(
                string key,
                string label,
                string unit)
            {
                Key = key;
                Label = label;
                Unit = unit;
            }
        }

        private sealed class QuestionDefinition
        {
            public readonly string ItemId;
            public readonly string Title;
            public readonly string Prompt;
            public readonly QuestionKind Kind;
            public readonly OptionDefinition[] Options;
            public readonly NumericFieldDefinition[] NumericFields;

            public QuestionDefinition(
                string itemId,
                string title,
                string prompt,
                QuestionKind kind,
                OptionDefinition[] options = null,
                NumericFieldDefinition[] numericFields = null)
            {
                ItemId = itemId;
                Title = title;
                Prompt = prompt;
                Kind = kind;
                Options = options ?? new OptionDefinition[0];
                NumericFields = numericFields ??
                                new NumericFieldDefinition[0];
            }
        }

        private sealed class OptionVisual
        {
            public Image Image;
            public TMP_Text Label;
            public string OriginalLabel;
        }

        private static readonly Color32 PageGray =
            ExperimentUiTheme.Page;
        private static readonly Color32 HeaderGray =
            ExperimentUiTheme.Header;
        private static readonly Color32 ContentGray =
            ExperimentUiTheme.Panel;
        private static readonly Color32 Yellow =
            ExperimentUiTheme.Primary;
        private static readonly Color32 SelectedYellow =
            ExperimentUiTheme.Selected;
        private static readonly Color32 ErrorRed =
            ExperimentUiTheme.Error;

        private readonly Dictionary<string, HashSet<string>>
            _selectedOptions =
                new Dictionary<string, HashSet<string>>(
                    StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, string>>
            _numericAnswers =
                new Dictionary<string, Dictionary<string, string>>(
                    StringComparer.Ordinal);
        private readonly Dictionary<int, string> _item5TaskTypeAnswers =
            new Dictionary<int, string>();
        private readonly Dictionary<string, OptionVisual> _optionVisuals =
            new Dictionary<string, OptionVisual>(
                StringComparer.Ordinal);
        private QuestionDefinition[] _questions;
        [SerializeField] private RectTransform _questionPage;
        [SerializeField] private RectTransform _completionPage;
        [SerializeField] private RectTransform _answerContent;
        private RectTransform _dropdownPopupRoot;
        [SerializeField] private TMP_Text _progressText;
        [SerializeField] private Image _progressFill;
        [SerializeField] private TMP_Text _questionTitleText;
        [SerializeField] private TMP_Text _promptText;
        [SerializeField] private TMP_Text _errorText;
        [SerializeField] private Button _previousButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private TMP_Text _nextButtonText;
        private OperationFlowView _flowView;
        private string _lastFlowOption;
        private string _samplingError;
        private Action _onCaseUnavailable;
        private TMP_FontAsset _font;
        private SceneNumericalExampleDto[] _item5Examples =
            new SceneNumericalExampleDto[0];
        private int _questionIndex;
        private bool _initialized;
        private Action _onEnterExperiment;

        public bool IsOpen
        {
            get { return gameObject.activeSelf; }
        }

        public void Initialize(Action onEnterExperiment, Action onCaseUnavailable = null)
        {
            _onEnterExperiment = onEnterExperiment;
            _onCaseUnavailable = onCaseUnavailable;
            if (_initialized)
                return;

            _font = ExperimentUiTheme.Font;
            if (_font == null)
                Debug.LogError("[PreExperimentQuestionnaire] 找不到字体：" + FontResourcePath);

            _questions = CreateQuestions();
            if (_questionPage == null) BuildInterface();
            BindNavigation();
            _initialized = true;
            gameObject.SetActive(false);
        }

        public void Open(
            string taskId,
            SceneNumericalExampleDto[] originalExamples)
        {
            if (!_initialized)
                Initialize(null);

            _selectedOptions.Clear();
            _numericAnswers.Clear();
            _item5TaskTypeAnswers.Clear();
            _lastFlowOption = null;
            QuestionCaseSampler.TryCreate(originalExamples, Guid.NewGuid().GetHashCode(),
                out _item5Examples, out _samplingError);
            PreExperimentAssessmentRuntime.Begin(
                taskId,
                _item5Examples);

            Debug.Log(
                "[PreExperimentQuestionnaire] 第1题已按五种作业类型抽样，共 " +
                _item5Examples.Length + " 条任务。" );

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ShowQuestion(0);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public const float AnswerFontSize = 32f;
        public void BuildLayoutForEditor() { BuildInterface(); }

        private void BindNavigation()
        {
            _previousButton.onClick.RemoveListener(PreviousQuestion);
            _nextButton.onClick.RemoveListener(NextQuestion);
            _previousButton.onClick.AddListener(PreviousQuestion);
            _nextButton.onClick.AddListener(NextQuestion);
            var enter = _completionPage.GetComponentInChildren<Button>(true);
            if (enter != null) { enter.onClick.RemoveListener(EnterExperiment); enter.onClick.AddListener(EnterExperiment); }
        }

        private void BuildInterface()
        {
            RectTransform root = GetComponent<RectTransform>();
            Stretch(root);
            Image background = GetComponent<Image>();
            if (background == null)
                background = gameObject.AddComponent<Image>();
            background.color = PageGray;
            background.raycastTarget = true;

            RectTransform surface = ExperimentUiTheme.DesignSurface(transform, "QuestionnaireSurface");
            RectTransform header = ExperimentUiTheme.HeaderBar(surface, "实验结束答题");

            _progressText = CreateText(
                "ProgressText",
                header,
                string.Empty,
                25f,
                FontStyles.Normal,
                Color.white,
                TextAlignmentOptions.MidlineRight);
            RectTransform progressRect = _progressText.rectTransform;
            progressRect.anchorMin = new Vector2(0.68f, 0f);
            progressRect.anchorMax = new Vector2(0.97f, 1f);
            progressRect.offsetMin = new Vector2(0f, 30f);
            progressRect.offsetMax = new Vector2(-16f, -8f);
            Image track = CreateUiObject("ProgressTrack", header).AddComponent<Image>();
            track.color = new Color32(93, 102, 112, 255);
            track.sprite = ExperimentUiTheme.BuiltinSprite("AssessmentSquare");
            track.type = Image.Type.Sliced;
            SetBottomStretch(track.rectTransform, 20f, 1510f, 72f, 7f);
            _progressFill = CreateUiObject("ProgressFill", track.transform).AddComponent<Image>();
            _progressFill.color = Yellow;
            _progressFill.sprite = ExperimentUiTheme.BuiltinSprite("AssessmentSquare");
            _progressFill.type = Image.Type.Sliced;
            Stretch(_progressFill.rectTransform);
            _progressFill.raycastTarget = track.raycastTarget = false;

            _questionPage = CreateUiObject(
                "QuestionPage",
                surface).GetComponent<RectTransform>();
            _questionPage.anchorMin = Vector2.zero;
            _questionPage.anchorMax = Vector2.one;
            _questionPage.offsetMin = new Vector2(64f, 132f);
            _questionPage.offsetMax = new Vector2(-64f, -114f);

            _questionTitleText = CreateText(
                "QuestionTitle",
                _questionPage,
                string.Empty,
                40f,
                FontStyles.Bold,
                Color.black,
                TextAlignmentOptions.MidlineLeft);
            SetTopStretch(
                _questionTitleText.rectTransform,
                0f,
                10f,
                10f,
                60f);

            _promptText = CreateText(
                "Prompt",
                _questionPage,
                string.Empty,
                29f,
                FontStyles.Normal,
                ExperimentUiTheme.Muted,
                TextAlignmentOptions.TopLeft);
            SetTopStretch(
                _promptText.rectTransform,
                68f,
                10f,
                10f,
                96f);

            _answerContent = CreateScrollArea(
                "AnswerScrollView",
                _questionPage,
                168f,
                36f);

            _errorText = CreateText(
                "ErrorText",
                _questionPage,
                string.Empty,
                21f,
                FontStyles.Normal,
                ErrorRed,
                TextAlignmentOptions.Center);
            SetBottomStretch(
                _errorText.rectTransform,
                2f,
                10f,
                10f,
                36f);

            _completionPage = CreateUiObject(
                "CompletionPage",
                surface).GetComponent<RectTransform>();
            _completionPage.anchorMin = Vector2.zero;
            _completionPage.anchorMax = Vector2.one;
            _completionPage.offsetMin = new Vector2(72f, 152f);
            _completionPage.offsetMax = new Vector2(-72f, -128f);
            ExperimentUiTheme.SciFiSurface(
                _completionPage.gameObject.AddComponent<Image>(),
                ContentGray);

            TMP_Text completeTitle = CreateText(
                "CompleteTitle",
                _completionPage,
                "已回答所有问题",
                48f,
                FontStyles.Bold,
                Color.black,
                TextAlignmentOptions.Center);
            completeTitle.rectTransform.anchorMin =
                new Vector2(0.08f, 0.56f);
            completeTitle.rectTransform.anchorMax =
                new Vector2(0.92f, 0.75f);
            completeTitle.rectTransform.offsetMin = Vector2.zero;
            completeTitle.rectTransform.offsetMax = Vector2.zero;

            TMP_Text completeInfo = CreateText(
                "CompleteInfo",
                _completionPage,
                "第1至8题已作答完成。点击提交，统一保存本次成绩并返回实验介绍。",
                29f,
                FontStyles.Normal,
                Color.black,
                TextAlignmentOptions.Center);
            completeInfo.rectTransform.anchorMin =
                new Vector2(0.08f, 0.41f);
            completeInfo.rectTransform.anchorMax =
                new Vector2(0.92f, 0.56f);
            completeInfo.rectTransform.offsetMin = Vector2.zero;
            completeInfo.rectTransform.offsetMax = Vector2.zero;

            Button enterButton = CreateButton(
                "EnterExperimentButton",
                _completionPage,
                "提交答题并返回实验介绍",
                new Vector2(0.5f, 0.28f),
                new Vector2(360f, 66f),
                Yellow,
                out _);
            enterButton.onClick.AddListener(EnterExperiment);

            RectTransform footer = ExperimentUiTheme.FooterBar(surface);
            TMP_Text helper = ExperimentUiTheme.Text("FooterHint", footer,
                "请完整作答后继续。", 22f, ExperimentUiTheme.Muted);
            SetTopStretch(helper.rectTransform, 34f, 72f, 720f, 64f);

            _previousButton = CreateButton(
                "PreviousButton",
                footer,
                "上一题",
                new Vector2(0.34f, 0.5f),
                new Vector2(250f, 58f),
                Color.white,
                out _);
            ExperimentUiTheme.FooterButton(_previousButton.GetComponent<RectTransform>(), 368f, 240f);
            _previousButton.onClick.AddListener(PreviousQuestion);

            _nextButton = CreateButton(
                "NextButton",
                footer,
                "下一题",
                new Vector2(0.66f, 0.5f),
                new Vector2(290f, 58f),
                Yellow,
                out _nextButtonText);
            ExperimentUiTheme.FooterButton(_nextButton.GetComponent<RectTransform>(), 72f, 260f);
            _nextButton.onClick.AddListener(NextQuestion);
        }

        private void ShowQuestion(int index)
        {
            _questionIndex = Mathf.Clamp(index, 0, _questions.Length - 1);
            _questionPage.gameObject.SetActive(true);
            _completionPage.gameObject.SetActive(false);
            _previousButton.gameObject.SetActive(true);
            _nextButton.gameObject.SetActive(true);
            _progressText.gameObject.SetActive(true);

            QuestionDefinition question = _questions[_questionIndex];
            _flowView = null;
            var answerRect=_answerContent.GetComponentInParent<ScrollRect>().GetComponent<RectTransform>();
            bool caseQuestion=question.ItemId=="5";
            answerRect.offsetMin=new Vector2(10f,caseQuestion?0f:36f);
            answerRect.offsetMax=new Vector2(-10f,caseQuestion?-150f:-168f);
            _progressText.text =
                "第 " + (_questionIndex + 1) + " / " +
                _questions.Length + " 题";
            _progressFill.rectTransform.anchorMax = new Vector2(
                (float)(_questionIndex + 1) / _questions.Length, 1f);
            _questionTitleText.text =
                "题目 " + (_questionIndex + 1) + "  " + question.Title;
            _promptText.text = question.Prompt;
            _errorText.text = _questionIndex == 0 ? (_samplingError ?? string.Empty) : string.Empty;
            _previousButton.interactable = _questionIndex > 0;
            _nextButtonText.text =
                _questionIndex == _questions.Length - 1
                    ? "提交全部答案"
                    : "下一题";
            if (_questionIndex == 0 && !string.IsNullOrEmpty(_samplingError))
                _nextButtonText.text = "返回重新获取";

            ClearChildren(_answerContent);
            if (_dropdownPopupRoot != null)
                ClearChildren(_dropdownPopupRoot);
            _optionVisuals.Clear();
            if (question.Kind == QuestionKind.Numeric)
                BuildNumericFields(question);
            else
                BuildOptions(question);
        }

        private void BuildOptions(QuestionDefinition question)
        {
            if (string.Equals(
                    question.ItemId,
                    "5",
                    StringComparison.Ordinal))
            {
                BuildExampleRows();
                return;
            }

            HashSet<string> selected = GetSelections(question.ItemId);
            for (int index = 0; index < question.Options.Length; index++)
            {
                OptionDefinition option = question.Options[index];
                Button button = CreateLayoutButton(
                    "Option_" + option.Id,
                    _answerContent,
                    option.Label,
                    78f,
                    Color.white,
                    AnswerFontSize,
                    out TMP_Text label);
                ExperimentUiTheme.ChoiceIndicator(button.transform,
                    question.Kind == QuestionKind.MultipleChoice);
                label.rectTransform.offsetMin = new Vector2(66f, 8f);
                string optionId = option.Id;
                button.onClick.AddListener(
                    () => ToggleOption(question, optionId));
                _optionVisuals.Add(option.Id, new OptionVisual
                {
                    Image = button.GetComponent<Image>(),
                    Label = label,
                    OriginalLabel = option.Label
                });
            }

            RefreshOptionVisuals(selected);
        }

        private void ToggleOption(
            QuestionDefinition question,
            string optionId)
        {
            HashSet<string> selected = GetSelections(question.ItemId);
            if (question.Kind == QuestionKind.SingleChoice)
            {
                selected.Clear();
                selected.Add(optionId);
            }
            else if (!selected.Add(optionId))
            {
                selected.Remove(optionId);
            }

            _errorText.text = string.Empty;
            RefreshOptionVisuals(selected);
        }

        private void RefreshOptionVisuals(HashSet<string> selected)
        {
            foreach (KeyValuePair<string, OptionVisual> pair in _optionVisuals)
            {
                bool isSelected = selected.Contains(pair.Key);
                ExperimentUiTheme.SelectedState(pair.Value.Image, isSelected);
                pair.Value.Label.text = pair.Value.OriginalLabel;
            }
        }

        private void BuildNumericFields(QuestionDefinition question)
        {
            Dictionary<string, string> values =
                GetNumericAnswers(question.ItemId);
            for (int index = 0;
                 index < question.NumericFields.Length;
                 index++)
            {
                NumericFieldDefinition field =
                    question.NumericFields[index];
                GameObject rowObject = CreateUiObject(
                    "Numeric_" + field.Key,
                    _answerContent);
                ExperimentUiTheme.SciFiSurface(
                    rowObject.AddComponent<Image>(),
                    ContentGray);
                LayoutElement layout = rowObject.AddComponent<LayoutElement>();
                layout.minHeight = 110f;
                layout.preferredHeight = 110f;

                TMP_Text label = CreateText(
                    "Label",
                    rowObject.transform,
                    field.Label,
                    AnswerFontSize,
                    FontStyles.Normal,
                    Color.black,
                    TextAlignmentOptions.MidlineLeft);
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(0.54f, 1f);
                labelRect.offsetMin = new Vector2(24f, 6f);
                labelRect.offsetMax = new Vector2(-12f, -6f);

                string existingValue;
                values.TryGetValue(field.Key, out existingValue);
                TMP_InputField input = CreateInputField(
                    "Input",
                    rowObject.transform,
                    existingValue ?? string.Empty);
                RectTransform inputRect = input.GetComponent<RectTransform>();
                inputRect.anchorMin = new Vector2(0.55f, 0.5f);
                inputRect.anchorMax = new Vector2(0.84f, 0.5f);
                inputRect.offsetMin = new Vector2(0f, -31f);
                inputRect.offsetMax = new Vector2(0f, 31f);

                TMP_Text unit = CreateText(
                    "Unit",
                    rowObject.transform,
                    field.Unit,
                    AnswerFontSize,
                    FontStyles.Normal,
                    Color.black,
                    TextAlignmentOptions.MidlineLeft);
                unit.rectTransform.anchorMin = new Vector2(0.86f, 0f);
                unit.rectTransform.anchorMax = new Vector2(1f, 1f);
                unit.rectTransform.offsetMin = Vector2.zero;
                unit.rectTransform.offsetMax = Vector2.zero;

                string capturedKey = field.Key;
                input.onValueChanged.AddListener(value =>
                {
                    values[capturedKey] = value;
                    _errorText.text = string.Empty;
                });
            }
        }

        private void PreviousQuestion()
        {
            SaveCurrentAnswer();
            if (_questionIndex > 0)
                ShowQuestion(_questionIndex - 1);
        }

        private void NextQuestion()
        {
            if (_questionIndex == 0 && !string.IsNullOrEmpty(_samplingError))
            {
                if (_onCaseUnavailable != null) _onCaseUnavailable();
                return;
            }
            QuestionDefinition question = _questions[_questionIndex];
            if (!HasAnswer(question))
            {
                if (string.Equals(
                        question.ItemId,
                        "5",
                        StringComparison.Ordinal))
                {
                    _errorText.text =
                        "请为每一条算例选择作业类型后再继续。";
                }
                else
                {
                    _errorText.text = question.Kind == QuestionKind.Numeric
                        ? "请填写本题的全部输入项后再继续。"
                        : "请至少选择一个答案后再继续。";
                }
                return;
            }

            SaveCurrentAnswer();
            if (_questionIndex < _questions.Length - 1)
            {
                ShowQuestion(_questionIndex + 1);
                return;
            }

            PreExperimentAssessmentRuntime.Complete();
            ShowCompletion();
        }

        private bool HasAnswer(QuestionDefinition question)
        {
            if (string.Equals(
                    question.ItemId,
                    "5",
                    StringComparison.Ordinal))
            {
                if (_item5Examples.Length == 0 ||
                    _item5TaskTypeAnswers.Count != _item5Examples.Length)
                {
                    return false;
                }

                for (int index = 0;
                     index < _item5Examples.Length;
                     index++)
                {
                    string optionId;
                    if (!_item5TaskTypeAnswers.TryGetValue(
                            index,
                            out optionId) ||
                        string.IsNullOrWhiteSpace(optionId))
                    {
                        return false;
                    }
                }

                return true;
            }

            if (question.Kind != QuestionKind.Numeric)
                return GetSelections(question.ItemId).Count > 0;

            Dictionary<string, string> values =
                GetNumericAnswers(question.ItemId);
            for (int index = 0;
                 index < question.NumericFields.Length;
                 index++)
            {
                string value;
                if (!values.TryGetValue(
                        question.NumericFields[index].Key,
                        out value) ||
                    string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }
            }

            return true;
        }

        private void SaveCurrentAnswer()
        {
            QuestionDefinition question = _questions[_questionIndex];
            if (string.Equals(
                    question.ItemId,
                    "5",
                    StringComparison.Ordinal))
            {
                List<PreExperimentCaseTaskTypeAnswerDto> caseAnswers =
                    new List<PreExperimentCaseTaskTypeAnswerDto>();
                for (int index = 0;
                     index < _item5Examples.Length;
                     index++)
                {
                    SceneNumericalExampleDto example =
                        _item5Examples[index];
                    string selectedOptionId;
                    _item5TaskTypeAnswers.TryGetValue(
                        index,
                        out selectedOptionId);
                    caseAnswers.Add(new PreExperimentCaseTaskTypeAnswerDto
                    {
                        rowIndex = index,
                        exampleId = example == null
                            ? string.Empty
                            : example.id ?? string.Empty,
                        exampleNo = example == null
                            ? string.Empty
                            : example.no ?? string.Empty,
                        selectedOptionId = selectedOptionId ?? string.Empty
                    });
                }

                PreExperimentAssessmentRuntime.SaveCaseTaskTypes(
                    question.ItemId,
                    question.Title,
                    caseAnswers);
                return;
            }

            if (question.Kind != QuestionKind.Numeric)
            {
                PreExperimentAssessmentRuntime.SaveSelection(
                    question.ItemId,
                    question.Title,
                    GetSelections(question.ItemId));
                return;
            }

            Dictionary<string, string> values =
                GetNumericAnswers(question.ItemId);
            List<PreExperimentNumericAnswerDto> answers =
                new List<PreExperimentNumericAnswerDto>();
            for (int index = 0;
                 index < question.NumericFields.Length;
                 index++)
            {
                NumericFieldDefinition field =
                    question.NumericFields[index];
                string value;
                values.TryGetValue(field.Key, out value);
                answers.Add(new PreExperimentNumericAnswerDto
                {
                    key = field.Key,
                    value = value ?? string.Empty,
                    unit = field.Unit
                });
            }

            PreExperimentAssessmentRuntime.SaveNumeric(
                question.ItemId,
                question.Title,
                answers);
        }

        private void ShowCompletion()
        {
            _questionPage.gameObject.SetActive(false);
            _completionPage.gameObject.SetActive(true);
            _previousButton.gameObject.SetActive(false);
            _nextButton.gameObject.SetActive(false);
            _progressText.gameObject.SetActive(false);
        }

        private void EnterExperiment()
        {
            Hide();
            if (_onEnterExperiment != null)
                _onEnterExperiment();
        }

        private HashSet<string> GetSelections(string itemId)
        {
            HashSet<string> result;
            if (!_selectedOptions.TryGetValue(itemId, out result))
            {
                result = new HashSet<string>(StringComparer.Ordinal);
                _selectedOptions.Add(itemId, result);
            }
            return result;
        }

        private Dictionary<string, string> GetNumericAnswers(string itemId)
        {
            Dictionary<string, string> result;
            if (!_numericAnswers.TryGetValue(itemId, out result))
            {
                result = new Dictionary<string, string>(StringComparer.Ordinal);
                _numericAnswers.Add(itemId, result);
            }
            return result;
        }

        private static QuestionDefinition[] CreateQuestions()
        {
            return new[]
            {
                new QuestionDefinition(
                    "5",
                    "算例作业类型勾选",
                "请选择每条任务的作业类型。五条全部正确得5分，否则得0分。\n位置规则：0贝为海侧支架，1至50贝为堆场，51贝为陆侧车道。",
                    QuestionKind.MultipleChoice,
                    new[]
                    {
                        new OptionDefinition("DISCHARGE", "卸船"),
                        new OptionDefinition("LOAD_SHIP", "装船"),
                        new OptionDefinition("REHANDLE", "翻箱"),
                        new OptionDefinition("UNLOAD_TRUCK", "卸车"),
                        new OptionDefinition("LOAD_TRUCK", "装车")
                    }),
                new QuestionDefinition(
                    "6",
                    "优化目标选择",
                    "选择本调度模型采用的优化目标。",
                    QuestionKind.SingleChoice,
                    new[]
                    {
                        new OptionDefinition("MIN_CMAX", "最小化最大完工时间（Cmax）"),
                        new OptionDefinition("MIN_EMPTY_TRAVEL", "最小化设备空驶距离"),
                        new OptionDefinition("MIN_ENERGY", "最小化设备总能耗"),
                        new OptionDefinition("MIN_WAITING", "最小化任务平均等待时间")
                    }),
                new QuestionDefinition(
                    "7",
                    "最小安全距离设置",
                    "输入两台RMG之间的最小安全距离。",
                    QuestionKind.Numeric,
                    numericFields: new[]
                    {
                        new NumericFieldDefinition("MIN_CRANE_DISTANCE", "两台RMG最小安全距离", "贝位")
                    }),
                new QuestionDefinition(
                    "8",
                    "水平移动速度设置",
                    "分别输入大车贝位方向速度和小车列方向速度。",
                    QuestionKind.Numeric,
                    numericFields: new[]
                    {
                        new NumericFieldDefinition("GANTRY_SPEED", "大车贝位方向速度", "m/s"),
                        new NumericFieldDefinition("TROLLEY_SPEED", "小车列方向速度", "m/s")
                    }),
                new QuestionDefinition(
                    "9",
                    "起升速度设置",
                    "输入吊具垂直起升速度。",
                    QuestionKind.Numeric,
                    numericFields: new[]
                    {
                        new NumericFieldDefinition("HOIST_SPEED", "吊具垂直起升速度", "m/s")
                    }),
                new QuestionDefinition(
                    "10",
                    "任务分配规则选择",
                    "勾选固定区任务和共享区任务应采用的全部分配规则。可多选。",
                    QuestionKind.MultipleChoice,
                    new[]
                    {
                        new OptionDefinition("SEA_FIXED_SHARED", "海侧RMG服务海侧固定区和共享区"),
                        new OptionDefinition("LAND_FIXED_SHARED", "陆侧RMG服务陆侧固定区和共享区"),
                        new OptionDefinition("SEA_FIXED_ONLY", "海侧RMG只能服务海侧固定区，不能进入共享区"),
                        new OptionDefinition("RANDOM_ASSIGNMENT", "全部任务在两台RMG之间随机分配")
                    }),
                new QuestionDefinition(
                    "11",
                    "必要约束勾选",
                    "勾选调度模型必须包含的全部约束。可多选。",
                    QuestionKind.MultipleChoice,
                    new[]
                    {
                        new OptionDefinition("COLLISION_AVOIDANCE", "防碰撞约束"),
                        new OptionDefinition("SUBTOUR_ELIMINATION", "子回路消除约束"),
                        new OptionDefinition("ALLOW_CRANE_CROSSING", "允许两台RMG交叉运行"),
                        new OptionDefinition("IGNORE_SAFE_DISTANCE", "忽略两台RMG之间的安全距离")
                    }),
                new QuestionDefinition(
                    "12",
                    "初始解规则选择",
                    "从以下方案中选择一种初始解构造规则。",
                    QuestionKind.SingleChoice,
                    new[]
                    {
                        new OptionDefinition("FIRST_COME_FIRST_SERVE", "先到先服务"),
                        new OptionDefinition("NEAREST_DISTANCE", "最近距离"),
                        new OptionDefinition("LOAD_BALANCING", "负荷均衡")
                    })
            };
        }

        private void BuildExampleRows()
        {
            GameObject captionObject = CreateUiObject(
                "RandomExampleCaption",
                _answerContent);
            captionObject.AddComponent<Image>().color = PageGray;
            LayoutElement captionLayout =
                captionObject.AddComponent<LayoutElement>();
            captionLayout.minHeight = 30f;
            captionLayout.preferredHeight = 30f;
            TMP_Text caption = CreateText(
                "Text",
                captionObject.transform,
                "教师算例任务（" + _item5Examples.Length +
                "条，作业类型待勾选）",
                27f,
                FontStyles.Normal,
                ExperimentUiTheme.Muted,
                TextAlignmentOptions.MidlineLeft);
            Stretch(caption.rectTransform);

            CreateRandomExampleRow(
                "RandomExampleHeader",
                new[]
                {
                    "任务编号", "起始位置", "目标位置", "作业类型选择"
                },
                true,
                0);

            for (int index = 0;
                 index < _item5Examples.Length;
                 index++)
            {
                SceneNumericalExampleDto example =
                    _item5Examples[index];
                if (example == null)
                    continue;

                CreateExampleAnswerRow(
                    "RandomExample_" + (index + 1),
                    new[]
                    {
                        string.IsNullOrWhiteSpace(example.no)
                            ? (index + 1).ToString()
                            : example.no,
                        example.startPointPosition.ToString("0.##") +
                        " / " + example.startList.ToString("0.##"),
                        example.endPointPosition.ToString("0.##") +
                        " / " + example.endList.ToString("0.##")
                    },
                    _questions[_questionIndex].Options,
                    index);
            }
            var flowPrefab = Resources.Load<OperationFlowView>("OperationFlowView");
            if (flowPrefab != null)
            {
                _flowView = Instantiate(flowPrefab, _answerContent, false);
                _flowView.UseQuestionLayout();
                var element = _flowView.GetComponent<LayoutElement>();
                if (element != null) element.preferredHeight = 240f;
                _flowView.gameObject.SetActive(false);
                if (!string.IsNullOrEmpty(_lastFlowOption)) _flowView.Show(_lastFlowOption);
            }
        }

        private void CreateExampleAnswerRow(
            string objectName,
            string[] values,
            OptionDefinition[] taskTypeOptions,
            int rowIndex)
        {
            GameObject rowObject = CreateUiObject(
                objectName,
                _answerContent);
            rowObject.AddComponent<Image>().color =
                rowIndex % 2 == 0
                    ? (Color)ExperimentUiTheme.PanelRaised
                    : (Color)ExperimentUiTheme.Alternate;
            ExperimentUiTheme.Divider(rowObject.transform);
            LayoutElement rowElement =
                rowObject.AddComponent<LayoutElement>();
            rowElement.minHeight = 72f;
            rowElement.preferredHeight = 72f;

            HorizontalLayoutGroup rowLayout =
                rowObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(12, 12, 10, 10);
            rowLayout.spacing = 5f;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;

            float[] widths = { 0.7f, 1.3f, 1.3f };
            for (int index = 0;
                 index < values.Length && index < widths.Length;
                 index++)
            {
                TMP_Text cell = CreateText(
                    "Column" + index,
                    rowObject.transform,
                    values[index] ?? "-",
                    32f,
                    FontStyles.Normal,
                    Color.black,
                    TextAlignmentOptions.Center);
                LayoutElement cellLayout =
                    cell.gameObject.AddComponent<LayoutElement>();
                cellLayout.flexibleWidth = widths[index];
                cellLayout.preferredWidth = widths[index] * 150f;
            }

            Button dropdown = CreateTaskTypeDropdown(
                rowObject.transform,
                taskTypeOptions,
                rowIndex);
            LayoutElement dropdownLayout =
                dropdown.gameObject.AddComponent<LayoutElement>();
            dropdownLayout.flexibleWidth = 1.35f;
            dropdownLayout.preferredWidth = 1.35f * 150f;
        }

        private void CreateRandomExampleRow(
            string objectName,
            string[] values,
            bool header,
            int rowIndex)
        {
            GameObject rowObject = CreateUiObject(
                objectName,
                _answerContent);
            rowObject.AddComponent<Image>().color =
                header
                    ? ExperimentUiTheme.TableHeader
                    : rowIndex % 2 == 0
                        ? ExperimentUiTheme.PanelRaised
                         : ExperimentUiTheme.Alternate;
            LayoutElement rowElement =
                rowObject.AddComponent<LayoutElement>();
            rowElement.minHeight = header ? 54f : 72f;
            rowElement.preferredHeight = header ? 54f : 72f;

            HorizontalLayoutGroup rowLayout =
                rowObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(8, 8, 2, 2);
            rowLayout.spacing = 5f;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;

            float[] widths = { 0.7f, 1.3f, 1.3f, 1.35f };
            for (int index = 0;
                 index < values.Length && index < widths.Length;
                 index++)
            {
                TMP_Text cell = CreateText(
                    "Column" + index,
                    rowObject.transform,
                    values[index] ?? "-",
                    header ? 30f : 32f,
                    header ? FontStyles.Bold : FontStyles.Normal,
                    Color.black,
                    TextAlignmentOptions.Center);
                LayoutElement cellLayout =
                    cell.gameObject.AddComponent<LayoutElement>();
                cellLayout.flexibleWidth = widths[index];
                cellLayout.preferredWidth = widths[index] * 150f;
            }
        }

        private Button CreateTaskTypeDropdown(
            Transform parent,
            OptionDefinition[] taskTypeOptions,
            int rowIndex)
        {
            GameObject dropdownObject = CreateUiObject(
                "TaskTypeDropdown",
                parent);
            Image background = dropdownObject.AddComponent<Image>();
            background.color = Color.white;
            Button dropdown = dropdownObject.AddComponent<Button>();
            dropdown.targetGraphic = background;
            Outline dropdownBorder = ExperimentUiTheme.SciFiSurface(
                background,
                Color.white,
                true,
                false);

            string selectedOptionId;
            bool hasSelection = _item5TaskTypeAnswers.TryGetValue(
                rowIndex,
                out selectedOptionId);
            string selectedLabel = "请选择";
            for (int index = 0; index < taskTypeOptions.Length; index++)
            {
                if (string.Equals(
                        taskTypeOptions[index].Id,
                        selectedOptionId,
                        StringComparison.Ordinal))
                {
                    selectedLabel = taskTypeOptions[index].Label;
                    hasSelection = true;
                    break;
                }
            }

            TMP_Text caption = CreateText(
                "Label",
                dropdownObject.transform,
                selectedLabel,
                32f,
                FontStyles.Normal,
                Color.black,
                TextAlignmentOptions.MidlineLeft);
            Stretch(caption.rectTransform);
            caption.rectTransform.offsetMin = new Vector2(12f, 2f);
            caption.rectTransform.offsetMax = new Vector2(-34f, -2f);
            caption.enableWordWrapping = false;

            Image arrow = CreateUiObject("Arrow", dropdownObject.transform).AddComponent<Image>();
            arrow.sprite = ExperimentUiTheme.BuiltinSprite("DropdownArrow");
            arrow.color = ExperimentUiTheme.TextColor;
            arrow.raycastTarget = false;
            RectTransform arrowRect = arrow.rectTransform;
            arrowRect.anchorMin = new Vector2(1f, 0.5f);
            arrowRect.anchorMax = new Vector2(1f, 0.5f);
            arrowRect.pivot = new Vector2(1f, 0.5f);
            arrowRect.sizeDelta = new Vector2(36f, 24f);
            arrowRect.anchoredPosition = new Vector2(-16f, 0f);

            SetTaskTypeSelectorVisual(
                dropdown,
                dropdownBorder,
                hasSelection);
            RectTransform controlRect =
                dropdownObject.GetComponent<RectTransform>();
            dropdown.onClick.AddListener(() => OpenTaskTypeMenu(
                controlRect,
                dropdown,
                dropdownBorder,
                caption,
                taskTypeOptions,
                rowIndex));
            return dropdown;
        }

        private void OpenTaskTypeMenu(
            RectTransform control,
            Button selector,
            Outline selectorBorder,
            TMP_Text caption,
            OptionDefinition[] options,
            int rowIndex)
        {
            RectTransform popupRoot = GetDropdownPopupRoot();
            CloseTaskTypeMenu();
            Canvas.ForceUpdateCanvases();

            GameObject blockerObject = CreateUiObject(
                "TaskTypeMenuBlocker",
                popupRoot);
            RectTransform blockerRect =
                blockerObject.GetComponent<RectTransform>();
            Stretch(blockerRect);
            Image blockerImage = blockerObject.AddComponent<Image>();
            blockerImage.color = new Color(0f, 0f, 0f, 0.01f);
            Button blocker = blockerObject.AddComponent<Button>();
            blocker.targetGraphic = blockerImage;
            blocker.transition = Selectable.Transition.None;
            blocker.onClick.AddListener(CloseTaskTypeMenu);

            const float itemHeight = 62f;
            float menuHeight = options.Length * itemHeight + 8f;
            Vector3[] worldCorners = new Vector3[4];
            control.GetWorldCorners(worldCorners);
            Vector3 localBottomLeft =
                popupRoot.InverseTransformPoint(worldCorners[0]);
            Vector3 localTopLeft =
                popupRoot.InverseTransformPoint(worldCorners[1]);
            Vector3 localBottomRight =
                popupRoot.InverseTransformPoint(worldCorners[3]);
            float menuWidth = Mathf.Abs(
                localBottomRight.x - localBottomLeft.x);
            bool openBelow =
                localBottomLeft.y - popupRoot.rect.yMin >= menuHeight + 8f;

            GameObject menuObject = CreateUiObject(
                "TaskTypeMenu",
                popupRoot);
            RectTransform menuRect =
                menuObject.GetComponent<RectTransform>();
            menuRect.anchorMin = popupRoot.pivot;
            menuRect.anchorMax = popupRoot.pivot;
            menuRect.pivot = openBelow
                ? new Vector2(0f, 1f)
                : new Vector2(0f, 0f);
            float left = Mathf.Clamp(
                localBottomLeft.x,
                popupRoot.rect.xMin + 8f,
                popupRoot.rect.xMax - menuWidth - 8f);
            menuRect.anchoredPosition = new Vector2(
                left,
                openBelow ? localBottomLeft.y - 4f : localTopLeft.y + 4f);
            menuRect.sizeDelta = new Vector2(menuWidth, menuHeight);
            ExperimentUiTheme.SciFiSurface(
                menuObject.AddComponent<Image>(),
                Color.white,
                true,
                true);

            VerticalLayoutGroup layout =
                menuObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.spacing = 0f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            string selectedId;
            _item5TaskTypeAnswers.TryGetValue(rowIndex, out selectedId);
            for (int index = 0; index < options.Length; index++)
            {
                OptionDefinition option = options[index];
                GameObject optionObject = CreateUiObject(
                    "Option_" + option.Id,
                    menuObject.transform);
                Image optionImage = optionObject.AddComponent<Image>();
                Button optionButton = optionObject.AddComponent<Button>();
                optionButton.targetGraphic = optionImage;
                ExperimentUiTheme.StyleChoice(optionButton);
                optionObject.AddComponent<LayoutElement>().preferredHeight =
                    itemHeight;

                bool selected = string.Equals(
                    selectedId,
                    option.Id,
                    StringComparison.Ordinal);
                ExperimentUiTheme.SelectedState(optionImage, selected);
                TMP_Text optionText = CreateText(
                    "Label",
                    optionObject.transform,
                    option.Label,
                    AnswerFontSize,
                    selected ? FontStyles.Bold : FontStyles.Normal,
                    Color.black,
                    TextAlignmentOptions.MidlineLeft);
                Stretch(optionText.rectTransform);
                optionText.rectTransform.offsetMin =
                    new Vector2(16f, 2f);
                optionText.rectTransform.offsetMax =
                    new Vector2(-12f, -2f);

                string optionId = option.Id;
                string optionLabel = option.Label;
                optionButton.onClick.AddListener(() =>
                {
                    _item5TaskTypeAnswers[rowIndex] = optionId;
                    caption.text = optionLabel;
                    SetTaskTypeSelectorVisual(
                        selector,
                        selectorBorder,
                        true);
                    _errorText.text = string.Empty;
                    _lastFlowOption = optionId;
                    if (_flowView != null)
                    {
                        _flowView.Show(optionId);
                        Canvas.ForceUpdateCanvases();
                        LayoutRebuilder.ForceRebuildLayoutImmediate(_answerContent);
                    }
                    CloseTaskTypeMenu();
                });
            }

            menuObject.transform.SetAsLastSibling();
        }

        private static void SetTaskTypeSelectorVisual(
            Button selector,
            Outline border,
            bool selected)
        {
            Color normal = selected
                ? (Color)ExperimentUiTheme.Selected
                : (Color)ExperimentUiTheme.PanelInput;
            ColorBlock colors = selector.colors;
            colors.normalColor = normal;
            colors.highlightedColor = selected
                ? (Color)ExperimentUiTheme.PanelPressed
                : (Color)ExperimentUiTheme.PanelHover;
            colors.pressedColor = ExperimentUiTheme.PanelPressed;
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = ExperimentUiTheme.Page;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            selector.colors = colors;
            if (selector.targetGraphic != null)
                selector.targetGraphic.color = Color.white;
            if (border != null)
                border.effectColor = selected
                    ? (Color)ExperimentUiTheme.SelectedBorder
                    : (Color)ExperimentUiTheme.Border;
        }

        private void CloseTaskTypeMenu()
        {
            if (_dropdownPopupRoot == null)
                return;
            for (int index = _dropdownPopupRoot.childCount - 1;
                 index >= 0;
                 index--)
            {
                GameObject child =
                    _dropdownPopupRoot.GetChild(index).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private RectTransform GetDropdownPopupRoot()
        {
            if (_dropdownPopupRoot != null)
            {
                _dropdownPopupRoot.SetAsLastSibling();
                return _dropdownPopupRoot;
            }

            Transform surface = _questionPage != null
                ? _questionPage.parent
                : transform;
            _dropdownPopupRoot = CreateUiObject(
                "TaskTypeDropdownPopupRoot",
                surface).GetComponent<RectTransform>();
            Stretch(_dropdownPopupRoot);
            _dropdownPopupRoot.SetAsLastSibling();
            return _dropdownPopupRoot;
        }

        private static SceneNumericalExampleDto[]
            CreateExampleSample(
                SceneNumericalExampleDto[] allExamples,
                int requestedCount)
        {
            List<SceneNumericalExampleDto> candidates =
                new List<SceneNumericalExampleDto>();
            if (allExamples != null)
            {
                for (int index = 0; index < allExamples.Length; index++)
                {
                    SceneNumericalExampleDto example = allExamples[index];
                    string inferredTaskType;
                    if (SceneExampleTaskCaseAdapter
                        .TryInferTaskTypeFromPositions(
                            example,
                            out inferredTaskType))
                    {
                        candidates.Add(example);
                    }
                    else if (example != null)
                    {
                        Debug.LogWarning(
                            "[PreExperimentQuestionnaire] 已跳过无法根据起止位置判定作业类型的算例：" +
                            (string.IsNullOrWhiteSpace(example.no)
                                ? (index + 1).ToString()
                                : example.no));
                    }
                }
            }

            int count = Mathf.Min(
                Mathf.Max(0, requestedCount),
                candidates.Count);
            SceneNumericalExampleDto[] result =
                new SceneNumericalExampleDto[count];
            for (int index = 0; index < count; index++)
                result[index] = candidates[index];
            return result;
        }

        private RectTransform CreateScrollArea(
            string objectName,
            Transform parent,
            float top,
            float bottom)
        {
            GameObject scrollObject = CreateUiObject(objectName, parent);
            RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(10f, bottom);
            scrollRect.offsetMax = new Vector2(-10f, -top);
            ExperimentUiTheme.SciFiSurface(
                scrollObject.AddComponent<Image>(),
                ContentGray);

            ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 34f;

            GameObject viewportObject = CreateUiObject("Viewport", scrollObject.transform);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            Stretch(viewport);
            viewportObject.AddComponent<Image>().color =
                new Color(1f, 1f, 1f, 0.01f);
            viewportObject.AddComponent<RectMask2D>();

            GameObject contentObject = CreateUiObject("Content", viewportObject.transform);
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layout =
                contentObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(1, 1, 1, 1);
            layout.spacing = 0f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter =
                contentObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            ExperimentUiTheme.Scrollbar(scroll);
            return content;
        }

        private Button CreateLayoutButton(
            string objectName,
            Transform parent,
            string value,
            float height,
            Color color,
            float fontSize,
            out TMP_Text label)
        {
            GameObject buttonObject = CreateUiObject(objectName, parent);
            Image image = buttonObject.AddComponent<Image>();
            image.color = color;
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            ExperimentUiTheme.StyleChoice(button);
            LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            label = CreateText(
                "Text", buttonObject.transform, value, fontSize,
                FontStyles.Normal, Color.black,
                TextAlignmentOptions.MidlineLeft);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(24f, 6f);
            label.rectTransform.offsetMax = new Vector2(-24f, -6f);
            return button;
        }

        private Button CreateButton(
            string objectName,
            Transform parent,
            string value,
            Vector2 normalizedAnchor,
            Vector2 size,
            Color color,
            out TMP_Text label)
        {
            GameObject buttonObject = CreateUiObject(objectName, parent);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = normalizedAnchor;
            rect.anchorMax = normalizedAnchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            Image image = buttonObject.AddComponent<Image>();
            image.color = color;
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            ExperimentUiTheme.StyleButton(button, color == (Color)Yellow);
            label = CreateText(
                "Text", buttonObject.transform, value, 25f,
                FontStyles.Normal, Color.black,
                TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            return button;
        }

        private TMP_InputField CreateInputField(
            string objectName,
            Transform parent,
            string value)
        {
            GameObject inputObject = CreateUiObject(objectName, parent);
            inputObject.AddComponent<Image>().color = Color.white;
            GameObject areaObject = CreateUiObject("Text Area", inputObject.transform);
            RectTransform area = areaObject.GetComponent<RectTransform>();
            Stretch(area);
            area.offsetMin = new Vector2(14f, 4f);
            area.offsetMax = new Vector2(-14f, -4f);
            areaObject.AddComponent<RectMask2D>();

            TMP_Text inputText = CreateText(
                "Text", area, value, AnswerFontSize, FontStyles.Normal,
                Color.black, TextAlignmentOptions.MidlineLeft);
            Stretch(inputText.rectTransform);
            TMP_Text placeholder = CreateText(
                "Placeholder", area, "请输入", AnswerFontSize,
                FontStyles.Italic, new Color32(110, 110, 110, 255),
                TextAlignmentOptions.MidlineLeft);
            Stretch(placeholder.rectTransform);

            TMP_InputField input = inputObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = inputText;
            input.placeholder = placeholder;
            input.text = value;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.caretColor = Color.black;
            ExperimentUiTheme.StyleInput(input);
            input.pointSize = AnswerFontSize;
            return input;
        }

        private TMP_Text CreateText(
            string objectName,
            Transform parent,
            string value,
            float size,
            FontStyles style,
            Color color,
            TextAlignmentOptions alignment)
        {
            GameObject textObject = CreateUiObject(objectName, parent);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            if (_font != null)
                text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color == Color.black ? (Color)ExperimentUiTheme.TextColor : color;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject result = new GameObject(name, typeof(RectTransform));
            result.transform.SetParent(parent, false);
            result.layer = parent.gameObject.layer;
            return result;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
                Destroy(parent.GetChild(index).gameObject);
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
