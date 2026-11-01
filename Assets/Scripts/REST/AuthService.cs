using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Vtp.VR.Rest
{
    public sealed class AuthService
    {
        private readonly RestApiClient _client;

        public AuthService(RestApiClient client)
        {
            if (client == null)
                throw new ArgumentNullException("client");
            _client = client;
        }

        public void Login(
            string userName,
            string password,
            Action<LoginResultDto> onSuccess,
            Action<RestApiError> onError)
        {
            LoginRequestDto request = new LoginRequestDto
            {
                userName = (userName ?? string.Empty).Trim(),

                password = ComputeMd5(password),

                loginFrom = _client.Settings.loginFrom
            };

            _client.PostJson(
                "/system/login",
                request,
                false,
                false,
                delegate(string body)
                {
                    LoginResponseEnvelope envelope;
                    try
                    {
                        envelope = JsonUtility.FromJson<LoginResponseEnvelope>(body);
                    }
                    catch (Exception ex)
                    {
                        InvokeError(onError,
                            RestApiError.Client("登录响应无法解析：" + ex.Message));
                        return;
                    }

                    if (envelope == null)
                    {
                        InvokeError(onError,
                            RestApiError.Client("登录响应为空。"));
                        return;
                    }

                    if (envelope.code != 200)
                    {
                        InvokeError(onError, RestApiError.Business(
                            envelope.code, envelope.msg, envelope.requestId));
                        return;
                    }

                    if (envelope.result == null ||
                        string.IsNullOrEmpty(envelope.result.token))
                    {
                        InvokeError(onError,
                            RestApiError.Client("服务器登录成功响应中没有 token。"));
                        return;
                    }

                    RestSession.Set(envelope.result);
                    if (onSuccess != null)
                        onSuccess(envelope.result);
                },
                onError);
        }

        private static string ComputeMd5(
    string plainText)
        {
            if (plainText == null)
            {
                plainText = string.Empty;
            }

            byte[] inputBytes =
                Encoding.UTF8.GetBytes(plainText);

            using (MD5 md5 = MD5.Create())
            {
                byte[] hashBytes =
                    md5.ComputeHash(inputBytes);

                StringBuilder builder =
                    new StringBuilder(
                        hashBytes.Length * 2);

                for (int index = 0;
                     index < hashBytes.Length;
                     index++)
                {
                    builder.Append(
                        hashBytes[index].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        public void ChangePassword(
            string oldPassword,
            string newPassword,
            Action onSuccess,
            Action<RestApiError> onError)
        {
            ChangePasswordRequestDto request = new ChangePasswordRequestDto
            {
                oldPassword = oldPassword,
                newPassword = newPassword
            };

            _client.PostJson(
                "/system/changePwd",
                request,
                true,
                false,
                delegate(string body)
                {
                    // 现有 OpenAPI 对 changePwd 的 200 响应未声明响应体；
                    // HTTP 已成功且响应体为空时按成功处理。
                    if (string.IsNullOrEmpty(body) ||
                        string.IsNullOrEmpty(body.Trim()))
                    {
                        if (onSuccess != null)
                            onSuccess();
                        return;
                    }

                    ApiEmptyEnvelope envelope;
                    try
                    {
                        envelope = JsonUtility.FromJson<ApiEmptyEnvelope>(body);
                    }
                    catch (Exception ex)
                    {
                        InvokeError(onError, RestApiError.Client(
                            "修改密码响应无法解析：" + ex.Message));
                        return;
                    }

                    if (envelope != null && envelope.code == 200)
                    {
                        if (onSuccess != null)
                            onSuccess();
                        return;
                    }

                    InvokeError(onError, RestApiError.Business(
                        envelope == null ? 0 : envelope.code,
                        envelope == null ? "修改密码响应为空。" : envelope.msg,
                        envelope == null ? null : envelope.requestId));
                },
                onError);
        }

        public void Logout(Action onComplete)
        {
            if (!RestSession.IsAuthenticated)
            {
                RemoteTaskRuntime.Clear();
                if (onComplete != null)
                    onComplete();
                return;
            }

            _client.PostJson(
                "/system/logout",
                null,
                true,
                false,
                delegate(string ignored)
                {
                    RestSession.Clear();
                    RemoteTaskRuntime.Clear();
                    if (onComplete != null)
                        onComplete();
                },
                delegate(RestApiError ignored)
                {
                    // 本地必须结束会话，不能因为服务器登出失败继续保留 token。
                    RestSession.Clear();
                    RemoteTaskRuntime.Clear();
                    if (onComplete != null)
                        onComplete();
                });
        }

        private static void InvokeError(
            Action<RestApiError> onError,
            RestApiError error)
        {
            if (onError != null)
                onError(error);
        }
    }
}
