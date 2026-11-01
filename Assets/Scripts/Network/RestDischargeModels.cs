using System;
using System.Collections.Generic;

namespace ZPMC.VR.Unity
{
    // ==================================================
    // 截图中的登录请求
    //
    // POST /vtp-server/system/login
    // ==================================================

    [Serializable]
    public class VtpLoginRequest
    {
        public string userName;

        public string password;

        public string loginFrom;
    }


    // ==================================================
    // 登录响应
    // ==================================================

    [Serializable]
    public class VtpLoginResponse
    {
        public int code;

        public string msg;

        public VtpLoginResult result;
    }


    [Serializable]
    public class VtpLoginResult
    {
        public string userId;

        public string userName;

        public string realName;

        public string token;
    }


    // ==================================================
    // 外部目标箱位算例响应
    //
    // 这里按你原来的 BoxNumber 输入设计
    // ==================================================

    [Serializable]
    public class DischargeCaseApiResponse
    {
        public int code;

        public string msg;

        public DischargeCaseApiResult result;
    }


    [Serializable]
    public class DischargeCaseApiResult
    {
        public string caseId;

        public List<string> boxNumbers;
    }
}