using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vtp.VR.Rest;

namespace Vtp.VR.Integration
{
    /// <summary>
    /// 评分流程第16步：展示算法原始调度序列，收集学生核对结果并提交。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScheduleResultAssessmentController : MonoBehaviour
    {
        private const string FontResourcePath =
            "Fonts/AlibabaPuHuiTi-3-55-Regular SDF";

        private sealed class ResultMetrics
        {
            public double Cmax;
            public int Rmg1Count;
            public int Rmg2Count;
            public int SharedCount;
            public bool Complete;
            public string IntegrityDetail;
            public string Fingerprint;
        }

        private sealed class CraneMotionSegment
        {
            public double StartTime;
            public double EndTime;
            public double StartBay;
            public double EndBay;
        }

        private static readonly Color32 Yellow =
            ExperimentUiTheme.Primary;
        private static readonly Color32 CardGray =
            ExperimentUiTheme.Panel;
        private static readonly Color32 RowGray =
            ExperimentUiTheme.PanelRaised;
        private static readonly Color32 AltGray =
            ExperimentUiTheme.Alternate;

        private CanvasGroup _group;
        private RectTransform _scheduleContent;
        private TMP_Text _summaryText;
        private TMP_Text _errorText;
        private TMP_InputField _cmaxInput;
        private TMP_InputField _rmg1Input;
        private TMP_InputField _rmg2Input;
        private TMP_InputField _sharedInput;
        private Toggle _completeToggle;
        private TMP_FontAsset _font;
        private ResultMetrics _metrics;
        private SchedulingResultDto _openedSchedule;
        private Action _onSubmitted;
        private bool _submitted;

        public void Initialize()
        {
            if (_group != null)
                return;
            BuildInterface();
            Hide();
        }

        public void Open(
            TaskCaseDocumentDto input,
            SchedulingResultDto schedule,
            Action onSubmitted)
        {
            if (_group == null)
                Initialize();

            _onSubmitted = onSubmitted;
            _submitted = false;
            _openedSchedule = schedule;
            _metrics = BuildMetrics(input, schedule);
            BuildScheduleRows(schedule);
            ClearInputs();

            _summaryText.text =
                "请根据下方算法原始序列读取并填写结果：" +
                "Cmax、RMG1任务数、RMG2任务数、共享区任务数，" +
                "并判断方案是否完整。不得修改任务分配或顺序。";
            _errorText.text = string.Empty;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _group.alpha = 1f;
            _group.interactable = true;
            _group.blocksRaycasts = true;
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

        private void Submit()
        {
            if (_submitted || _group == null || !_group.interactable) return;
            double cmax;
            int rmg1;
            int rmg2;
            int shared;
            if (!TryParseDouble(_cmaxInput.text, out cmax) ||
                !int.TryParse(_rmg1Input.text.Trim(), out rmg1) ||
                !int.TryParse(_rmg2Input.text.Trim(), out rmg2) ||
                !int.TryParse(_sharedInput.text.Trim(), out shared))
            {
                _errorText.text =
                    "请完整填写四项数值；任务数量必须为整数。";
                return;
            }

            string currentFingerprint =
                ComputeScheduleFingerprint(_openedSchedule);
            bool originalScheduleUnchanged = string.Equals(
                currentFingerprint,
                _metrics.Fingerprint,
                StringComparison.Ordinal);
            bool passed =
                Math.Abs(cmax - _metrics.Cmax) <= 0.01d &&
                rmg1 == _metrics.Rmg1Count &&
                rmg2 == _metrics.Rmg2Count &&
                shared == _metrics.SharedCount &&
                _metrics.Complete &&
                originalScheduleUnchanged &&
                _completeToggle.isOn;

            StudentAssessmentRuntime.RecordScheduleSubmission(
                passed,
                cmax,
                rmg1,
                rmg2,
                shared,
                currentFingerprint,
                _metrics.IntegrityDetail +
                (originalScheduleUnchanged
                    ? "算法原始任务分配和顺序未发生变化。"
                    : "算法原始任务分配或顺序已发生变化。"));
            // 此时实验后八题尚未作答，只记录本项；最终成绩由八题提交时统一保存。
            _submitted = true;

            Hide();
            Action callback = _onSubmitted;
            _onSubmitted = null;
            if (callback != null)
                callback();
        }

        private ResultMetrics BuildMetrics(
            TaskCaseDocumentDto input,
            SchedulingResultDto schedule)
        {
            ResultMetrics metrics = new ResultMetrics();
            ScheduledOperationDto[] operations =
                schedule == null || schedule.operations == null
                    ? new ScheduledOperationDto[0]
                    : (ScheduledOperationDto[])schedule.operations.Clone();

            metrics.Cmax =
                schedule != null && schedule.metrics != null
                    ? schedule.metrics.makespanSeconds
                    : 0d;
            bool complete = operations.Length > 0;
            HashSet<string> jobs = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            HashSet<int> sequences = new HashSet<int>();
            Dictionary<string, List<ScheduledOperationDto>> byCrane =
                new Dictionary<string, List<ScheduledOperationDto>>(
                    StringComparer.OrdinalIgnoreCase);
            HashSet<string> expectedJobs = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            if (input != null && input.jobs != null)
            {
                for (int index = 0; index < input.jobs.Length; index++)
                {
                    SchedulingJobInputDto job = input.jobs[index];
                    if (job == null ||
                        string.IsNullOrWhiteSpace(job.jobId) ||
                        !expectedJobs.Add(job.jobId))
                    {
                        complete = false;
                    }
                }
            }

            for (int index = 0; index < operations.Length; index++)
            {
                ScheduledOperationDto operation = operations[index];
                if (operation == null)
                {
                    complete = false;
                    continue;
                }

                metrics.Cmax = Math.Max(
                    metrics.Cmax,
                    operation.plannedEndSeconds);
                if (!jobs.Add(operation.jobId ?? string.Empty) ||
                    operation.sequence <= 0 ||
                    !sequences.Add(operation.sequence))
                {
                    complete = false;
                }

                if (IsRmg(operation.craneId, 1))
                    metrics.Rmg1Count++;
                else if (IsRmg(operation.craneId, 2))
                    metrics.Rmg2Count++;

                if (IsShared(operation))
                    metrics.SharedCount++;

                string crane = operation.craneId ?? string.Empty;
                List<ScheduledOperationDto> list;
                if (!byCrane.TryGetValue(crane, out list))
                {
                    list = new List<ScheduledOperationDto>();
                    byCrane.Add(crane, list);
                }
                list.Add(operation);
            }

            if (input != null && input.jobs != null &&
                input.jobs.Length != operations.Length)
            {
                complete = false;
            }
            if (expectedJobs.Count > 0 &&
                !expectedJobs.SetEquals(jobs))
            {
                complete = false;
            }
            for (int expected = 1;
                 expected <= operations.Length;
                 expected++)
            {
                if (!sequences.Contains(expected))
                    complete = false;
            }

            foreach (KeyValuePair<string, List<ScheduledOperationDto>> pair
                     in byCrane)
            {
                pair.Value.Sort(
                    delegate(
                        ScheduledOperationDto left,
                        ScheduledOperationDto right)
                    {
                        return left.plannedStartSeconds.CompareTo(
                            right.plannedStartSeconds);
                    });
                for (int index = 1; index < pair.Value.Count; index++)
                {
                    if (pair.Value[index].plannedStartSeconds <
                        pair.Value[index - 1].plannedEndSeconds)
                    {
                        complete = false;
                    }
                }
            }

            string craneSafetyDetail;
            if (!ValidateCraneSafety(
                    input,
                    operations,
                    metrics.Cmax,
                    out craneSafetyDetail))
            {
                complete = false;
            }

            metrics.Complete = complete;
            metrics.Fingerprint = ComputeScheduleFingerprint(schedule);
            metrics.IntegrityDetail = complete
                ? "系统校验：任务无遗漏、无重复，设备时间段无重叠，" +
                  craneSafetyDetail
                : "系统校验：序列存在遗漏、重复、设备时间冲突或" +
                  craneSafetyDetail;
            return metrics;
        }

        private static bool ValidateCraneSafety(
            TaskCaseDocumentDto input,
            ScheduledOperationDto[] operations,
            double cmax,
            out string detail)
        {
            if (input == null ||
                input.cranes == null ||
                input.constraints == null)
            {
                detail = "缺少轨道吊或安全距离约束。";
                return false;
            }

            double minimumDistance =
                Math.Max(0d, input.constraints.minimumCraneDistance);
            List<CraneMotionSegment> rmg1 = BuildTrajectory(
                input,
                operations,
                1,
                Math.Max(1d, cmax));
            List<CraneMotionSegment> rmg2 = BuildTrajectory(
                input,
                operations,
                2,
                Math.Max(1d, cmax));
            if (rmg1.Count == 0 || rmg2.Count == 0)
            {
                detail = "无法建立两台RMG的完整运动轨迹。";
                return false;
            }

            double initial1 = PositionAt(rmg1[0], 0d);
            double initial2 = PositionAt(rmg2[0], 0d);
            double direction = initial2 >= initial1 ? 1d : -1d;
            const double tolerance = 0.000001d;

            for (int leftIndex = 0;
                 leftIndex < rmg1.Count;
                 leftIndex++)
            {
                CraneMotionSegment left = rmg1[leftIndex];
                for (int rightIndex = 0;
                     rightIndex < rmg2.Count;
                     rightIndex++)
                {
                    CraneMotionSegment right = rmg2[rightIndex];
                    double start = Math.Max(
                        left.StartTime,
                        right.StartTime);
                    double end = Math.Min(
                        left.EndTime,
                        right.EndTime);
                    if (end < start - tolerance)
                        continue;

                    double startGap = direction *
                        (PositionAt(right, start) -
                         PositionAt(left, start));
                    double endGap = direction *
                        (PositionAt(right, end) -
                         PositionAt(left, end));
                    if (Math.Min(startGap, endGap) + tolerance <
                        minimumDistance)
                    {
                        detail =
                            "两台RMG在" +
                            start.ToString(
                                "0.##",
                                CultureInfo.InvariantCulture) +
                            "至" +
                            end.ToString(
                                "0.##",
                                CultureInfo.InvariantCulture) +
                            "秒之间小于最小安全距离或发生交叉。";
                        return false;
                    }
                }
            }

            detail = "两台RMG全时段保持不少于" +
                     minimumDistance.ToString(
                         "0.##",
                         CultureInfo.InvariantCulture) +
                     "贝位的安全距离。";
            return true;
        }

        private static List<CraneMotionSegment> BuildTrajectory(
            TaskCaseDocumentDto input,
            ScheduledOperationDto[] operations,
            int rmgNumber,
            double horizon)
        {
            List<ScheduledOperationDto> selected =
                new List<ScheduledOperationDto>();
            if (operations != null)
            {
                for (int index = 0; index < operations.Length; index++)
                {
                    ScheduledOperationDto operation = operations[index];
                    if (operation != null &&
                        IsRmg(operation.craneId, rmgNumber))
                    {
                        selected.Add(operation);
                    }
                }
            }
            selected.Sort(
                delegate(
                    ScheduledOperationDto left,
                    ScheduledOperationDto right)
                {
                    return left.plannedStartSeconds.CompareTo(
                        right.plannedStartSeconds);
                });

            double initialBay;
            if (!TryGetInitialBay(input, rmgNumber, out initialBay))
            {
                if (selected.Count == 0)
                    return new List<CraneMotionSegment>();
                initialBay = selected[0].sourceBay;
            }

            List<CraneMotionSegment> result =
                new List<CraneMotionSegment>();
            double cursorTime = 0d;
            double cursorBay = initialBay;
            for (int index = 0; index < selected.Count; index++)
            {
                ScheduledOperationDto operation = selected[index];
                double start = Math.Max(
                    cursorTime,
                    operation.plannedStartSeconds);
                double end = Math.Max(
                    start,
                    operation.plannedEndSeconds);
                double sourceBay = operation.sourceBay;
                double targetBay = operation.targetBay;

                if (start > cursorTime)
                {
                    result.Add(new CraneMotionSegment
                    {
                        StartTime = cursorTime,
                        EndTime = start,
                        StartBay = cursorBay,
                        EndBay = sourceBay
                    });
                }

                if (end > start)
                {
                    result.Add(new CraneMotionSegment
                    {
                        StartTime = start,
                        EndTime = end,
                        StartBay = sourceBay,
                        EndBay = targetBay
                    });
                }
                cursorTime = end;
                cursorBay = targetBay;
            }

            if (cursorTime < horizon)
            {
                result.Add(new CraneMotionSegment
                {
                    StartTime = cursorTime,
                    EndTime = horizon,
                    StartBay = cursorBay,
                    EndBay = cursorBay
                });
            }
            else if (result.Count == 0)
            {
                result.Add(new CraneMotionSegment
                {
                    StartTime = 0d,
                    EndTime = horizon,
                    StartBay = cursorBay,
                    EndBay = cursorBay
                });
            }

            return result;
        }

        private static bool TryGetInitialBay(
            TaskCaseDocumentDto input,
            int rmgNumber,
            out double bay)
        {
            bay = 0d;
            if (input == null || input.cranes == null)
                return false;

            for (int index = 0; index < input.cranes.Length; index++)
            {
                CraneInputDto crane = input.cranes[index];
                if (crane != null && IsRmg(crane.craneId, rmgNumber))
                {
                    bay = crane.initialBay;
                    return true;
                }
            }
            return false;
        }

        private static double PositionAt(
            CraneMotionSegment segment,
            double time)
        {
            double duration = segment.EndTime - segment.StartTime;
            if (duration <= 0.000001d)
                return segment.EndBay;
            double ratio = Math.Max(
                0d,
                Math.Min(1d, (time - segment.StartTime) / duration));
            return segment.StartBay +
                   (segment.EndBay - segment.StartBay) * ratio;
        }

        private static string ComputeScheduleFingerprint(
            SchedulingResultDto schedule)
        {
            StringBuilder builder = new StringBuilder();
            AppendField(builder, schedule == null ? null : schedule.scheduleId);
            ScheduledOperationDto[] operations =
                schedule == null || schedule.operations == null
                    ? new ScheduledOperationDto[0]
                    : schedule.operations;
            for (int index = 0; index < operations.Length; index++)
            {
                ScheduledOperationDto operation = operations[index];
                if (operation == null)
                {
                    AppendField(builder, "<null>");
                    continue;
                }
                AppendField(builder, operation.sequence.ToString());
                AppendField(builder, operation.jobId);
                AppendField(builder, operation.containerNo);
                AppendField(builder, operation.craneId);
                AppendField(builder, operation.quayCraneId.ToString());
                AppendField(builder, operation.sourceVessel);
                AppendField(builder, operation.sourceShipBay.ToString());
                AppendField(builder, operation.sourceShipColumn.ToString());
                AppendField(builder, operation.sourceShipTier.ToString());
                AppendField(builder, operation.fromCode);
                AppendField(builder, operation.toCode);
                AppendField(
                    builder,
                    operation.plannedStartSeconds.ToString(
                        "R",
                        CultureInfo.InvariantCulture));
                AppendField(
                    builder,
                    operation.plannedEndSeconds.ToString(
                        "R",
                        CultureInfo.InvariantCulture));
                AppendField(builder, operation.taskType);
                AppendField(builder, operation.sourceAreaType);
                AppendField(builder, operation.sourceAreaId);
                AppendField(
                    builder,
                    operation.sourceBay.ToString(
                        "R",
                        CultureInfo.InvariantCulture));
                AppendField(
                    builder,
                    operation.sourceRow.ToString(
                        "R",
                        CultureInfo.InvariantCulture));
                AppendField(builder, operation.sourceTier.ToString());
                AppendField(builder, operation.targetAreaType);
                AppendField(builder, operation.targetAreaId);
                AppendField(
                    builder,
                    operation.targetBay.ToString(
                        "R",
                        CultureInfo.InvariantCulture));
                AppendField(
                    builder,
                    operation.targetRow.ToString(
                        "R",
                        CultureInfo.InvariantCulture));
                AppendField(builder, operation.targetTier.ToString());
            }

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(
                    Encoding.UTF8.GetBytes(builder.ToString()));
                return BitConverter.ToString(hash)
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        private static void AppendField(
            StringBuilder builder,
            string value)
        {
            string normalized = value ?? string.Empty;
            builder.Append(normalized.Length);
            builder.Append(':');
            builder.Append(normalized);
            builder.Append('|');
        }

        private static bool IsRmg(string craneId, int number)
        {
            string value = (craneId ?? string.Empty)
                .Replace(" ", string.Empty)
                .ToUpperInvariant();
            return value == "RMG" + number ||
                   value.EndsWith("-RMG" + number) ||
                   value.EndsWith("RMG" + number);
        }

        private static bool IsShared(ScheduledOperationDto operation)
        {
            return ContainsShared(operation.sourceAreaType) ||
                   ContainsShared(operation.sourceAreaId) ||
                   ContainsShared(operation.targetAreaType) ||
                   ContainsShared(operation.targetAreaId) ||
                   ContainsShared(operation.fromCode) ||
                   ContainsShared(operation.toCode);
        }

        private static bool ContainsShared(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   (value.IndexOf(
                        "SHARED",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("共享", StringComparison.Ordinal) >= 0);
        }

        private void BuildScheduleRows(SchedulingResultDto schedule)
        {
            ClearChildren(_scheduleContent);
            AddScheduleRow(
                new[] { "序号", "RMG", "起点", "目标位", "开始(s)", "结束(s)" },
                true,
                0);

            ScheduledOperationDto[] operations =
                schedule == null || schedule.operations == null
                    ? new ScheduledOperationDto[0]
                    : (ScheduledOperationDto[])schedule.operations.Clone();
            Array.Sort(
                operations,
                delegate(
                    ScheduledOperationDto left,
                    ScheduledOperationDto right)
                {
                    if (left == null) return 1;
                    if (right == null) return -1;
                    return left.sequence.CompareTo(right.sequence);
                });

            for (int index = 0; index < operations.Length; index++)
            {
                ScheduledOperationDto operation = operations[index];
                if (operation == null)
                    continue;
                string[] line = {
                    operation.sequence.ToString(CultureInfo.InvariantCulture),
                    operation.craneId, operation.fromCode, operation.toCode,
                    operation.plannedStartSeconds.ToString("0.##", CultureInfo.InvariantCulture),
                    operation.plannedEndSeconds.ToString("0.##", CultureInfo.InvariantCulture)
                };
                AddScheduleRow(line, false, index);
            }
        }

        private void AddScheduleRow(
            string[] values,
            bool header,
            int index)
        {
            GameObject row = CreateUiObject(
                header ? "ScheduleHeader" : "ScheduleRow_" + index,
                _scheduleContent);
            row.AddComponent<Image>().color =
                header ? ExperimentUiTheme.TableHeader : (index % 2 == 0 ? RowGray : AltGray);
            ExperimentUiTheme.Divider(row.transform);
            LayoutElement layout = row.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 60f;
            HorizontalLayoutGroup columns = row.AddComponent<HorizontalLayoutGroup>();
            columns.padding = new RectOffset(12, 12, 4, 4);
            columns.spacing = 16f;
            columns.childControlHeight = columns.childControlWidth = true;
            float[] widths = { 0.5f, 0.85f, 1.3f, 1.3f, 0.8f, 0.8f };
            for (int column = 0; column < values.Length; column++)
            {
                TMP_Text label = CreateText("Column" + column, row.transform, values[column],
                    20f, header ? FontStyles.Bold : FontStyles.Normal,
                    column >= 4 ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft);
                label.gameObject.AddComponent<LayoutElement>().flexibleWidth = widths[column];
            }
        }

        private void BuildInterface()
        {
            _font = ExperimentUiTheme.Font;
            _group = GetComponent<CanvasGroup>();
            if (_group == null)
                _group = gameObject.AddComponent<CanvasGroup>();
            Image blocker = GetComponent<Image>();
            if (blocker == null)
                blocker = gameObject.AddComponent<Image>();
            blocker.color = ExperimentUiTheme.Page;
            blocker.raycastTarget = true;
            Stretch(GetComponent<RectTransform>());

            RectTransform card = ExperimentUiTheme.DesignSurface(transform, "ScheduleAssessmentCard");
            ExperimentUiTheme.HeaderBar(card, "调度结果核对与提交");

            TMP_Text title = CreateText(
                "TitleText", card, "读取调度序列并完成核对", 36f,
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            SetTopStretch(title.rectTransform, 138f, 72f, 72f, 64f);

            _summaryText = CreateText(
                "SummaryText", card, string.Empty, 21f,
                FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            _summaryText.color = ExperimentUiTheme.Muted;
            SetTopStretch(
                _summaryText.rectTransform, 214f, 72f, 72f, 80f);

            _scheduleContent = CreateScrollArea(
                card, new Vector2(0.0375f, 0.19f),
                new Vector2(0.63f, 0.70f));

            RectTransform form = CreateUiObject(
                "VerificationForm", card).GetComponent<RectTransform>();
            form.anchorMin = new Vector2(0.665f, 0.19f);
            form.anchorMax = new Vector2(0.9625f, 0.70f);
            form.offsetMin = Vector2.zero;
            form.offsetMax = Vector2.zero;
            ExperimentUiTheme.SciFiSurface(
                form.gameObject.AddComponent<Image>(),
                new Color(1f, 1f, 1f, 0.82f));
            VerticalLayoutGroup formLayout =
                form.gameObject.AddComponent<VerticalLayoutGroup>();
            formLayout.padding = new RectOffset(12, 12, 12, 12);
            formLayout.spacing = 24f;
            formLayout.childControlHeight = true;
            formLayout.childControlWidth = true;
            formLayout.childForceExpandHeight = false;

            _cmaxInput = AddInputRow(form, "Cmax（秒）", false);
            _rmg1Input = AddInputRow(form, "RMG1任务数", true);
            _rmg2Input = AddInputRow(form, "RMG2任务数", true);
            _sharedInput = AddInputRow(form, "共享区任务数", true);
            _completeToggle = AddToggle(
                form,
                "方案无任务遗漏、无重复且无设备时间冲突");

            _errorText = CreateText(
                "ErrorText", card, string.Empty, 20f,
                FontStyles.Normal, TextAlignmentOptions.Center);
            _errorText.color = ExperimentUiTheme.Error;
            SetBottomStretch(_errorText.rectTransform, 142f, 72f, 72f, 40f);
            RectTransform footer = ExperimentUiTheme.FooterBar(card);
            TMP_Text hint = ExperimentUiTheme.Text("FooterHint", footer,
                "提交核对后开始三维调度，总成绩在实验后答题完成时统一保存。", 22f, ExperimentUiTheme.Muted);
            SetTopStretch(hint.rectTransform, 34f, 72f, 740f, 64f);

            Button submit = CreateButton(
                "SubmitButton",
                footer,
                "提交核对并开始三维调度",
                new Vector2(0.5f, 0f),
                new Vector2(430f, 60f));
            ExperimentUiTheme.FooterButton(submit.GetComponent<RectTransform>(), 72f, 470f);
            submit.onClick.AddListener(Submit);
        }

        private RectTransform CreateScrollArea(
            RectTransform parent,
            Vector2 min,
            Vector2 max)
        {
            GameObject root = CreateUiObject(
                "ScheduleScrollView", parent);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            ExperimentUiTheme.SciFiSurface(
                root.AddComponent<Image>(),
                Color.white);
            ScrollRect scroll = root.AddComponent<ScrollRect>();
            scroll.horizontal = false;

            RectTransform viewport = CreateUiObject(
                "Viewport", rect).GetComponent<RectTransform>();
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = CreateUiObject(
                "Content", viewport).GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout =
                content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(1, 1, 1, 1);
            layout.spacing = 0f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>()
                .verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            ExperimentUiTheme.Scrollbar(scroll);
            return content;
        }

        private TMP_InputField AddInputRow(
            Transform parent,
            string labelValue,
            bool integer)
        {
            GameObject row = CreateUiObject(
                labelValue, parent);
            ExperimentUiTheme.SciFiSurface(
                row.AddComponent<Image>(),
                RowGray);
            LayoutElement layout = row.AddComponent<LayoutElement>();
            layout.minHeight = 80f;
            layout.preferredHeight = 80f;

            TMP_Text label = CreateText(
                "Label", row.transform, labelValue, 20f,
                FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = new Vector2(0.52f, 1f);
            label.rectTransform.offsetMin = new Vector2(12f, 3f);
            label.rectTransform.offsetMax = new Vector2(-6f, -3f);

            TMP_InputField input = CreateInputField(
                "Input", row.transform, integer);
            RectTransform inputRect =
                input.GetComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0.53f, 0.5f);
            inputRect.anchorMax = new Vector2(0.98f, 0.5f);
            inputRect.offsetMin = new Vector2(0f, -24f);
            inputRect.offsetMax = new Vector2(0f, 24f);
            return input;
        }

        private Toggle AddToggle(Transform parent, string labelValue)
        {
            GameObject row = CreateUiObject("Completeness", parent);
            ExperimentUiTheme.SciFiSurface(
                row.AddComponent<Image>(),
                RowGray);
            LayoutElement layout = row.AddComponent<LayoutElement>();
            layout.minHeight = 72f;
            layout.preferredHeight = 72f;

            GameObject box = CreateUiObject("Box", row.transform);
            RectTransform boxRect = box.GetComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0f, 0.5f);
            boxRect.anchorMax = new Vector2(0f, 0.5f);
            boxRect.anchoredPosition = new Vector2(28f, 0f);
            boxRect.sizeDelta = new Vector2(32f, 32f);
            Image boxImage = box.AddComponent<Image>();
            ExperimentUiTheme.Surface(boxImage, ExperimentUiTheme.PanelInput);

            GameObject check = CreateUiObject("Checkmark", box.transform);
            Stretch(check.GetComponent<RectTransform>());
            Image checkImage = check.AddComponent<Image>();
            checkImage.color = ExperimentUiTheme.TextColor;
            checkImage.sprite = ExperimentUiTheme.BuiltinSprite("Checkmark");

            Toggle toggle = row.AddComponent<Toggle>();
            toggle.targetGraphic = boxImage;
            toggle.graphic = checkImage;
            toggle.isOn = false;

            TMP_Text label = CreateText(
                "Label", row.transform, labelValue, 18f,
                FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(54f, 4f);
            label.rectTransform.offsetMax = new Vector2(-8f, -4f);
            return toggle;
        }

        private TMP_InputField CreateInputField(
            string name,
            Transform parent,
            bool integer)
        {
            GameObject obj = CreateUiObject(name, parent);
            Image image = obj.AddComponent<Image>();
            image.color = Color.white;

            RectTransform viewport = CreateUiObject(
                "Text Area", obj.transform).GetComponent<RectTransform>();
            Stretch(viewport);
            viewport.offsetMin = new Vector2(8f, 2f);
            viewport.offsetMax = new Vector2(-8f, -2f);
            viewport.gameObject.AddComponent<RectMask2D>();

            TMP_Text text = CreateText(
                "Text", viewport, string.Empty, 20f,
                FontStyles.Normal, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            text.margin = new Vector4(8f, 3f, 8f, 3f);

            TMP_Text placeholder = CreateText(
                "Placeholder", viewport, "请输入", 18f,
                FontStyles.Normal, TextAlignmentOptions.Center);
            placeholder.color = new Color(0.4f, 0.4f, 0.4f);
            Stretch(placeholder.rectTransform);

            TMP_InputField input = obj.AddComponent<TMP_InputField>();
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = integer
                ? TMP_InputField.ContentType.IntegerNumber
                : TMP_InputField.ContentType.DecimalNumber;
            input.caretColor = Color.black;
            ExperimentUiTheme.StyleInput(input);
            return input;
        }

        private Button CreateButton(
            string name,
            Transform parent,
            string value,
            Vector2 anchor,
            Vector2 size)
        {
            GameObject obj = CreateUiObject(name, parent);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 18f);
            rect.sizeDelta = size;
            Image image = obj.AddComponent<Image>();
            image.color = Yellow;
            Button button = obj.AddComponent<Button>();
            button.targetGraphic = image;
            ExperimentUiTheme.StyleButton(button, true);
            TMP_Text label = CreateText(
                "Label", obj.transform, value, 22f,
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
            text.raycastTarget = false;
            if (_font != null)
                text.font = _font;
            return text;
        }

        private void ClearInputs()
        {
            _cmaxInput.text = string.Empty;
            _rmg1Input.text = string.Empty;
            _rmg2Input.text = string.Empty;
            _sharedInput.text = string.Empty;
            _completeToggle.isOn = false;
        }

        private static bool TryParseDouble(string text, out double value)
        {
            text = (text ?? string.Empty).Trim();
            return double.TryParse(
                       text,
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out value) ||
                   double.TryParse(
                       text,
                       NumberStyles.Float,
                       CultureInfo.CurrentCulture,
                       out value);
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

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
                return;
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
