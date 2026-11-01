using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Vtp.VR.Rest;

namespace Vtp.VR.LocalAlgorithmExe
{
    [Serializable]
    public sealed class RmgExeRunResponse
    {
        public bool success;
        public string engine_version;
        public string algorithm;
        public string algorithm_name;
        public string instance_source;
        public string fallback_reason;
        public string output_file;
        public string output_json_file;
        public string message;
        public string finished_at;
    }

    public sealed class RmgExeExecutionResult
    {
        public RmgExeRunResponse Response;
        public TaskCaseDocumentDto Input;
        public SchedulingResultDto Schedule;
        public SceneNumericalExampleDto[] Examples;
        public bool InputWasLocalFallback;
        public string SourceMessage;
        public JObject Parameters;

        public bool UsedLocalFallback
        {
            get { return InputWasLocalFallback; }
        }
    }

    public enum RmgExeParameterValueType
    {
        Integer,
        Decimal
    }

    /// <summary>
    /// 算法EXE可输入参数的公开定义。参数页面和进程调用共用这份定义，
    /// 避免界面提示范围与真正写给EXE的范围不一致。
    /// </summary>
    public sealed class RmgExeParameterDefinition
    {
        public readonly string Key;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly RmgExeParameterValueType ValueType;
        public readonly double Minimum;
        public readonly double Maximum;
        public readonly double DefaultValue;

        public RmgExeParameterDefinition(
            string key,
            string displayName,
            string description,
            RmgExeParameterValueType valueType,
            double minimum,
            double maximum,
            double defaultValue)
        {
            Key = key;
            DisplayName = displayName;
            Description = description;
            ValueType = valueType;
            Minimum = minimum;
            Maximum = maximum;
            DefaultValue = defaultValue;
        }

        public string GetRangeText()
        {
            string format =
                ValueType == RmgExeParameterValueType.Integer
                    ? "0"
                    : "0.########";
            return Minimum.ToString(format, CultureInfo.InvariantCulture) +
                   " ～ " +
                   Maximum.ToString(format, CultureInfo.InvariantCulture);
        }

