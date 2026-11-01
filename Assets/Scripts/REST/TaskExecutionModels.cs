using System;

namespace Vtp.VR.Rest
{
    [Serializable]
    public sealed class AlgorithmReferenceDto
    {
        public string algorithmId;
        public string version;
        public string sha256;
        public long fileSize;
        public string signature;
        public string fileId;
    }

    [Serializable]
    public sealed class AlgorithmDownloadTicketDto
    {
        public string algorithmId;
        public string version;
        public string sha256;
        public long fileSize;
        public string signature;
        public string downloadUrl;
        public string expiresAt;
    }

    [Serializable]
    public sealed class AlgorithmDownloadTicketEnvelope
    {
        public int code;
        public string msg;
        public AlgorithmDownloadTicketDto result;
        public string requestId;
    }

    [Serializable]
    public sealed class TaskCaseDocumentDto
    {
        public string schemaVersion;
        public string taskId;
        public int taskVersion;
        public string sceneId;
        public string generatedAt;
        public CraneInputDto[] cranes;
        public SchedulingJobInputDto[] jobs;
        public SchedulingConstraintDto constraints;
    }

    [Serializable]
    public sealed class CraneInputDto
    {
        public string craneId;
        public int initialBay;
        public float speed;
    }

    [Serializable]
    public sealed class SchedulingJobInputDto
    {
        public string jobId;
        public string containerNo;
        public string taskType;
        public string craneHint;
        public float startPointPosition;
        public float startList;
        public float endPointPosition;
        public float endList;
        public SlotPositionDto source;
        public SlotPositionDto target;
        public float releaseTimeSeconds;
        public int priority;
        public float estimatedDurationSeconds;
    }

    [Serializable]
    public sealed class SchedulingConstraintDto
    {
        public int minimumCraneDistance;
        public bool allowCrossing;
        public int maximumRunSeconds;
    }

    [Serializable]
    public sealed class SchedulingResultDto
    {
        public string scheduleId;
        public ScheduledOperationDto[] operations;
        public SchedulingMetricsDto metrics;
    }

    [Serializable]
    public sealed class ScheduledOperationDto
    {
        public int quayCraneId;
        public string sourceVessel;
        public int sourceShipBay;
        public int sourceShipColumn;
        public int sourceShipTier;
        public int sequence;
        public string jobId;
        public string containerNo;
        public string craneId;
        public string fromCode;
        public string toCode;
        public float plannedStartSeconds;
        public float plannedEndSeconds;
        public string taskType;
        public string sourceAreaType;
        public string sourceAreaId;
        public float sourceBay;
        public float sourceRow;
        public int sourceTier;
        public string targetAreaType;
        public string targetAreaId;
        public float targetBay;
        public float targetRow;
        public int targetTier;
    }

    [Serializable]
    public sealed class SchedulingMetricsDto
    {
        public int jobCount;
        public float makespanSeconds;
        public float emptyTravelDistance;
    }

    public sealed class ValidatedTaskCase
    {
        public readonly TaskCaseDocumentDto Document;
        public readonly string LocalPath;

        public ValidatedTaskCase(
            TaskCaseDocumentDto document,
            string localPath)
        {
            Document = document;
            LocalPath = localPath;
        }
    }
}
