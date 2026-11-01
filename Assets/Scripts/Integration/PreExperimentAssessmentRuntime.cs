using System;
using System.Collections.Generic;
using Vtp.VR.Assessment;
using Vtp.VR.Rest;

namespace Vtp.VR.Integration
{
    [Serializable]
    public sealed class PreExperimentNumericAnswerDto
    {
        public string key;
        public string value;
        public string unit;
    }

    [Serializable]
    public sealed class PreExperimentCaseTaskTypeAnswerDto
    {
        public int rowIndex;
        public string exampleId;
        public string exampleNo;
        public string selectedOptionId;
    }

    [Serializable]
    public sealed class PreExperimentAnswerDto
    {
        public string itemId;
        public string title;
        public string[] selectedOptionIds;
        public PreExperimentNumericAnswerDto[] numericValues;
        public PreExperimentCaseTaskTypeAnswerDto[] caseTaskTypeAnswers;
    }

    /// <summary>
    /// 保存评分细则第5至12项的学生原始答案。
    /// 本类只记录，不在客户端显示得分或正确答案，便于后续统一提交服务器评分。
    /// </summary>
    public static class PreExperimentAssessmentRuntime
    {
        private static readonly List<PreExperimentAnswerDto> AnswersInternal =
            new List<PreExperimentAnswerDto>();

        private static string[] _expectedTaskTypes = new string[0];

        public static string TaskId { get; private set; }
        public static bool IsCompleted { get; private set; }

        public static PreExperimentAnswerDto[] Answers
        {
            get { return AnswersInternal.ToArray(); }
        }

        /// <summary>
        /// 第5题每条算例对应的作业类型参考答案，顺序与题目行一致。
        /// </summary>
        public static string[] ExpectedTaskTypes
        {
            get { return (string[])_expectedTaskTypes.Clone(); }
        }

        public static void Begin(
            string taskId,
            SceneNumericalExampleDto[] randomExamples)
        {
            TaskId = (taskId ?? string.Empty).Trim();
            IsCompleted = false;
            AnswersInternal.Clear();
            _expectedTaskTypes = CollectTaskTypes(randomExamples);
        }

        public static void SaveSelection(
            string itemId,
            string title,
            IEnumerable<string> selectedOptionIds)
        {
            List<string> values = new List<string>();
            if (selectedOptionIds != null)
            {
                foreach (string value in selectedOptionIds)
                {
                    if (!string.IsNullOrWhiteSpace(value) &&
                        !values.Contains(value))
                    {
                        values.Add(value.Trim());
                    }
                }
            }

            Save(new PreExperimentAnswerDto
            {
                itemId = itemId,
                title = title,
                selectedOptionIds = values.ToArray(),
                numericValues = new PreExperimentNumericAnswerDto[0],
                caseTaskTypeAnswers =
                    new PreExperimentCaseTaskTypeAnswerDto[0]
            });
        }

        public static void SaveCaseTaskTypes(
            string itemId,
            string title,
            IEnumerable<PreExperimentCaseTaskTypeAnswerDto> values)
        {
            List<PreExperimentCaseTaskTypeAnswerDto> copied =
                new List<PreExperimentCaseTaskTypeAnswerDto>();
            if (values != null)
            {
                foreach (PreExperimentCaseTaskTypeAnswerDto value in values)
                {
                    if (value == null)
                        continue;
                    copied.Add(new PreExperimentCaseTaskTypeAnswerDto
                    {
                        rowIndex = value.rowIndex,
                        exampleId = value.exampleId,
                        exampleNo = value.exampleNo,
                        selectedOptionId = value.selectedOptionId
                    });
                }
            }

            Save(new PreExperimentAnswerDto
            {
                itemId = itemId,
                title = title,
                selectedOptionIds = new string[0],
                numericValues = new PreExperimentNumericAnswerDto[0],
                caseTaskTypeAnswers = copied.ToArray()
            });
        }

