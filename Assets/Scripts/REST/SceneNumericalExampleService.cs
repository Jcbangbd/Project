using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vtp.VR.Rest
{
    /// <summary>
    /// 服务器返回的单条场景算例。
    /// </summary>
    [Serializable]
    public sealed class SceneNumericalExampleDto
    {
        public string id;

        public string courseTaskId;

        public string no;

        public string deptId;

        /// <summary>
        /// 轨道吊名称，例如海侧ARMG。
        /// </summary>
        public string argm;

        /// <summary>
        /// 作业类型，例如翻箱(堆场->支架)。
        /// </summary>
        public string type;

        public float startPointPosition;

        public float startList;

        public float endPointPosition;

        public float endList;

        public float time_consuming;
    }

    /// <summary>
    /// findSceneNumericalExample统一响应。
    /// </summary>
    [Serializable]
    public sealed class SceneNumericalExampleEnvelope
    {
        public int code;

        public string msg;

        public SceneNumericalExampleDto[] result;

        public string requestId;
    }

    /// <summary>
    /// 调用服务器真实存在的算例接口。
    /// </summary>
    public sealed class SceneNumericalExampleService
    {
        private const string Path =
            "/sceneNumericalExample/" +
            "findSceneNumericalExample";

        private readonly RestApiClient _client;

        public SceneNumericalExampleService(
            RestApiClient client)
        {
            if (client == null)
            {
                throw new ArgumentNullException("client");
            }

            _client = client;
        }

        public void GetByTaskId(
            string taskId,
            Action<SceneNumericalExampleDto[]> onSuccess,
            Action<RestApiError> onError)
        {
            string normalizedTaskId =
                (taskId ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(normalizedTaskId))
            {
                InvokeError(
                    onError,
                    RestApiError.Client(
                        "查询算例时taskId不能为空。"));

                return;
            }

            Dictionary<string, string> fields =
                new Dictionary<string, string>();

            fields.Add(
                "taskId",
                normalizedTaskId);

            _client.PostForm(
                Path,
                fields,

                // true：自动添加登录返回的Token
                true,

                // 可以对网络错误和5xx进行重试
                true,

                delegate (string responseBody)
                {
                    HandleResponse(
                        responseBody,
                        onSuccess,
                        onError);
                },
                onError);
        }

        private static void HandleResponse(
            string responseBody,
            Action<SceneNumericalExampleDto[]> onSuccess,
            Action<RestApiError> onError)
        {
            SceneNumericalExampleEnvelope envelope;

            try
            {
                envelope =
                    JsonUtility.FromJson<
                        SceneNumericalExampleEnvelope>(
                        responseBody);
            }
            catch (Exception exception)
            {
                InvokeError(
                    onError,
                    RestApiError.Client(
                        "算例响应JSON无法解析：" +
                        exception.Message));

                return;
            }

            if (envelope == null)
            {
                InvokeError(
                    onError,
                    RestApiError.Client(
                        "服务器返回的算例响应为空。"));

                return;
            }

            if (envelope.code != 200)
            {
                InvokeError(
                    onError,
                    RestApiError.Business(
                        envelope.code,
                        string.IsNullOrWhiteSpace(
                            envelope.msg)
                            ? "获取算例失败。"
                            : envelope.msg,
                        envelope.requestId));

                return;
            }

            SceneNumericalExampleDto[] examples =
                envelope.result ??
                new SceneNumericalExampleDto[0];

            if (onSuccess != null)
            {
                onSuccess(examples);
            }
        }

        private static void InvokeError(
            Action<RestApiError> onError,
            RestApiError error)
        {
            if (onError != null)
            {
                onError(error);
            }
        }
    }
}