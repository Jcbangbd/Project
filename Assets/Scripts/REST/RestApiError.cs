using System;

namespace Vtp.VR.Rest
{
    public sealed class RestApiError
    {
        public long HttpStatus { get; private set; }
        public int BusinessCode { get; private set; }
        public string Message { get; private set; }
        public string RequestId { get; private set; }
        public bool IsNetworkError { get; private set; }

        public bool IsUnauthorized
        {
            get { return HttpStatus == 401 || BusinessCode == 401; }
        }

        public RestApiError(
            long httpStatus,
            int businessCode,
            string message,
            string requestId,
            bool isNetworkError)
        {
            HttpStatus = httpStatus;
            BusinessCode = businessCode;
            Message = string.IsNullOrEmpty(message) ? "请求失败" : message;
            RequestId = requestId;
            IsNetworkError = isNetworkError;
        }

        public static RestApiError Business(int code, string message, string requestId)
        {
            return new RestApiError(200, code, message, requestId, false);
        }

        public static RestApiError Client(string message)
        {
            return new RestApiError(0, 0, message, null, false);
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(RequestId))
                return string.Format("{0}（requestId: {1}）", Message, RequestId);
            return Message;
        }
    }
}
