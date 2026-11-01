using System;
using System.IO;
using System.Text;
using UnityEngine;
using Vtp.VR.Rest;

namespace Vtp.VR.Integration
{
    /// <summary>
    /// 把算法返回的全部任务打印到 Console，并保存成便于检查的 JSON。
    /// 静态工具类，不需要挂载到 GameObject。
    /// </summary>
    public static class SceneScheduleDebugExporter
    {
        [Serializable]
        private sealed class ScheduleDebugDocument
        {
            public string exportedAt;
            public TaskCaseDocumentDto input;
            public SchedulingResultDto result;
        }

        public static string LogAndSave(
            TaskCaseDocumentDto input,
            SchedulingResultDto schedule)
        {
            if (schedule == null ||
                schedule.operations == null)
            {
                Debug.LogWarning(
                    "[ScheduleAll] 没有可输出的调度序列。");
                return null;
            }

            ScheduledOperationDto[] operations =
                (ScheduledOperationDto[])
                schedule.operations.Clone();

            Array.Sort(
                operations,
                delegate(
                    ScheduledOperationDto left,
                    ScheduledOperationDto right)
                {
                    if (left == null)
                        return 1;
                    if (right == null)
                        return -1;
                    return left.sequence.CompareTo(
                        right.sequence);
                });

            int dischargeCount = 0;
            int loadCount = 0;
            int reorderCount = 0;
            int collectCount = 0;
            int dispatchCount = 0;

            for (int index = 0;
                 index < operations.Length;
                 index++)
            {
                ScheduledOperationDto operation =
                    operations[index];
                if (operation == null)
                    continue;

                switch (operation.taskType)
                {
                    case "卸船":
                        dischargeCount++;
                        break;
                    case "装船":
                        loadCount++;
                        break;
                    case "翻箱":
                        reorderCount++;
                        break;
                    case "卸车":
                        collectCount++;
                        break;
                    case "装车":
                        dispatchCount++;
                        break;
                }

                Debug.Log(
                    string.Format(
                        "[ScheduleAll] {0}/{1}，" +
                        "sequence={2}，jobId={3}，" +
                        "类型={4}，轨道吊={5}，" +
                        "起点={6}，终点={7}，" +
                        "开始={8:0.###}s，结束={9:0.###}s",
                        index + 1,
                        operations.Length,
                        operation.sequence,
                        operation.jobId,
                        operation.taskType,
                        operation.craneId,
                        operation.fromCode,
                        operation.toCode,
                        operation.plannedStartSeconds,
                        operation.plannedEndSeconds));
            }

            Debug.Log(
                string.Format(
                    "[ScheduleAll] 总数={0}：" +
                    "卸船={1}，装船={2}，翻箱={3}，" +
                    "卸车={4}，装车={5}",
                    operations.Length,
                    dischargeCount,
                    loadCount,
                    reorderCount,
                    collectCount,
                    dispatchCount));

            try
            {
                string folder =
                    Path.Combine(
                        Application.persistentDataPath,
                        "Schedules");
                Directory.CreateDirectory(folder);

                string taskId =
                    input == null
                        ? "unknown-task"
                        : MakeSafeFileName(input.taskId);
                string fileName =
                    taskId +
                    "_" +
                    DateTime.Now.ToString(
                        "yyyyMMdd_HHmmss") +
                    ".json";
                string path =
                    Path.Combine(
                        folder,
                        fileName);

                ScheduleDebugDocument document =
                    new ScheduleDebugDocument
                    {
                        exportedAt =
                            DateTime.UtcNow.ToString("o"),
                        input = input,
                        result = schedule
                    };

                File.WriteAllText(
                    path,
                    JsonUtility.ToJson(
                        document,
                        true),
                    new UTF8Encoding(false));

                Debug.Log(
                    "[ScheduleAll] 完整12条调度数据已保存：" +
                    path);
                return path;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[ScheduleAll] 保存调度JSON失败：" +
                    exception.Message);
                return null;
            }
        }

        private static string MakeSafeFileName(
            string value)
        {
            string result =
                string.IsNullOrWhiteSpace(value)
                    ? "unknown-task"
                    : value.Trim();

            char[] invalid =
                Path.GetInvalidFileNameChars();
            for (int index = 0;
                 index < invalid.Length;
                 index++)
            {
                result =
                    result.Replace(
                        invalid[index],
                        '_');
            }

            return result;
        }
    }
}

