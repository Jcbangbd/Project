using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ZPMC.VR.Unity
{
    public class RestDischargeTargetClient
        : MonoBehaviour
    {
        public static RestDischargeTargetClient Instance
        {
            get;
            private set;
        }


        // ==================================================
        // 服务器
        // ==================================================

        [Header("REST Server")]

        [Tooltip(
            "填写截图中 Swagger 所属服务器的根地址，"
            + "不要包含最后的 API path")]
        [SerializeField]
        private string baseUrl =
            "http://YOUR-SERVER:PORT";


        // ==================================================
        // 截图中已经确认的接口
        // ==================================================

        [Header("Login API")]

        [SerializeField]
        private string loginPath =
            "/vtp-server/system/login";


        // ==================================================
        // 真正返回算例目标箱位的 API
        //
        // 当前截图未展示，
        // 所以必须由实际后端接口填写
        // ==================================================

        [Header("Discharge Target API")]

        [SerializeField]
        private string dischargeTargetPath =
            "/vtp-server/REPLACE-WITH-REAL-CASE-API";


        // ==================================================
        // 登录参数
        // ==================================================

        [Header("Login Account")]

        [SerializeField]
        private string userName = "";

        [SerializeField]
        private string password = "";

        [SerializeField]
        private string loginFrom = "web";


        // ==================================================
        // Token Header
        //
        // 按实际接口要求配置
        // ==================================================

        [Header("Token Header")]

        [SerializeField]
        private string tokenHeaderName =
            "Authorization";

        [SerializeField]
        private string tokenPrefix =
            "Bearer ";


        [Header("Request")]

        [SerializeField]
        private int timeoutSeconds = 10;


        // ==================================================
        // Runtime
        // ==================================================

        private string token;


        public bool HasToken
        {
            get
            {
                return !string.IsNullOrEmpty(
                    token);
            }
        }


        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;

                DontDestroyOnLoad(
                    gameObject);
            }
            else
            {
                Destroy(
                    gameObject);
            }
        }


        // ==================================================
        // 对 UMP_LevelInfo 暴露的唯一入口
        // ==================================================

        public IEnumerator RefreshDischargeTargets(
            Action<int> onSuccess,
            Action<string> onFailed)
        {
            // ------------------------------------------
            // 1. 没有 Token 时连接截图中的登录 API
            // ------------------------------------------

            if (!HasToken)
            {
                bool loginSuccess = false;

                string loginError = null;


                yield return StartCoroutine(
                    LoginCoroutine(
                        delegate
                        {
                            loginSuccess = true;
                        },
                        delegate (
                            string error)
                        {
                            loginSuccess = false;

                            loginError = error;
                        }));


                if (!loginSuccess)
                {
                    if (onFailed != null)
                    {
                        onFailed(
                            "VTP登录失败: "
                            + loginError);
                    }

                    yield break;
                }
            }


            // ------------------------------------------
            // 2. 获取目标箱位
            // ------------------------------------------

            yield return StartCoroutine(
                RequestDischargeTargetsCoroutine(
                    onSuccess,
                    onFailed));
        }


        // ==================================================
        // 截图中的：
        //
        // POST /vtp-server/system/login
        // ==================================================

        private IEnumerator LoginCoroutine(
            Action onSuccess,
            Action<string> onFailed)
        {
            VtpLoginRequest body =
                new VtpLoginRequest
                {
                    userName =
                        userName,

                    password =
                        password,

                    loginFrom =
                        loginFrom
                };


            string json =
                JsonUtility.ToJson(
                    body);


            byte[] bodyRaw =
                Encoding.UTF8.GetBytes(
                    json);


            string url =
                BuildUrl(
                    loginPath);


            Debug.Log(
                "[REST] Login: "
                + url);


            using (
                UnityWebRequest request =
                    new UnityWebRequest(
                        url,
                        UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler =
                    new UploadHandlerRaw(
                        bodyRaw);


                request.downloadHandler =
                    new DownloadHandlerBuffer();


                request.timeout =
                    timeoutSeconds;


                request.SetRequestHeader(
                    "Content-Type",
                    "application/json");


                request.SetRequestHeader(
                    "Accept",
                    "application/json");


                yield return
                    request.SendWebRequest();


#if UNITY_2020_2_OR_NEWER

                bool failed =
                    request.result
                    != UnityWebRequest.Result.Success;

#else

                bool failed =
                    request.isNetworkError
                    || request.isHttpError;

#endif


                if (failed)
                {
                    if (onFailed != null)
                    {
                        onFailed(
                            string.Format(
                                "HTTP={0}, Error={1}",
                                request.responseCode,
                                request.error));
                    }

                    yield break;
                }


                VtpLoginResponse response;

                try
                {
                    response =
                        JsonUtility.FromJson
                        <VtpLoginResponse>(
                            request
                                .downloadHandler
                                .text);
                }
                catch (Exception ex)
                {
                    if (onFailed != null)
                    {
                        onFailed(
                            "登录响应解析失败: "
                            + ex.Message);
                    }

                    yield break;
                }


                if (response == null)
                {
                    if (onFailed != null)
                    {
                        onFailed(
                            "登录响应为空");
                    }

                    yield break;
                }


                if (response.code != 200)
                {
                    if (onFailed != null)
                    {
                        onFailed(
                            "登录失败: "
                            + response.msg);
                    }

                    yield break;
                }


                if (response.result == null)
                {
                    if (onFailed != null)
                    {
                        onFailed(
                            "登录响应缺少 result");
                    }

                    yield break;
                }


                if (string.IsNullOrEmpty(
                    response.result.token))
                {
                    if (onFailed != null)
                    {
                        onFailed(
                            "登录响应没有 token");
                    }

                    yield break;
                }


                token =
                    response.result.token;


                Debug.Log(
                    "[REST] VTP login success");


                if (onSuccess != null)
                {
                    onSuccess();
                }
            }
        }


        // ==================================================
        // 获取卸船目标箱位
        // ==================================================

        private IEnumerator
            RequestDischargeTargetsCoroutine(
                Action<int> onSuccess,
                Action<string> onFailed)
        {
            string url =
                BuildUrl(
                    dischargeTargetPath);


            Debug.Log(
                "[REST] Request discharge targets: "
                + url);


            // 当前按 GET 示例
            //
            // 如果实际 Swagger 是 POST，
            // 只修改这个方法即可
            using (
                UnityWebRequest request =
                    UnityWebRequest.Get(
                        url))
            {
                request.timeout =
                    timeoutSeconds;


                request.SetRequestHeader(
                    "Accept",
                    "application/json");


                // --------------------------------------
                // 携带登录 Token
                // --------------------------------------

                if (!string.IsNullOrEmpty(
                    token))
                {
                    request.SetRequestHeader(
                        tokenHeaderName,
                        tokenPrefix + token);
                }


                yield return
                    request.SendWebRequest();


#if UNITY_2020_2_OR_NEWER

                bool failed =
                    request.result
                    != UnityWebRequest.Result.Success;

#else

                bool failed =
                    request.isNetworkError
                    || request.isHttpError;

#endif


                if (failed)
                {
                    if (onFailed != null)
                    {
                        onFailed(
                            string.Format(
                                "目标箱位请求失败。"
                                + "HTTP={0}, Error={1}",
                                request.responseCode,
                                request.error));
                    }

                    yield break;
                }


                string responseText =
                    request
                        .downloadHandler
                        .text;


                Debug.Log(
                    "[REST] Target response: "
                    + responseText);


                // --------------------------------------
                // JSON
                // --------------------------------------

                DischargeCaseApiResponse response;

                try
                {
                    response =
                        JsonUtility.FromJson
                        <DischargeCaseApiResponse>(
                            responseText);
                }
                catch (Exception ex)
                {
                    if (onFailed != null)
                    {
                        onFailed(
                            "目标算例JSON解析失败: "
                            + ex.Message);
                    }

                    yield break;
                }


                if (response == null)
                {
                    if (onFailed != null)
                    {
                        onFailed(
                            "目标算例响应为空");
                    }

                    yield break;
                }


                if (response.code != 200)
                {
                    if (onFailed != null)
                    {
                        onFailed(
                            "目标算例接口失败: "
                            + response.msg);
                    }

                    yield break;
                }


                if (response.result == null)
                {
                    if (onFailed != null)
                    {
                        onFailed(
                            "目标算例缺少 result");
                    }

                    yield break;
                }


                if (response.result.boxNumbers == null)
                {
                    if (onFailed != null)
                    {
                        onFailed(
                            "目标算例缺少 boxNumbers");
                    }

                    yield break;
                }


                // --------------------------------------
                // BoxNumber
                // ↓
                // DischargeTargetSlot
                // --------------------------------------

                List<DischargeTargetSlot> targets =
                    new List<DischargeTargetSlot>();


                for (
                    int i = 0;
                    i < response.result.boxNumbers.Count;
                    i++)
                {
                    string boxNumber =
                        response.result
                            .boxNumbers[i];


                    DischargeTargetSlot target;

                    string error;


                    if (!DischargeTargetSlotParser
                        .TryParse(
                            boxNumber,
                            out target,
                            out error))
                    {
                        if (onFailed != null)
                        {
                            onFailed(
                                "第 "
                                + (i + 1)
                                + " 个目标箱位错误: "
                                + error);
                        }

                        yield break;
                    }


                    targets.Add(
                        target);
                }


                // --------------------------------------
                // 核心：
                //
                // REST 算例写入你现有队列
                // --------------------------------------

                DischargeTargetSlotQueue
                    .LoadTargets(
                        targets);


                Debug.Log(
                    string.Format(
                        "[REST] 已装载 {0} 个外部卸船目标箱位",
                        targets.Count));


                if (onSuccess != null)
                {
                    onSuccess(
                        targets.Count);
                }
            }
        }


        private string BuildUrl(
            string path)
        {
            string root =
                baseUrl.TrimEnd('/');


            string apiPath =
                path.StartsWith("/")
                ? path
                : "/" + path;


            return root + apiPath;
        }
    }
}