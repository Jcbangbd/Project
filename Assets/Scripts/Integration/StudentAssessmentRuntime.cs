using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using Vtp.VR.Assessment;
using Vtp.VR.Rest;

namespace Vtp.VR.Integration
{
    [Serializable]
    public sealed class StudentAssessmentReportDto
    {
        public string schemaVersion;
        public string submissionId;
        public string assessmentProfileId;
        public string experimentId;
        public int assessmentProfileVersion;
        public string studentNumber;
        public string taskId;
        public string taskTitle;
        public int taskVersion;
        public string startedAt;
        public string submittedAt;
        public int totalScore;
        public int maximumScore;
        public string referenceAlgorithm;
        public string selectedAlgorithm;
        public string selectedReasonId;
        public string algorithmParametersJson;
        public string scheduleFingerprint;
        public string quayAssignmentMethod;
        public QuayAssignmentRecord[] quayAssignments;
        public PreExperimentAnswerDto[] preExperimentAnswers;
        public AssessmentItemResultDto[] items;
    }

    /// <summary>
    /// 轨道吊实验对通用评分会话的适配层。
    /// 页面只调用本类记录动作；计分、总分和未作答补零由通用评分内核处理。
    /// 正式成绩始终保存本地JSON。服务器提交通过可注入网关预留，
    /// 未配置网关时不会猜测服务器URL或复用不匹配的业务接口。
    /// </summary>
    public static class StudentAssessmentRuntime
    {
        private const string ReportSchemaVersion =
            "assessment-report/v1";

        private static readonly AssessmentSession Session =
            new AssessmentSession(
                RmgSchedulingAssessmentProfile.Create());

        private static string _studentNumber = string.Empty;
        private static string _taskId = string.Empty;
        private static string _taskTitle = string.Empty;
        private static int _taskVersion = 1;
        private static string _startedAt = string.Empty;
        private static string _referenceAlgorithm = string.Empty;
        private static string _selectedAlgorithm = string.Empty;
        private static string _selectedReasonId = string.Empty;
        private static string _parametersJson = string.Empty;
        private static string _scheduleFingerprint = string.Empty;
        private static bool _selectedReasonCorrect;
        private static bool _parametersValid;
        private static bool _localTestTask;

        public static string CurrentTaskId
        {
            get { return _taskId; }
        }

        public static void BeginSession(string studentNumber)
        {
            _studentNumber = (studentNumber ?? string.Empty).Trim();
            ResetTaskContext();
            Session.Reset();
            Session.RecordPass(
                "1",
                true,
                "学生已登录并建立服务器会话。");
        }

        public static void SelectTask(
            VrTaskSummaryDto task,
            bool localTestTask)
        {
            AutomaticQuayCraneScheduler.Clear();
            AssessmentItemResultDto login;
            bool hasLogin = Session.TryGetResult("1", out login);
            Session.Reset();
            if (hasLogin)
            {
                Session.Record(
                    login.itemId,
                    login.score,
                    login.passed,
                    login.detail);
            }

            _taskId = task == null
                ? string.Empty
                : (task.taskId ?? string.Empty).Trim();
            _taskTitle = task == null
                ? string.Empty
                : (task.title ?? string.Empty).Trim();
            _taskVersion = task == null || task.version <= 0
                ? 1
                : task.version;
            _startedAt = DateTime.UtcNow.ToString("o");
            _referenceAlgorithm = string.Empty;
            _selectedAlgorithm = string.Empty;
            _selectedReasonId = string.Empty;
            _parametersJson = string.Empty;
            _scheduleFingerprint = string.Empty;
            _selectedReasonCorrect = false;
            _parametersValid = false;
            _localTestTask = localTestTask;

            Session.RecordPass(
                "2",
                task != null && !string.IsNullOrWhiteSpace(_taskId),
                localTestTask
                    ? "已选择本地测试任务；该记录不应作为正式成绩。"
                    : "已从教师任务列表选择任务。");
        }

        public static void MarkIntroductionViewed()
        {
            Session.RecordPass(
                "3",
                true,
                "已查看实验场景定义和问题描述。");
        }