        public string GetDefaultText()
        {
            string format =
                ValueType == RmgExeParameterValueType.Integer
                    ? "0"
                    : "0.########";
            return DefaultValue.ToString(
                format,
                CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// 当前RmgAlgorithmEngine.exe支持的参数定义、默认值和组合校验。
    /// </summary>
    public static class RmgExeParameterFactory
    {
        public static RmgExeParameterDefinition[]
            GetDefinitions(string algorithm)
        {
            switch (NormalizeAlgorithm(algorithm))
            {
                case "ga":
                    return new[]
                    {
                        Int("POPULATION_SIZE", "种群规模", "每一代保留的候选调度方案数量", 10, 5000, 120),
                        Int("MAX_GENERATIONS", "最大迭代代数", "遗传算法允许执行的最大代数", 1, 100000, 400),
                        Int("ELITE_COUNT", "精英保留数量", "直接保留到下一代的优良个体数", 0, 1000, 8),
                        Int("TOURNAMENT_SIZE", "锦标赛规模", "每次选择参与比较的个体数量", 2, 1000, 4),
                        Dec("CROSSOVER_RATE", "交叉概率", "两个父代产生新解的概率", 0, 1, 0.90),
                        Dec("ORDER_MUTATION_RATE", "顺序变异概率", "改变任务执行顺序的概率", 0, 1, 0.20),
                        Dec("OWNER_MUTATION_RATE", "设备变异概率", "改变任务所属轨道吊的概率", 0, 1, 0.04),
                        Dec("TARGET_BLOCK_MUTATION_RATE", "目标箱区变异概率", "调整任务目标箱区的概率", 0, 1, 0.08),
                        Int("STAGNATION_LIMIT", "停滞终止代数", "连续若干代无改进时提前停止", 1, 100000, 180),
                        Int("GA_RANDOM_SEED", "随机种子", "用于复现实验结果的随机种子", 0, int.MaxValue, 42),
                        Int("PRINT_EVERY", "进度输出间隔", "每隔多少代输出一次进度", 1, 100000, 20)
                    };

                case "pso":
                    return new[]
                    {
                        Int("SWARM_SIZE", "粒子群规模", "同时搜索的候选调度方案数量", 10, 5000, 120),
                        Int("MAX_ITERATIONS", "最大迭代次数", "粒子群允许执行的最大迭代次数", 1, 100000, 400),
                        Dec("INERTIA_START", "初始惯性权重", "迭代开始时对原搜索方向的保留程度", 0, 2, 0.90),
                        Dec("INERTIA_END", "结束惯性权重", "迭代结束时对原搜索方向的保留程度", 0, 2, 0.40),
                        Dec("COGNITIVE_FACTOR", "个体学习因子", "粒子向自身历史最优解学习的强度", 0, 5, 1.70),
                        Dec("SOCIAL_FACTOR", "群体学习因子", "粒子向群体最优解学习的强度", 0, 5, 1.70),
                        Dec("ORDER_VELOCITY_LIMIT", "顺序速度上限", "任务顺序更新幅度的上限", 0.000001, 10, 0.60),
                        Dec("OWNER_VELOCITY_LIMIT", "设备速度上限", "设备分配更新幅度的上限", 0.000001, 10, 1.20),
                        Dec("TARGET_VELOCITY_LIMIT", "目标箱区速度上限", "目标箱区更新幅度的上限", 0.000001, 10, 1.20),
                        Dec("POSITION_LIMIT", "位置边界", "粒子位置编码允许的绝对上限", 0.000001, 100, 4.0),
                        Dec("MUTATION_RATE", "扰动概率", "对粒子进行随机扰动的概率", 0, 1, 0.08),
                        Int("STAGNATION_LIMIT", "停滞终止次数", "连续若干次无改进时提前停止", 1, 100000, 120),
                        Int("PSO_RANDOM_SEED", "随机种子", "用于复现实验结果的随机种子", 0, int.MaxValue, 42),
                        Int("PRINT_EVERY", "进度输出间隔", "每隔多少次迭代输出一次进度", 1, 100000, 20)
                    };

                case "alns":
                    return new[]
                    {
                        Int("MAX_ITERATIONS", "最大迭代次数", "ALNS允许执行的最大迭代次数", 1, 100000, 600),
                        Int("STAGNATION_LIMIT", "停滞终止次数", "连续若干次无改进时提前停止", 1, 100000, 300),
                        Int("INITIAL_SOLUTION_COUNT", "初始解数量", "生成并比较的初始调度方案数量", 1, 5000, 30),
                        Dec("MIN_REMOVAL_RATE", "最小破坏比例", "每次从当前解移除任务的最小比例", 0, 1, 0.05),
                        Dec("MAX_REMOVAL_RATE", "最大破坏比例", "每次从当前解移除任务的最大比例", 0, 1, 0.15),
                        Dec("INITIAL_TEMPERATURE_RATIO", "初始温度比例", "模拟退火接受准则的初始温度比例", 0.000001, 1, 0.05),
                        Dec("COOLING_RATE", "降温率", "每次迭代后温度的保留比例", 0.000001, 0.999999, 0.995),
                        Dec("MIN_TEMPERATURE", "最低温度", "退火过程中允许达到的最低温度", 0.000000001, 1, 0.000001),
                        Int("WEIGHT_UPDATE_INTERVAL", "算子权重更新间隔", "每隔多少次迭代更新破坏和修复算子权重", 1, 100000, 25),
                        Dec("WEIGHT_REACTION_FACTOR", "权重反应系数", "新表现对算子权重的影响程度", 0, 1, 0.20),
                        Dec("REWARD_GLOBAL_BEST", "全局最优奖励", "产生全局最优解时给予算子的分值", 0, 1000, 8.0),
                        Dec("REWARD_IMPROVEMENT", "改进解奖励", "产生优于当前解的方案时给予的分值", 0, 1000, 4.0),
                        Dec("REWARD_ACCEPTED", "接受解奖励", "方案被接受时给予的分值", 0, 1000, 1.0),
                        Int("INSERTION_POSITION_SAMPLES", "插入位置采样数", "修复阶段为任务尝试的候选插入位置数量", 1, 100000, 25),
                        Int("ALNS_RANDOM_SEED", "随机种子", "用于复现实验结果的随机种子", 0, int.MaxValue, 42),
                        Int("PRINT_EVERY", "进度输出间隔", "每隔多少次迭代输出一次进度", 1, 100000, 20)
                    };

                default:
                    throw new ArgumentException(
                        "算法必须是 ga、pso 或 alns。",
                        "algorithm");
            }
        }

        public static JObject CreateDefaults(string algorithm)
        {
            RmgExeParameterDefinition[] definitions =
                GetDefinitions(algorithm);
            JObject result = new JObject();
            for (int index = 0; index < definitions.Length; index++)
            {
                RmgExeParameterDefinition definition =
                    definitions[index];
                result[definition.Key] =
                    definition.ValueType ==
                    RmgExeParameterValueType.Integer
                        ? new JValue(Convert.ToInt64(definition.DefaultValue))
                        : new JValue(definition.DefaultValue);
            }
            return result;
        }

        public static bool TryNormalizeAndValidate(
            string algorithm,
            JObject supplied,
            out JObject normalized,
            out string error)
        {
            normalized = null;
            error = null;

            RmgExeParameterDefinition[] definitions;
            try
            {
                definitions = GetDefinitions(algorithm);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }

            if (supplied == null)
            {
                error = "没有填写算法参数。";
                return false;
            }

            JObject result = new JObject();
            for (int index = 0; index < definitions.Length; index++)
            {
                RmgExeParameterDefinition definition = definitions[index];
                JToken token = supplied[definition.Key];
                if (token == null ||
                    token.Type == JTokenType.Null ||
                    string.IsNullOrWhiteSpace(token.ToString()))
                {
                    error = "参数“" + definition.DisplayName + "”不能为空。";
                    return false;
                }

                double value;
                if (!TryReadNumber(token, out value))
                {
                    error = "参数“" + definition.DisplayName + "”不是有效数字。";
                    return false;
                }

                if (definition.ValueType ==
                        RmgExeParameterValueType.Integer &&
                    Math.Abs(value - Math.Round(value)) > 0.0000001)
                {
                    error = "参数“" + definition.DisplayName + "”必须填写整数。";
                    return false;
                }

                if (value < definition.Minimum ||
                    value > definition.Maximum)
                {
                    error = "参数“" + definition.DisplayName +
                            "”必须位于 " + definition.GetRangeText() +
                            " 范围内。";
                    return false;
                }

                result[definition.Key] =
                    definition.ValueType ==
                    RmgExeParameterValueType.Integer
                        ? new JValue(Convert.ToInt64(Math.Round(value)))
                        : new JValue(value);
            }

            if (!ValidateRelationships(
                    NormalizeAlgorithm(algorithm),
                    result,
                    out error))
            {
                return false;
            }

            normalized = result;
            return true;
        }

        private static bool ValidateRelationships(
            string algorithm,
            JObject values,
            out string error)
        {
            error = null;
            if (algorithm == "ga")
            {
                if (Number(values, "ELITE_COUNT") >= Number(values, "POPULATION_SIZE"))
                {
                    error = "精英保留数量必须小于种群规模。";
                    return false;
                }
                if (Number(values, "TOURNAMENT_SIZE") > Number(values, "POPULATION_SIZE"))
                {
                    error = "锦标赛规模不能超过种群规模。";
                    return false;
                }
                if (Number(values, "STAGNATION_LIMIT") > Number(values, "MAX_GENERATIONS"))
                {
                    error = "停滞终止代数不能超过最大迭代代数。";
                    return false;
                }
                if (Number(values, "PRINT_EVERY") > Number(values, "MAX_GENERATIONS"))
                {
                    error = "进度输出间隔不能超过最大迭代代数。";
                    return false;
                }
            }
            else if (algorithm == "pso")
            {
                if (Number(values, "INERTIA_START") < Number(values, "INERTIA_END"))
                {
                    error = "初始惯性权重不能小于结束惯性权重。";
                    return false;
                }
                if (Number(values, "STAGNATION_LIMIT") > Number(values, "MAX_ITERATIONS"))
                {
                    error = "停滞终止次数不能超过最大迭代次数。";
                    return false;
                }
                if (Number(values, "PRINT_EVERY") > Number(values, "MAX_ITERATIONS"))
                {
                    error = "进度输出间隔不能超过最大迭代次数。";
                    return false;
                }
            }
            else if (algorithm == "alns")
            {
                if (Number(values, "MIN_REMOVAL_RATE") > Number(values, "MAX_REMOVAL_RATE"))
                {
                    error = "最小破坏比例不能大于最大破坏比例。";
                    return false;
                }
                if (Number(values, "STAGNATION_LIMIT") > Number(values, "MAX_ITERATIONS"))
                {
                    error = "停滞终止次数不能超过最大迭代次数。";
                    return false;
                }
                if (Number(values, "WEIGHT_UPDATE_INTERVAL") > Number(values, "MAX_ITERATIONS"))
                {
                    error = "算子权重更新间隔不能超过最大迭代次数。";
                    return false;
                }
                if (Number(values, "PRINT_EVERY") > Number(values, "MAX_ITERATIONS"))
                {
                    error = "进度输出间隔不能超过最大迭代次数。";
                    return false;
                }
                if (Number(values, "REWARD_GLOBAL_BEST") < Number(values, "REWARD_IMPROVEMENT") ||
                    Number(values, "REWARD_IMPROVEMENT") < Number(values, "REWARD_ACCEPTED"))
                {
                    error = "奖励值应满足：全局最优奖励 ≥ 改进解奖励 ≥ 接受解奖励。";
                    return false;
                }
            }

            return true;
        }

        private static bool TryReadNumber(JToken token, out double value)
        {
            if (token.Type == JTokenType.Integer ||
                token.Type == JTokenType.Float)
            {
                value = token.Value<double>();
                return !double.IsNaN(value) && !double.IsInfinity(value);
            }

            string text = token.ToString().Trim();
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

        private static double Number(JObject values, string key)
        {
            return values[key].Value<double>();
        }

        private static string NormalizeAlgorithm(string algorithm)
        {
            return (algorithm ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static RmgExeParameterDefinition Int(
            string key,
            string displayName,
            string description,
            double minimum,
            double maximum,
            double defaultValue)
        {
            return new RmgExeParameterDefinition(
                key, displayName, description,
                RmgExeParameterValueType.Integer,
                minimum, maximum, defaultValue);
        }

        private static RmgExeParameterDefinition Dec(
            string key,
            string displayName,
            string description,
            double minimum,
            double maximum,
            double defaultValue)
        {
            return new RmgExeParameterDefinition(
                key, displayName, description,
                RmgExeParameterValueType.Decimal,
                minimum, maximum, defaultValue);
        }
    }
}