        public static void SaveNumeric(
            string itemId,
            string title,
            IEnumerable<PreExperimentNumericAnswerDto> values)
        {
            List<PreExperimentNumericAnswerDto> copied =
                new List<PreExperimentNumericAnswerDto>();
            if (values != null)
            {
                foreach (PreExperimentNumericAnswerDto value in values)
                {
                    if (value == null)
                        continue;
                    copied.Add(new PreExperimentNumericAnswerDto
                    {
                        key = value.key,
                        value = value.value,
                        unit = value.unit
                    });
                }
            }

            Save(new PreExperimentAnswerDto
            {
                itemId = itemId,
                title = title,
                selectedOptionIds = new string[0],
                numericValues = copied.ToArray(),
                caseTaskTypeAnswers =
                    new PreExperimentCaseTaskTypeAnswerDto[0]
            });
        }

        public static PreExperimentAnswerDto Find(string itemId)
        {
            for (int index = 0; index < AnswersInternal.Count; index++)
            {
                if (string.Equals(
                        AnswersInternal[index].itemId,
                        itemId,
                        StringComparison.Ordinal))
                {
                    return AnswersInternal[index];
                }
            }

            return null;
        }

        public static void Complete()
        {
            IsCompleted = AnswersInternal.Count >= 8;
            if (IsCompleted)
                EvaluateAndRecord();
        }

        public static void Clear()
        {
            TaskId = null;
            IsCompleted = false;
            AnswersInternal.Clear();
            _expectedTaskTypes = new string[0];
        }

        private static void Save(PreExperimentAnswerDto answer)
        {
            for (int index = 0; index < AnswersInternal.Count; index++)
            {
                if (string.Equals(
                        AnswersInternal[index].itemId,
                        answer.itemId,
                        StringComparison.Ordinal))
                {
                    AnswersInternal[index] = answer;
                    return;
                }
            }

            AnswersInternal.Add(answer);
        }

        private static string[] CollectTaskTypes(
            SceneNumericalExampleDto[] examples)
        {
            List<string> result = new List<string>();
            if (examples == null)
                return result.ToArray();

            for (int index = 0; index < examples.Length; index++)
            {
                SceneNumericalExampleDto example = examples[index];
                string taskType;
                string optionId;
                if (example != null &&
                    SceneExampleTaskCaseAdapter
                        .TryInferTaskTypeFromPositions(
                            example,
                            out taskType) &&
                    TryGetTaskTypeOptionId(taskType, out optionId))
                {
                    result.Add(optionId);
                }
                else
                {
                    result.Add(string.Empty);
                }
            }

            return result.ToArray();
        }

        private static void EvaluateAndRecord()
        {
            RecordCaseTaskTypeScore(
                "5",
                _expectedTaskTypes);
            RecordSelectionScore(
                "6",
                new[] { "MIN_CMAX" });

            bool item7 = NumericIntegerInSet(
                "7",
                "MIN_CRANE_DISTANCE",
                new[] { 2, 3, 4 },
                "贝位");
            StudentAssessmentRuntime.RecordRuleResult(
                "7",
                item7,
                item7
                    ? "最小安全距离为2、3或4贝位。"
                    : "最小安全距离不是2、3或4中的整数。" );

            bool item8 = NumericInRange(
                             "8", "GANTRY_SPEED", 1.0, 2.0, "m/s") &&
                         NumericInRange(
                             "8", "TROLLEY_SPEED", 0.5, 0.8, "m/s");
            StudentAssessmentRuntime.RecordRuleResult(
                "8",
                item8,
                item8
                    ? "大车和小车速度均在规定范围内。"
                    : "大车或小车速度缺失或超出规定范围。" );

            bool item9 = NumericInRange(
                "9", "HOIST_SPEED", 0.2, 0.8, "m/s");
            StudentAssessmentRuntime.RecordRuleResult(
                "9",
                item9,
                item9
                    ? "起升速度在0.200至0.800 m/s范围内。"
                    : "起升速度缺失或超出规定范围。" );

            RecordSelectionScore(
                "10",
                new[] { "SEA_FIXED_SHARED", "LAND_FIXED_SHARED" });
            RecordSelectionScore(
                "11",
                new[] { "COLLISION_AVOIDANCE", "SUBTOUR_ELIMINATION" });
            RecordSelectionScore(
                "12",
                new[] { "LOAD_BALANCING" });
        }