        public static void MarkExamplesViewed(int exampleCount)
        {
            Session.RecordPass(
                "4",
                exampleCount > 0,
                "已查看集装箱任务数量：" + exampleCount + "。");
        }

        public static void MarkAlgorithmIntroductionViewed()
        {
            Session.RecordPass(
                "13",
                true,
                "已打开GA、PSO和ALNS算法介绍及参数范围。");
        }

        public static void SetAlgorithmReference(
            string algorithmKey,
            string detail)
        {
            _referenceAlgorithm =
                (algorithmKey ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(detail))
                Debug.Log("[Assessment] 教师任务参考算法：" + detail);
        }

        public static void RecordAlgorithmSelection(
            AlgorithmAssessmentSelection selection)
        {
            _selectedAlgorithm = selection == null
                ? string.Empty
                : (selection.AlgorithmKey ?? string.Empty).Trim();
            _selectedReasonId = selection == null
                ? string.Empty
                : (selection.ReasonId ?? string.Empty).Trim();
            _selectedReasonCorrect =
                selection != null && selection.IsReferenceReason;
            _parametersValid =
                selection != null && selection.Parameters != null;
            _parametersJson =
                selection == null || selection.Parameters == null
                    ? string.Empty
                    : selection.Parameters.ToString();
        }

        public static void RecordAlgorithmExecution(
            bool feasible,
            string error)
        {
            bool algorithmCorrect =
                feasible &&
                !string.IsNullOrWhiteSpace(_referenceAlgorithm) &&
                string.Equals(
                    _selectedAlgorithm,
                    _referenceAlgorithm,
                    StringComparison.OrdinalIgnoreCase) &&
                _selectedReasonCorrect;

            Session.RecordPass(
                "14",
                algorithmCorrect,
                feasible
                    ? "已按教师任务配置核对算法与选择原因，所选算法生成了可行结果。"
                    : "所选算法未生成可行解：" +
                      (error ?? "未知错误"));

            bool parameterPassed = feasible && _parametersValid;
            Session.RecordPass(
                "15",
                parameterPassed,
                parameterPassed
                    ? "全部必填参数通过范围和参数关系校验，算法成功启动。"
                    : "参数未通过完整校验或算法未能启动。");
        }

        public static void RecordScheduleSubmission(
            bool passed,
            double enteredCmax,
            int enteredRmg1,
            int enteredRmg2,
            int enteredShared,
            string scheduleFingerprint,
            string detail)
        {
            _scheduleFingerprint = scheduleFingerprint ?? string.Empty;
            Session.RecordPass(
                "16",
                passed,
                "提交值：Cmax=" +
                enteredCmax.ToString("0.##", CultureInfo.InvariantCulture) +
                "，RMG1=" + enteredRmg1 +
                "，RMG2=" + enteredRmg2 +
                "，共享区=" + enteredShared + "。" +
                (detail ?? string.Empty));
        }

        public static void RecordRuleResult(
            string itemId,
            bool passed,
            string detail)
        {
            Session.RecordPass(itemId, passed, detail);
        }

        public static StudentAssessmentReportDto BuildReport()
        {
            AssessmentScoreSnapshotDto snapshot =
                Session.BuildSnapshot();
            return new StudentAssessmentReportDto
            {
                schemaVersion = ReportSchemaVersion,
                submissionId = Guid.NewGuid().ToString("N"),
                assessmentProfileId = snapshot.profileId,
                experimentId = snapshot.experimentId,
                assessmentProfileVersion = snapshot.profileVersion,
                studentNumber = _studentNumber,
                taskId = _taskId,
                taskTitle = _taskTitle,
                taskVersion = _taskVersion,
                startedAt = _startedAt,
                submittedAt = DateTime.UtcNow.ToString("o"),
                totalScore = snapshot.totalScore,
                maximumScore = snapshot.maximumScore,
                referenceAlgorithm = _referenceAlgorithm,
                selectedAlgorithm = _selectedAlgorithm,
                selectedReasonId = _selectedReasonId,
                algorithmParametersJson = _parametersJson,
                scheduleFingerprint = _scheduleFingerprint,
                quayAssignmentMethod = AutomaticQuayCraneScheduler.Method,
                quayAssignments = AutomaticQuayCraneScheduler.BuildRecords(),
                preExperimentAnswers =
                    PreExperimentAssessmentRuntime.Answers,
                items = snapshot.items
            };
        }

