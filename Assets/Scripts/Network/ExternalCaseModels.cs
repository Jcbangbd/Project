using System;
using System.Collections.Generic;

namespace ZPMC.VR.Unity
{
    /// <summary>
    /// 外部传入的一整个卸船算例
    /// </summary>
    [Serializable]
    public class DischargeCaseRequest
    {
        public string caseId;

        // 当前 A40 卸船建议使用 1013
        public int stsId;

        // 例如 Vessel004
        public string vesselName;

        public List<DischargeTaskDto> tasks;
    }

    /// <summary>
    /// 一条卸船任务
    /// </summary>
    [Serializable]
    public class DischargeTaskDto
    {
        public int sequence;

        // 船上源位置
        public int sourceBay;
        public int sourceColumn;
        public int sourceTier;

        // 堆场目标箱位
        // 例如 A40017011
        public string targetBoxNumber;
    }

    /// <summary>
    /// REST 接口响应
    /// </summary>
    [Serializable]
    public class ApiResponse
    {
        public bool success;
        public string code;
        public string message;
        public string caseId;
    }

    /// <summary>
    /// 解析后的堆场目标位置
    /// </summary>
    public class YardTargetCode
    {
        public string BoxNumber;
        public string BlockName;
        public int Bay;
        public int Column;
        public int Tier;
    }

    /// <summary>
    /// VR 内部真正使用的卸船作业项
    /// </summary>
    public class DischargeWorkItem
    {
        public CellCode Source;
        public YardTargetCode Target;
    }
}