        private static void RecordSelectionScore(
            string itemId,
            string[] expected)
        {
            PreExperimentAnswerDto answer = Find(itemId);
            bool passed = answer != null &&
                          AssessmentRules.SetEquals(
                              answer.selectedOptionIds,
                              expected);
            StudentAssessmentRuntime.RecordRuleResult(
                itemId,
                passed,
                passed
                    ? "选择结果与评分细则完全一致。"
                    : "存在错选、漏选、多选或未提交。" );
        }

        private static void RecordCaseTaskTypeScore(
            string itemId,
            string[] expected)
        {
            PreExperimentAnswerDto answer = Find(itemId);
            bool passed = answer != null &&
                          answer.caseTaskTypeAnswers != null &&
                          expected != null &&
                          answer.caseTaskTypeAnswers.Length ==
                          expected.Length &&
                          expected.Length > 0;
            if (passed)
            {
                for (int index = 0; index < expected.Length; index++)
                {
                    PreExperimentCaseTaskTypeAnswerDto row =
                        answer.caseTaskTypeAnswers[index];
                    if (row == null ||
                        row.rowIndex != index ||
                        string.IsNullOrWhiteSpace(expected[index]) ||
                        !string.Equals(
                            row.selectedOptionId,
                            expected[index],
                            StringComparison.OrdinalIgnoreCase))
                    {
                        passed = false;
                        break;
                    }
                }
            }

            StudentAssessmentRuntime.RecordRuleResult(
                itemId,
                passed,
                passed
                    ? "全部算例的作业类型均选择正确，本题得5分。"
                    : "存在算例类型错选、漏选或未提交，本题得0分。");
        }

        private static bool NumericIntegerInSet(
            string itemId,
            string key,
            int[] allowed,
            string expectedUnit)
        {
            PreExperimentNumericAnswerDto numeric;
            return TryGetNumeric(itemId, key, out numeric) &&
                   AssessmentRules.UnitEquals(
                       numeric.unit,
                       expectedUnit) &&
                   AssessmentRules.IntegerInSet(
                       numeric.value,
                       allowed);
        }

        private static bool NumericInRange(
            string itemId,
            string key,
            double minimum,
            double maximum,
            string expectedUnit)
        {
            PreExperimentNumericAnswerDto numeric;
            return TryGetNumeric(itemId, key, out numeric) &&
                   AssessmentRules.UnitEquals(
                       numeric.unit,
                       expectedUnit) &&
                   AssessmentRules.NumberInRange(
                       numeric.value,
                       minimum,
                       maximum);
        }

        private static bool TryGetNumeric(
            string itemId,
            string key,
            out PreExperimentNumericAnswerDto value)
        {
            value = null;
            PreExperimentAnswerDto answer = Find(itemId);
            if (answer == null || answer.numericValues == null)
                return false;
            for (int index = 0;
                 index < answer.numericValues.Length;
                 index++)
            {
                PreExperimentNumericAnswerDto numeric =
                    answer.numericValues[index];
                if (numeric == null ||
                    !string.Equals(
                        numeric.key,
                        key,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                value = numeric;
                return true;
            }
            return false;
        }

        private static bool TryGetTaskTypeOptionId(
            string taskType,
            out string optionId)
        {
            string value = (taskType ?? string.Empty).Trim();
            string upper = value.ToUpperInvariant();
            if (value.Contains("卸船") || upper == "DISCHARGE")
            {
                optionId = "DISCHARGE";
                return true;
            }
            if (value.Contains("装船") || upper == "LOAD_SHIP")
            {
                optionId = "LOAD_SHIP";
                return true;
            }
            if (value.Contains("翻箱") || upper == "REHANDLE")
            {
                optionId = "REHANDLE";
                return true;
            }
            if (value.Contains("卸车") || upper == "UNLOAD_TRUCK")
            {
                optionId = "UNLOAD_TRUCK";
                return true;
            }
            if (value.Contains("装车") || upper == "LOAD_TRUCK")
            {
                optionId = "LOAD_TRUCK";
                return true;
            }

            optionId = null;
            return false;
        }
    }
}
