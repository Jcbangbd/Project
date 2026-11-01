using Vtp.VR.Rest;

namespace Vtp.VR.Integration
{
    /// <summary>
    /// 跨场景保存本次远程算例、算法输入和算法输出。
    /// 只保存普通数据，不需要挂载到 GameObject。
    /// </summary>
    public static class SceneExampleScheduleRuntime
    {
        public static TaskCaseDocumentDto Input
        {
            get;
            private set;
        }

        public static SchedulingResultDto Schedule
        {
            get;
            private set;
        }

        public static bool HasSchedule
        {
            get
            {
                return Input != null &&
                       Input.jobs != null &&
                       Schedule != null &&
                       Schedule.operations != null &&
                       Schedule.operations.Length > 0;
            }
        }

        public static void Set(
            TaskCaseDocumentDto input,
            SchedulingResultDto schedule)
        {
            Input = input;
            Schedule = schedule;
        }

        public static void Clear()
        {
            Input = null;
            Schedule = null;
        }
    }
}

