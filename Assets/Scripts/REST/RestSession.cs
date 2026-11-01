using System;

namespace Vtp.VR.Rest
{
    public static class RestSession
    {
        public static string Token { get; private set; }
        public static string UserId { get; private set; }
        public static string UserName { get; private set; }
        public static string RealName { get; private set; }
        public static string RoleId { get; private set; }
        public static string RoleName { get; private set; }

        public static bool IsAuthenticated
        {
            get { return !string.IsNullOrEmpty(Token); }
        }

        public static void Set(LoginResultDto login)
        {
            if (login == null || string.IsNullOrEmpty(login.token))
                throw new ArgumentException("登录结果中没有 token。", "login");

            Token = login.token;
            UserId = login.userId;
            UserName = login.userName;
            RealName = login.realName;
            RoleId = login.roleId;
            RoleName = login.roleName;
        }

        public static void Clear()
        {
            Token = null;
            UserId = null;
            UserName = null;
            RealName = null;
            RoleId = null;
            RoleName = null;
        }
    }
}
