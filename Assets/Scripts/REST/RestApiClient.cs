using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;


namespace Vtp.VR.Rest
{
    public sealed class RestApiClient : MonoBehaviour
    {
        private const string ConfigFileName = "rest-api-config.json";
        private static RestApiClient _instance;

        [SerializeField]
        private RestApiSettings settings = new RestApiSettings();

        public static RestApiClient Instance
        {
            get { return _instance; }
        }

        public RestApiSettings Settings
        {
            get { return settings; }
        }

        public static RestApiClient EnsureInstance()
        {
            if (_instance != null)
                return _instance;

            _instance = FindObjectOfType<RestApiClient>();
            if (_instance != null)
                return _instance;

            GameObject root = new GameObject("RestApiClient");
            _instance = root.AddComponent<RestApiClient>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettingsFromStreamingAssets();
        }

        private void LoadSettingsFromStreamingAssets()
        {
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, ConfigFileName);
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    JsonUtility.FromJsonOverwrite(json, settings);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("REST 配置读取失败，将使用 Inspector/默认配置：" + ex.Message);
            }

            settings.baseUrl = (settings.baseUrl ?? string.Empty).Trim().TrimEnd('/');
            if (settings.timeoutSeconds <= 0)
                settings.timeoutSeconds = 20;
            if (settings.maxRetries < 0)
                settings.maxRetries = 0;
        }

        public Coroutine Get(
            string relativePath,
            Action<string> onSuccess,
            Action<RestApiError> onError)
        {
            return StartCoroutine(SendJsonRoutine(
                UnityWebRequest.kHttpVerbGET,
                relativePath,
                null,
                true,
                true,
                onSuccess,
                onError));
        }

        public Coroutine PostJson(
            string relativePath,
            object body,
            bool requiresAuthentication,
            bool canRetry,
            Action<string> onSuccess,
            Action<RestApiError> onError)
        {
            string json = body == null ? "{}" : JsonUtility.ToJson(body);
            return StartCoroutine(SendJsonRoutine(
                UnityWebRequest.kHttpVerbPOST,
                relativePath,
                json,
                requiresAuthentication,
                canRetry,
                onSuccess,
                onError));
        }

        public Coroutine PostForm(
            string relativePath,
            IDictionary<string, string> fields,
            bool requiresAuthentication,
            bool canRetry,
            Action<string> onSuccess,
            Action<RestApiError> onError)
                {
                    return StartCoroutine(
                        SendFormRoutine(
                    relativePath,
                    fields,
                    requiresAuthentication,
                    canRetry,
                    onSuccess,
                    onError));
        }

        public Coroutine DownloadBytes(
            string urlOrPath,
            Action<byte[]> onSuccess,
            Action<RestApiError> onError)
        {
            return StartCoroutine(DownloadBytesRoutine(urlOrPath, onSuccess, onError));
        }

        public string BuildAbsoluteUrl(string urlOrPath)
        {
            if (string.IsNullOrEmpty(urlOrPath))
                throw new ArgumentException("URL 不能为空。", "urlOrPath");

            Uri absolute;
            if (Uri.TryCreate(urlOrPath, UriKind.Absolute, out absolute))
                return absolute.AbsoluteUri;

            string baseUrl = (settings.baseUrl ?? string.Empty).Trim().TrimEnd('/');
            string relativePath = urlOrPath.Trim().TrimStart('/');
            return baseUrl + "/" + relativePath;
        }

        private IEnumerator SendJsonRoutine(
            string method,
            string relativePath,
            string jsonBody,
            bool requiresAuthentication,
            bool canRetry,
            Action<string> onSuccess,
            Action<RestApiError> onError)
        {
            if (requiresAuthentication && !RestSession.IsAuthenticated)
            {
                InvokeError(onError, RestApiError.Client("尚未登录或登录状态已失效。"));
                yield break;
            }

            string url;
            try
            {
                url = BuildAbsoluteUrl(relativePath);
            }
            catch (Exception ex)
            {
                InvokeError(onError, RestApiError.Client(ex.Message));
                yield break;
            }

            string requestId = Guid.NewGuid().ToString("N");
            int attempts = canRetry ? settings.maxRetries + 1 : 1;

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                using (UnityWebRequest request = CreateJsonRequest(
                    method, url, jsonBody, requiresAuthentication, requestId))
                {
                    UnityWebRequestAsyncOperation operation;
                    try
                    {
                        operation = request.SendWebRequest();
                    }
                    catch (Exception ex)
                    {
                        InvokeError(onError, new RestApiError(
                            0, 0, ex.Message, requestId, true));
                        yield break;
                    }

                    yield return operation;

                    bool failed = HasTransportOrHttpError(request);
                    long status = request.responseCode;
                    string responseText = request.downloadHandler == null
                        ? string.Empty
                        : request.downloadHandler.text;

                    if (!failed && status >= 200 && status < 300)
                    {
                        if (onSuccess != null)
                            onSuccess(responseText);
                        yield break;
                    }

                    bool networkError = status == 0;
                    if (attempt + 1 < attempts &&
                        (networkError || IsRetriableHttpStatus(status)))
                    {
                        yield return new WaitForSecondsRealtime(
                            Mathf.Min(4f, Mathf.Pow(2f, attempt)));
                        continue;
                    }

                    RestApiError error = BuildError(
                        request, responseText, requestId, networkError);
                    if (error.IsUnauthorized)
                        RestSession.Clear();
                    InvokeError(onError, error);
                    yield break;
                }
            }
        }

        private IEnumerator DownloadBytesRoutine(
            string urlOrPath,
            Action<byte[]> onSuccess,
            Action<RestApiError> onError)
        {
            string url;
            try
            {
                url = BuildAbsoluteUrl(urlOrPath);
            }
            catch (Exception ex)
            {
                InvokeError(onError, RestApiError.Client(ex.Message));
                yield break;
            }

            string requestId = Guid.NewGuid().ToString("N");
            int attempts = settings.maxRetries + 1;
            bool attachAuth = IsSameServer(url) && RestSession.IsAuthenticated;

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    request.timeout = settings.timeoutSeconds;
                    request.SetRequestHeader("Accept", "application/octet-stream");
                    request.SetRequestHeader("X-Request-Id", requestId);
                    if (attachAuth)
                        ApplyAuthentication(request);

                    UnityWebRequestAsyncOperation operation;
                    try
                    {
                        operation = request.SendWebRequest();
                    }
                    catch (Exception ex)
                    {
                        InvokeError(onError, new RestApiError(
                            0, 0, ex.Message, requestId, true));
                        yield break;
                    }

                    yield return operation;

                    bool failed = HasTransportOrHttpError(request);
                    long status = request.responseCode;
                    if (!failed && status >= 200 && status < 300)
                    {
                        if (onSuccess != null)
                            onSuccess(request.downloadHandler.data);
                        yield break;
                    }

                    bool networkError = status == 0;
                    if (attempt + 1 < attempts &&
                        (networkError || IsRetriableHttpStatus(status)))
                    {
                        yield return new WaitForSecondsRealtime(
                            Mathf.Min(4f, Mathf.Pow(2f, attempt)));
                        continue;
                    }

                    string responseText = request.downloadHandler == null
                        ? string.Empty
                        : request.downloadHandler.text;
                    RestApiError error = BuildError(
                        request, responseText, requestId, networkError);
                    if (error.IsUnauthorized)
                        RestSession.Clear();
                    InvokeError(onError, error);
                    yield break;
                }
            }
        }

        private IEnumerator SendFormRoutine(
    string relativePath,
    IDictionary<string, string> fields,
    bool requiresAuthentication,
    bool canRetry,
    Action<string> onSuccess,
    Action<RestApiError> onError)
        {
            if (requiresAuthentication &&
                !RestSession.IsAuthenticated)
            {
                InvokeError(
                    onError,
                    RestApiError.Client(
                        "尚未登录或登录状态已经失效。"));
                yield break;
            }

            string url;

            try
            {
                url = BuildAbsoluteUrl(relativePath);
            }
            catch (Exception exception)
            {
                InvokeError(
                    onError,
                    RestApiError.Client(exception.Message));
                yield break;
            }

            string formBody;

            try
            {
                formBody = BuildFormBody(fields);
            }
            catch (Exception exception)
            {
                InvokeError(
                    onError,
                    RestApiError.Client(
                        "表单参数创建失败：" +
                        exception.Message));
                yield break;
            }

            string requestId =
                Guid.NewGuid().ToString("N");

            int attempts = canRetry
                ? settings.maxRetries + 1
                : 1;

            for (int attempt = 0;
                 attempt < attempts;
                 attempt++)
            {
                byte[] bodyBytes =
                    Encoding.UTF8.GetBytes(formBody);

                using (UnityWebRequest request =
                       new UnityWebRequest(
                           url,
                           UnityWebRequest.kHttpVerbPOST))
                {
                    request.uploadHandler =
                        new UploadHandlerRaw(bodyBytes);

                    request.downloadHandler =
                        new DownloadHandlerBuffer();

                    request.timeout =
                        settings.timeoutSeconds;

                    request.SetRequestHeader(
                        "Content-Type",
                        "application/x-www-form-urlencoded; charset=utf-8");

                    request.SetRequestHeader(
                        "Accept",
                        "application/json");

                    request.SetRequestHeader(
                        "X-Request-Id",
                        requestId);

                    // 登录成功后的远程接口必须添加Token
                    if (requiresAuthentication)
                    {
                        ApplyAuthentication(request);
                    }

                    Debug.Log(
                        "[REST] POST FORM " + url);

                    UnityWebRequestAsyncOperation operation;

                    try
                    {
                        operation = request.SendWebRequest();
                    }
                    catch (Exception exception)
                    {
                        InvokeError(
                            onError,
                            new RestApiError(
                                0,
                                0,
                                exception.Message,
                                requestId,
                                true));

                        yield break;
                    }

                    yield return operation;

                    bool failed =
                        HasTransportOrHttpError(request);

                    long status =
                        request.responseCode;

                    string responseText =
                        request.downloadHandler == null
                            ? string.Empty
                            : request.downloadHandler.text;

                    if (!failed &&
                        status >= 200 &&
                        status < 300)
                    {
                        if (onSuccess != null)
                        {
                            onSuccess(responseText);
                        }

                        yield break;
                    }

                    bool networkError = status == 0;

                    if (attempt + 1 < attempts &&
                        (networkError ||
                         IsRetriableHttpStatus(status)))
                    {
                        yield return
                            new WaitForSecondsRealtime(
                                Mathf.Min(
                                    4f,
                                    Mathf.Pow(2f, attempt)));

                        continue;
                    }

                    RestApiError error = BuildError(
                        request,
                        responseText,
                        requestId,
                        networkError);

                    if (error.IsUnauthorized)
                    {
                        RestSession.Clear();
                    }

                    InvokeError(onError, error);
                    yield break;
                }
            }
        }

        private static string BuildFormBody(
            IDictionary<string, string> fields)
        {
            if (fields == null || fields.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder builder =
                new StringBuilder();

            foreach (KeyValuePair<string, string> field
                     in fields)
            {
                if (string.IsNullOrWhiteSpace(field.Key))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('&');
                }

                builder.Append(
                    UnityWebRequest.EscapeURL(
                        field.Key.Trim()));

                builder.Append('=');

                builder.Append(
                    UnityWebRequest.EscapeURL(
                        field.Value ?? string.Empty));
            }

            return builder.ToString();
        }

        private UnityWebRequest CreateJsonRequest(
            string method,
            string url,
            string jsonBody,
            bool requiresAuthentication,
            string requestId)
        {
            UnityWebRequest request = new UnityWebRequest(url, method);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = settings.timeoutSeconds;
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("X-Request-Id", requestId);

            if (jsonBody != null)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bytes);
                request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            }

            if (requiresAuthentication)
                ApplyAuthentication(request);

            return request;
        }

        private void ApplyAuthentication(UnityWebRequest request)
        {
            if (!RestSession.IsAuthenticated ||
                string.IsNullOrEmpty(settings.authHeaderName))
                return;

            string value = string.IsNullOrEmpty(settings.authScheme)
                ? RestSession.Token
                : settings.authScheme.Trim() + " " + RestSession.Token;
            request.SetRequestHeader(settings.authHeaderName.Trim(), value);
        }

        private bool IsSameServer(string absoluteUrl)
        {
            try
            {
                Uri target = new Uri(absoluteUrl, UriKind.Absolute);
                Uri server = new Uri(settings.baseUrl, UriKind.Absolute);
                return string.Equals(target.Scheme, server.Scheme,
                           StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(target.Host, server.Host,
                           StringComparison.OrdinalIgnoreCase) &&
                       target.Port == server.Port;
            }
            catch
            {
                return false;
            }
        }

        private static bool HasTransportOrHttpError(UnityWebRequest request)
        {
#if UNITY_2020_2_OR_NEWER
            return request.result == UnityWebRequest.Result.ConnectionError ||
                   request.result == UnityWebRequest.Result.ProtocolError ||
                   request.result == UnityWebRequest.Result.DataProcessingError;
#else
            return request.isNetworkError || request.isHttpError;
#endif
        }

        private static bool IsRetriableHttpStatus(long status)
        {
            return status == 408 || status == 429 || status >= 500;
        }

        private static RestApiError BuildError(
            UnityWebRequest request,
            string responseText,
            string fallbackRequestId,
            bool networkError)
        {
            int code = 0;
            string message = request.error;
            string requestId = fallbackRequestId;

            if (!string.IsNullOrEmpty(responseText))
            {
                try
                {
                    ApiErrorEnvelope envelope =
                        JsonUtility.FromJson<ApiErrorEnvelope>(responseText);
                    if (envelope != null)
                    {
                        code = envelope.code;
                        if (!string.IsNullOrEmpty(envelope.msg))
                            message = envelope.msg;
                        if (!string.IsNullOrEmpty(envelope.requestId))
                            requestId = envelope.requestId;
                    }
                }
                catch
                {
                    // 返回体不是约定 JSON 时，不把整段服务器内容写入日志。
                }
            }

            return new RestApiError(
                request.responseCode,
                code,
                message,
                requestId,
                networkError);
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
