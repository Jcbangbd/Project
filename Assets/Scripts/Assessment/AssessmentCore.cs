using System;
using System.Collections.Generic;
using System.Globalization;

namespace Vtp.VR.Assessment
{
    [Serializable]
    public sealed class AssessmentItemDefinition
    {
        public string itemId;
        public string title;
        public int maxScore;

        public AssessmentItemDefinition(
            string itemId,
            string title,
            int maxScore)
        {
            this.itemId = itemId;
            this.title = title;
            this.maxScore = Math.Max(0, maxScore);
        }
    }

    [Serializable]
    public sealed class AssessmentItemResultDto
    {
        public string itemId;
        public string title;
        public int maxScore;
        public int score;
        public bool passed;
        public string detail;
    }

    [Serializable]
    public sealed class AssessmentScoreSnapshotDto
    {
        public string profileId;
        public string experimentId;
        public int profileVersion;
        public int totalScore;
        public int maximumScore;
        public AssessmentItemResultDto[] items;
    }

    /// <summary>
    /// 一套实验的评分项配置。其他实验复用评分机制时只需创建新的配置，
    /// 页面和业务代码继续通过AssessmentSession记录每项判定结果。
    /// </summary>
    public sealed class AssessmentProfile
    {
        public readonly string ProfileId;
        public readonly string ExperimentId;
        public readonly int Version;
        public readonly AssessmentItemDefinition[] Items;

        public AssessmentProfile(
            string profileId,
            string experimentId,
            int version,
            AssessmentItemDefinition[] items)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                throw new ArgumentException("profileId不能为空。", "profileId");
            if (items == null || items.Length == 0)
                throw new ArgumentException("评分配置必须包含评分项。", "items");

