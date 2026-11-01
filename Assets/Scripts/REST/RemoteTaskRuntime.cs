namespace Vtp.VR.Rest
{
    public static class RemoteTaskRuntime
    {
        private static VrTaskSummaryDto[] _availableTasks =
            new VrTaskSummaryDto[0];

        public static VrTaskSummaryDto[] AvailableTasks
        {
            get { return _availableTasks; }
        }

        public static VrTaskDetailDto CurrentTask { get; private set; }
        public static TaskCaseDocumentDto CurrentCase { get; private set; }
        public static string CurrentCasePath { get; private set; }
        public static SchedulingResultDto CurrentSchedule { get; private set; }
        public static AlgorithmReferenceDto CurrentAlgorithm
        {
            get;
            private set;
        }

        public static bool HasReadyTask
        {
            get
            {
                return CurrentTask != null &&
                       CurrentCase != null &&
                       CurrentSchedule != null &&
                       CurrentAlgorithm != null &&
                       !string.IsNullOrEmpty(CurrentTask.taskId);
            }
        }

        public static void SetAvailableTasks(VrTaskSummaryDto[] tasks)
        {
            _availableTasks = tasks ?? new VrTaskSummaryDto[0];
        }

        public static void SetPrepared(
            VrTaskDetailDto task,
            ValidatedTaskCase taskCase,
            SchedulingResultDto schedule,
            AlgorithmReferenceDto algorithm)
        {
            CurrentTask = task;
            CurrentCase =
                taskCase == null ? null : taskCase.Document;
            CurrentCasePath =
                taskCase == null ? null : taskCase.LocalPath;
            CurrentSchedule = schedule;
            CurrentAlgorithm = algorithm;

        }

        public static void Clear()
        {
            _availableTasks = new VrTaskSummaryDto[0];
            CurrentTask = null;
            CurrentCase = null;
            CurrentCasePath = null;
            CurrentSchedule = null;
            CurrentAlgorithm = null;
        }
    }
}