        public static string SaveAndSubmit()
        {
            StudentAssessmentReportDto report = BuildReport();
            string directory = Path.Combine(
                Application.persistentDataPath,
                "AssessmentRecords");
            Directory.CreateDirectory(directory);
            string safeTaskId = string.IsNullOrWhiteSpace(_taskId)
                ? "unknown-task"
                : MakeFileNameSafe(_taskId);
            string path = Path.Combine(
                directory,
                safeTaskId + "_" +
                DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");
            string json = JsonUtility.ToJson(report, true);
            File.WriteAllText(path, json);

            TrySubmitToConfiguredGateway(report, json);
            return path;
        }

        /// <summary>
        /// 兼容现有页面的记录入口。新增实验应直接使用AssessmentSession，
        /// 本实验新增判定应优先调用RecordRuleResult。
        /// </summary>
        public static void RecordItem(
            string itemId,
            string title,
            int maxScore,
            int score,
            bool passed,
            string detail)
        {
            AssessmentItemDefinition definition;
            if (Session.TryGetDefinition(itemId, out definition) &&
                (definition.maxScore != maxScore ||
                 !string.Equals(
                     definition.title,
                     title,
                     StringComparison.Ordinal)))
            {
                Debug.LogWarning(
                    "[Assessment] 页面评分项与评分配置不一致，" +
                    "已按配置计分：" + itemId);
            }
            Session.Record(itemId, score, passed, detail);
        }

        private static void TrySubmitToConfiguredGateway(
            StudentAssessmentReportDto report,
            string json)
        {
            if (_localTestTask ||
                !RestSession.IsAuthenticated ||
                string.IsNullOrWhiteSpace(_taskId))
            {
                return;
            }

            IAssessmentSubmissionGateway gateway;
            if (!AssessmentSubmissionGatewayRegistry.TryGet(out gateway))
            {
                Debug.Log(
                    "[Assessment] 尚未配置服务器评分提交网关，" +
                    "成绩仅保存到本地。");
                return;
            }

            AssessmentSubmissionRequestDto request =
                new AssessmentSubmissionRequestDto
                {
                    schemaVersion = "assessment-submission/v1",
                    submissionId = report.submissionId,
                    assessmentProfileId = report.assessmentProfileId,
                    assessmentProfileVersion =
                        report.assessmentProfileVersion,
                    experimentId = report.experimentId,
                    studentNumber = report.studentNumber,
                    taskId = report.taskId,
                    taskVersion = report.taskVersion,
                    occurredAt = report.submittedAt,
                    totalScore = report.totalScore,
                    maximumScore = report.maximumScore,
                    reportJson = json
                };

            gateway.Submit(
                request,
                delegate(AssessmentSubmissionReceiptDto receipt)
                {
                    Debug.Log(
                        "[Assessment] 服务器已接收成绩，记录号：" +
                        (receipt == null
                            ? string.Empty
                            : receipt.serverRecordId));
                },
                delegate(AssessmentSubmissionError error)
                {
                    Debug.LogWarning(
                        "[Assessment] 服务器暂未接收成绩，" +
                        "本地记录已保留：" +
                        (error == null ? "未知错误" : error.message));
                });
        }

        private static void ResetTaskContext()
        {
            _taskId = string.Empty;
            _taskTitle = string.Empty;
            _taskVersion = 1;
            _startedAt = DateTime.UtcNow.ToString("o");
            _referenceAlgorithm = string.Empty;
            _selectedAlgorithm = string.Empty;
            _selectedReasonId = string.Empty;
            _parametersJson = string.Empty;
            _scheduleFingerprint = string.Empty;
            _selectedReasonCorrect = false;
            _parametersValid = false;
            _localTestTask = false;
        }

        private static string MakeFileNameSafe(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            string result = value;
            for (int index = 0; index < invalid.Length; index++)
                result = result.Replace(invalid[index], '_');
            return result;
        }
    }
}
