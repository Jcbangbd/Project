using System;
using UnityEngine;

namespace Vtp.VR.Rest
{
    /// <summary>
    /// 复用服务器现有 sysFile 接口，通过 fileId 取得真实下载地址并下载文件。
    /// </summary>
    public sealed class RemoteFileService
    {
        private readonly RestApiClient _client;

        public RemoteFileService(RestApiClient client)
        {
            if (client == null)
                throw new ArgumentNullException("client");
            _client = client;
        }

        public void DownloadByFileId(
            string fileId,
            Action<byte[]> onSuccess,
            Action<RestApiError> onError)
        {
            if (string.IsNullOrWhiteSpace(fileId))
            {
                InvokeError(onError, RestApiError.Client("fileId 不能为空。"));
                return;
            }

            string path = "/sysFile/downloadFile?fileId=" +
                          Uri.EscapeDataString(fileId.Trim());

            _client.PostJson(
                path,
                null,
                true,
                true,
                delegate(string body)
                {
                    string downloadUrl;
                    string parseError;
                    if (!TryParseDownloadUrl(
                            body,
                            out downloadUrl,
                            out parseError))
                    {
                        InvokeError(
                            onError,
                            RestApiError.Client(parseError));
                        return;
                    }

                    _client.DownloadBytes(downloadUrl, onSuccess, onError);
                },
                onError);
        }

        private static bool TryParseDownloadUrl(
            string body,
            out string downloadUrl,
            out string error)
        {
            downloadUrl = null;
            error = null;

            if (string.IsNullOrWhiteSpace(body))
            {
                error = "服务器返回的文件下载地址为空。";
                return false;
            }

            try
            {
                DownloadUrlObjectEnvelope objectEnvelope =
                    JsonUtility.FromJson<DownloadUrlObjectEnvelope>(body);
                if (objectEnvelope != null &&
                    objectEnvelope.code == 200 &&
                    objectEnvelope.result != null &&
                    !string.IsNullOrWhiteSpace(
                        objectEnvelope.result.downloadUrl))
                {
                    downloadUrl =
                        objectEnvelope.result.downloadUrl.Trim();
                    return true;
                }
            }
            catch
            {
                // 继续尝试字符串包装格式。
            }

            try
            {
                DownloadUrlStringEnvelope stringEnvelope =
                    JsonUtility.FromJson<DownloadUrlStringEnvelope>(body);
                if (stringEnvelope != null &&
                    stringEnvelope.code == 200 &&
                    !string.IsNullOrWhiteSpace(stringEnvelope.result))
                {
                    downloadUrl = stringEnvelope.result.Trim();
                    return true;
                }

                if (stringEnvelope != null &&
                    stringEnvelope.code != 0 &&
                    stringEnvelope.code != 200)
                {
                    error = string.IsNullOrWhiteSpace(stringEnvelope.msg)
                        ? "服务器拒绝了文件下载请求。"
                        : stringEnvelope.msg;
                    return false;
                }
            }
            catch
            {
                // 继续尝试纯字符串。
            }

            string value = body.Trim();
            if (value.Length >= 2 &&
                value[0] == '"' &&
                value[value.Length - 1] == '"')
            {
                value = value.Substring(1, value.Length - 2)
                    .Replace("\\/", "/")
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\");
            }

            if (Uri.IsWellFormedUriString(
                    value,
                    UriKind.RelativeOrAbsolute))
            {
                downloadUrl = value;
                return true;
            }

            error = "服务器没有返回有效的文件下载地址。";
            return false;
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
