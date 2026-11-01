using System;
using UnityEngine;

namespace Vtp.VR.Rest
{
    /// <summary>
    /// 从远程业务服务器取得任务所绑定算法的一次性下载票据。
    /// 远程Token只保留在Unity REST客户端，不传给本地Python服务。
    /// </summary>
    public sealed class AlgorithmCatalogService
    {
        private const string Root = "/vr-api/v1/algorithms";
        private readonly RestApiClient _client;

        public AlgorithmCatalogService(RestApiClient client)
        {
            if (client == null)
                throw new ArgumentNullException("client");
            _client = client;
        }

        public void GetDownloadTicket(
            AlgorithmReferenceDto algorithm,
            Action<AlgorithmDownloadTicketDto> onSuccess,
            Action<RestApiError> onError)
        {
            if (algorithm == null ||
                string.IsNullOrWhiteSpace(algorithm.algorithmId) ||
                string.IsNullOrWhiteSpace(algorithm.version) ||
                string.IsNullOrWhiteSpace(algorithm.sha256))
            {
                InvokeError(
                    onError,
                    RestApiError.Client(
                        "任务绑定的算法信息不完整。"));
                return;
            }

            string path =
                Root + "/" +
                Uri.EscapeDataString(algorithm.algorithmId.Trim()) +
                "/versions/" +
                Uri.EscapeDataString(algorithm.version.Trim()) +
                "/download-ticket";

            _client.PostJson(
                path,
                null,
                true,
                true,
                delegate(string body)
                {
                    AlgorithmDownloadTicketEnvelope envelope;
                    try
                    {
                        envelope =
                            JsonUtility.FromJson<
                                AlgorithmDownloadTicketEnvelope>(body);
                    }
                    catch (Exception exception)
                    {
                        InvokeError(
                            onError,
                            RestApiError.Client(
                                "算法下载票据响应无法解析：" +
                                exception.Message));
                        return;
                    }

                    if (envelope == null ||
                        envelope.code != 200 ||
                        envelope.result == null)
                    {
                        InvokeError(
                            onError,
                            RestApiError.Business(
                                envelope == null ? 0 : envelope.code,
                                envelope == null
                                    ? "算法下载票据响应为空。"
                                    : envelope.msg,
                                envelope == null
                                    ? null
                                    : envelope.requestId));
                        return;
                    }

                    AlgorithmDownloadTicketDto ticket =
                        envelope.result;
                    if (!MatchesReference(algorithm, ticket))
                    {
                        InvokeError(
                            onError,
                            RestApiError.Client(
                                "算法下载票据与任务绑定版本不一致。"));
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(ticket.downloadUrl))
                    {
                        InvokeError(
                            onError,
                            RestApiError.Client(
                                "服务器没有返回算法下载地址。"));
                        return;
                    }

                    if (onSuccess != null)
                        onSuccess(ticket);
                },
                onError);
        }

        private static bool MatchesReference(
            AlgorithmReferenceDto expected,
            AlgorithmDownloadTicketDto actual)
        {
            return actual != null &&
                   string.Equals(
                       expected.algorithmId,
                       actual.algorithmId,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       expected.version,
                       actual.version,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       NormalizeHash(expected.sha256),
                       NormalizeHash(actual.sha256),
                       StringComparison.OrdinalIgnoreCase) &&
                   (string.IsNullOrWhiteSpace(expected.signature) ||
                    string.Equals(
                        expected.signature.Trim(),
                        (actual.signature ?? string.Empty).Trim(),
                        StringComparison.Ordinal)) &&
                   (expected.fileSize <= 0 ||
                    actual.fileSize <= 0 ||
                    expected.fileSize == actual.fileSize);
        }

        private static string NormalizeHash(string value)
        {
            return (value ?? string.Empty)
                .Replace("-", string.Empty)
                .Trim()
                .ToLowerInvariant();
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
