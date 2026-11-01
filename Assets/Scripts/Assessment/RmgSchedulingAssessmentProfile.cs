namespace Vtp.VR.Assessment
{
    /// <summary>
    /// 轨道吊调度虚拟仿真实验评分细则版本1，总分100分。
    /// </summary>
    public static class RmgSchedulingAssessmentProfile
    {
        public const string ProfileId =
            "rmg-scheduling-student-actions";
        public const string ExperimentId =
            "rmg-scheduling";
        public const int Version = 1;

        public static AssessmentProfile Create()
        {
            return new AssessmentProfile(
                ProfileId,
                ExperimentId,
                Version,
                new[]
                {
                    Item("1", "登录系统", 0),
                    Item("2", "获取实验任务", 0),
                    Item("3", "查看实验介绍", 0),
                    Item("4", "查看算例", 0),
                    Item("5", "算例作业类型勾选", 5),
                    Item("6", "优化目标选择", 5),
                    Item("7", "最小安全距离设置", 8),
                    Item("8", "水平移动速度设置", 8),
                    Item("9", "起升速度设置", 6),
                    Item("10", "任务分配规则选择", 10),
                    Item("11", "勾选必要约束", 15),
                    Item("12", "初始解规则选择", 5),
                    Item("13", "阅读算法介绍面板", 0),
                    Item("14", "求解算法选择", 10),
                    Item("15", "填写算法参数值", 18),
                    Item("16", "调度结果核对与提交", 10)
                });
        }

        private static AssessmentItemDefinition Item(
            string itemId,
            string title,
            int maxScore)
        {
            return new AssessmentItemDefinition(
                itemId,
                title,
                maxScore);
        }
    }
}

