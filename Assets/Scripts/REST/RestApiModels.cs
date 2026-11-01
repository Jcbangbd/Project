using System;

namespace Vtp.VR.Rest
{
    [Serializable]
    public sealed class RestApiSettings
    {
        public string baseUrl = "http://43.143.107.9:9012/vtp-server";
        public string loginFrom = "web";
        public string authHeaderName = "Token";
        public string authScheme = "";
        public int timeoutSeconds = 20;
        public int maxRetries = 2;
    }

    [Serializable]
    public sealed class ApiErrorEnvelope
    {
        public int code;
        public string msg;
        public string requestId;
    }

    [Serializable]
    public sealed class ApiEmptyEnvelope
    {
        public int code;
        public string msg;
        public string requestId;
    }

    [Serializable]
    public sealed class LoginRequestDto
    {
        public string userName;
        public string password;
        public string loginFrom;
    }

    [Serializable]
    public sealed class LoginResultDto
    {
        public string userId;
        public string userName;
        public string realName;
        public string token;
        public int flag;
        public string roleId;
        public string roleName;
        public string deptId;
        public string deptName;
        public string groupId;
        public string groupName;
        public string phone;
        public string[] authCodes;
        public string signId;
    }

    [Serializable]
    public sealed class LoginResponseEnvelope
    {
        public int code;
        public string msg;
        public LoginResultDto result;
        public string requestId;
    }

    [Serializable]
    public sealed class ChangePasswordRequestDto
    {
        public string oldPassword;
        public string newPassword;
    }

    [Serializable]
    public sealed class VrTaskDocumentRefDto
    {
        public string fileId;
        public string fileName;
        public string fileMd5;
        public long fileSize;
        public string schemaVersion;
    }

    [Serializable]
    public sealed class VrTaskSummaryDto
    {
        public string taskId;
        public string title;
        public string taskType;
        public string experimentId;
        public string status;
        public int version;
        public string deadline;
        public VrTaskDocumentRefDto caseDocument;
        public AlgorithmReferenceDto requiredAlgorithm;
    }

    [Serializable]
    public sealed class VrTaskDetailDto
    {
        public string taskId;
        public string title;
        public string taskType;
        public string experimentId;
        public string classId;
        public string status;
        public int version;
        public string availableFrom;
        public string deadline;
        public string scoringRule;
        public VrTaskDocumentRefDto caseDocument;
        public AlgorithmReferenceDto requiredAlgorithm;
    }

    [Serializable]
    public sealed class VrTaskListResultDto
    {
        public VrTaskSummaryDto[] list;
        public long total;
    }

    [Serializable]
    public sealed class VrTaskListResponseEnvelope
    {
        public int code;
        public string msg;
        public VrTaskListResultDto result;
        public string requestId;
    }

    [Serializable]
    public sealed class VrTaskDetailResponseEnvelope
    {
        public int code;
        public string msg;
        public VrTaskDetailDto result;
        public string requestId;
    }

    [Serializable]
    public sealed class TaskEventRequestDto
    {
        public string eventId;
        public string eventType;
        public int taskVersion;
        public string occurredAt;
        public string deviceId;
        public string clientVersion;
        public string detail;
    }

    [Serializable]
    public sealed class DownloadUrlResultDto
    {
        public string downloadUrl;
        public string expiresAt;
        public string fileMd5;
        public long fileSize;
    }

    [Serializable]
    public sealed class DownloadUrlObjectEnvelope
    {
        public int code;
        public string msg;
        public DownloadUrlResultDto result;
        public string requestId;
    }

    [Serializable]
    public sealed class DownloadUrlStringEnvelope
    {
        public int code;
        public string msg;
        public string result;
        public string requestId;
    }

    [Serializable]
    public sealed class SlotPositionDto
    {
        public string areaType;
        public string areaId;
        public int bay;
        public int row;
        public int tier;
        public string displayCode;
    }

}