            ProfileId = profileId.Trim();
            ExperimentId = (experimentId ?? string.Empty).Trim();
            Version = Math.Max(1, version);
            Items = (AssessmentItemDefinition[])items.Clone();
        }
    }

    /// <summary>
    /// 与具体实验页面无关的评分会话。统一处理评分项校验、覆盖记录、
    /// 未作答项补零、总分计算和稳定的评分项输出顺序。
    /// </summary>
    public sealed class AssessmentSession
    {
        private readonly AssessmentProfile _profile;
        private readonly Dictionary<string, AssessmentItemDefinition>
            _definitions;
        private readonly Dictionary<string, AssessmentItemResultDto>
            _results;

        public AssessmentSession(AssessmentProfile profile)
        {
            if (profile == null)
                throw new ArgumentNullException("profile");

            _profile = profile;
            _definitions =
                new Dictionary<string, AssessmentItemDefinition>(
                    StringComparer.Ordinal);
            _results = new Dictionary<string, AssessmentItemResultDto>(
                StringComparer.Ordinal);

            for (int index = 0; index < profile.Items.Length; index++)
            {
                AssessmentItemDefinition definition = profile.Items[index];
                if (definition == null ||
                    string.IsNullOrWhiteSpace(definition.itemId))
                {
                    throw new ArgumentException(
                        "评分配置包含空评分项或空itemId。",
                        "profile");
                }
                if (_definitions.ContainsKey(definition.itemId))
                {
                    throw new ArgumentException(
                        "评分配置包含重复itemId：" + definition.itemId,
                        "profile");
                }
                _definitions.Add(definition.itemId, definition);
            }
        }

        public AssessmentProfile Profile
        {
            get { return _profile; }
        }

        public void Reset()
        {
            _results.Clear();
        }

        public bool TryGetDefinition(
            string itemId,
            out AssessmentItemDefinition definition)
        {
            return _definitions.TryGetValue(
                itemId ?? string.Empty,
                out definition);
        }

        public bool TryGetResult(
            string itemId,
            out AssessmentItemResultDto result)
        {
            AssessmentItemResultDto stored;
            if (!_results.TryGetValue(itemId ?? string.Empty, out stored))
            {
                result = null;
                return false;
            }

            result = Clone(stored);
            return true;
        }

        public void Record(
            string itemId,
            int score,
            bool passed,
            string detail)
        {
            AssessmentItemDefinition definition;
            if (!TryGetDefinition(itemId, out definition))
            {
                throw new ArgumentException(
                    "评分配置中不存在itemId：" + itemId,
                    "itemId");
            }

            int normalizedScore = Math.Max(
                0,
                Math.Min(definition.maxScore, score));
            _results[definition.itemId] = new AssessmentItemResultDto
            {
                itemId = definition.itemId,
                title = definition.title,
                maxScore = definition.maxScore,
                score = normalizedScore,
                passed = passed,
                detail = detail ?? string.Empty
            };
        }

        public void RecordPass(
            string itemId,
            bool passed,
            string detail)
        {
            AssessmentItemDefinition definition;
            if (!TryGetDefinition(itemId, out definition))
            {
                throw new ArgumentException(
                    "评分配置中不存在itemId：" + itemId,
                    "itemId");
            }
            Record(
                itemId,
                passed ? definition.maxScore : 0,
                passed,
                detail);
        }

        public AssessmentScoreSnapshotDto BuildSnapshot()
        {
            List<AssessmentItemResultDto> ordered =
                new List<AssessmentItemResultDto>(_profile.Items.Length);
            int total = 0;
            int maximum = 0;

            for (int index = 0; index < _profile.Items.Length; index++)
            {
                AssessmentItemDefinition definition = _profile.Items[index];
                AssessmentItemResultDto stored;
                AssessmentItemResultDto result;
                if (_results.TryGetValue(definition.itemId, out stored))
                {
                    result = Clone(stored);
                }
                else
                {
                    result = new AssessmentItemResultDto
                    {
                        itemId = definition.itemId,
                        title = definition.title,
                        maxScore = definition.maxScore,
                        score = 0,
                        passed = false,
                        detail = "未记录该项操作。"
                    };
                }

                ordered.Add(result);
                total += result.score;
                maximum += result.maxScore;
            }

            return new AssessmentScoreSnapshotDto
            {
                profileId = _profile.ProfileId,
                experimentId = _profile.ExperimentId,
                profileVersion = _profile.Version,
                totalScore = total,
                maximumScore = maximum,
                items = ordered.ToArray()
            };
        }

        private static AssessmentItemResultDto Clone(
            AssessmentItemResultDto source)
        {
            return new AssessmentItemResultDto
            {
                itemId = source.itemId,
                title = source.title,
                maxScore = source.maxScore,
                score = source.score,
                passed = source.passed,
                detail = source.detail
            };
        }
    }

    /// <summary>
    /// 可由不同实验共同使用的全对全得分判定函数。
    /// </summary>
    public static class AssessmentRules
    {
        public static bool SetEquals(
            IEnumerable<string> actual,
            IEnumerable<string> expected)
        {
            HashSet<string> actualSet = new HashSet<string>(
                Normalize(actual),
                StringComparer.OrdinalIgnoreCase);
            HashSet<string> expectedSet = new HashSet<string>(
                Normalize(expected),
                StringComparer.OrdinalIgnoreCase);
            return actualSet.SetEquals(expectedSet);
        }

        public static bool TryParseNumber(string text, out double value)
        {
            string normalized = (text ?? string.Empty).Trim();
            return double.TryParse(
                       normalized,
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out value) ||
                   double.TryParse(
                       normalized,
                       NumberStyles.Float,
                       CultureInfo.CurrentCulture,
                       out value);
        }

        public static bool NumberInRange(
            string text,
            double minimum,
            double maximum)
        {
            double value;
            return TryParseNumber(text, out value) &&
                   value >= minimum &&
                   value <= maximum;
        }

        public static bool IntegerInSet(
            string text,
            IEnumerable<int> allowed)
        {
            double value;
            if (!TryParseNumber(text, out value) ||
                Math.Abs(value - Math.Round(value)) > 0.000001d)
            {
                return false;
            }

            int integer = Convert.ToInt32(Math.Round(value));
            if (allowed == null)
                return false;
            foreach (int candidate in allowed)
            {
                if (candidate == integer)
                    return true;
            }
            return false;
        }

        public static bool UnitEquals(string actual, string expected)
        {
            return string.Equals(
                (actual ?? string.Empty).Trim(),
                (expected ?? string.Empty).Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> Normalize(
            IEnumerable<string> source)
        {
            if (source == null)
                yield break;

            foreach (string value in source)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    yield return value.Trim();
            }
        }
    }
}

