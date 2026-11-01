using System;

namespace Vtp.VR.Assessment
{
    [Serializable]
    public sealed class AssessmentSubmissionRequestDto
    {
        public string schemaVersion;
        public string submissionId;
        public string assessmentProfileId;
        public int assessmentProfileVersion;
        public string experimentId;
        public string studentNumber;
        public string taskId;
        public int taskVersion;
        public string occurredAt;
        public int totalScore;
        public int maximumScore;
        public string reportJson;
    }

    [Serializable]
    public sealed class AssessmentSubmissionReceiptDto
    {
        public string submissionId;
        public string serverRecordId;
        public string status;
        public string acceptedAt;
    }

    [Serializable]
    public sealed class AssessmentSubmissionError
    {
        public string code;
        public string message;
        public bool retryable;
    }

    /// <summary>
    /// 评分模块唯一依赖的服务器提交契约。
    /// 后续由服务器对接层实现该接口；评分内核不依赖URL、Token或传输协议。
    /// </summary>
    public interface IAssessmentSubmissionGateway
    {
        void Submit(
            AssessmentSubmissionRequestDto request,
            Action<AssessmentSubmissionReceiptDto> onSuccess,
            Action<AssessmentSubmissionError> onError);
    }

    public static class AssessmentSubmissionGatewayRegistry
    {
        private static IAssessmentSubmissionGateway _current;

        public static bool TryGet(
            out IAssessmentSubmissionGateway gateway)
        {
            gateway = _current;
            return gateway != null;
        }

        public static void Configure(
            IAssessmentSubmissionGateway gateway)
        {
            if (gateway == null)
                throw new ArgumentNullException("gateway");
            _current = gateway;
        }

        public static void Clear()
        {
            _current = null;
        }
    }
}
