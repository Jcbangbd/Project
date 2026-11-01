using System;
using System.Collections.Generic;
using Vtp.VR.Rest;

namespace Vtp.VR.Integration
{
    public static class QuestionCaseSampler
    {
        public static readonly string[] RequiredTypes = { "卸船", "装船", "卸车", "装车", "翻箱" };
        public static bool TryCreate(SceneNumericalExampleDto[] examples, int seed,
            out SceneNumericalExampleDto[] sample, out string error)
        {
            var groups = new Dictionary<string, List<SceneNumericalExampleDto>>();
            foreach (string type in RequiredTypes) groups.Add(type, new List<SceneNumericalExampleDto>());
            if (examples != null) foreach (var example in examples)
            {
                string type;
                if (SceneExampleTaskCaseAdapter.TryInferTaskTypeFromPositions(example, out type))
                    groups[type].Add(example);
            }
            var missing = new List<string>();
            foreach (string type in RequiredTypes) if (groups[type].Count == 0) missing.Add(type);
            if (missing.Count > 0)
            {
                sample = new SceneNumericalExampleDto[0];
                error = "教师算例缺少以下作业类型：" + string.Join("、", missing.ToArray()) + "。请重新获取或联系教师补充完整算例。";
                return false;
            }
            var random = new Random(seed);
            sample = new SceneNumericalExampleDto[RequiredTypes.Length];
            for (int i = 0; i < sample.Length; i++)
            {
                var group = groups[RequiredTypes[i]];
                sample[i] = group[random.Next(group.Count)];
            }
            for (int i = sample.Length - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                var value = sample[i]; sample[i] = sample[other]; sample[other] = value;
            }
            error = null;
            return true;
        }
    }
}
