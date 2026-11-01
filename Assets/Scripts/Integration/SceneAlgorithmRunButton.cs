using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using UnityEngine.UI;
using Vtp.VR.Rest;
using ZPMC.VR.Unity;

namespace Vtp.VR.Integration
{
    /// <summary>
    /// 挂在“开始调度”按钮上。
    /// 登录阶段只完成 REST 登录。
    /// 用户点击本按钮后依次执行：
    /// 1. 携带登录 Token 从远程 REST 接口获取算例；
    /// 2. 检查并等待本地 FastAPI/算法就绪；
    /// 3. 将远程算例送入本地算法；
    /// 4. 保存调度结果并进入 VR 执行场景；
    /// 5. 等待 A40 初始化，再创建目标箱位队列。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class SceneAlgorithmRunButton : MonoBehaviour
    {
        [Header("界面")]
        [SerializeField]
        private Button runButton;

        [SerializeField]
        private TMP_Text statusText;

        [Header("远程算例")]
        [Tooltip(
            "传给 findSceneNumericalExample 接口的 taskId，" +
            "例如 kc1。")]
        [SerializeField]
        private string courseTaskId = "kc1";

        [Header("调度成功后的场景")]
        [SerializeField]
        private bool enterVrSceneAfterSuccess = true;

        [SerializeField]
        private string vrSceneName = "SceneStage1";

        [SerializeField]
        private string requiredBlockName = "A40";

        [SerializeField]
        [Min(1f)]
        private float blockInitializationTimeoutSeconds = 60f;

        [Header("算法计算锁")]

        [Tooltip("独立的全屏计算遮罩Canvas")]
        [SerializeField]
        private GameObject calculationLockCanvas;

        [Tooltip("遮罩中显示进度的文字")]
        [SerializeField]
        private TMP_Text calculationLockText;

        [Tooltip("计算期间需要禁用的相机、移动和输入脚本")]
        [SerializeField]
        private Behaviour[] inputBehavioursToDisable;

        [Tooltip("算法计算期间是否暂停Unity场景时间")]
        [SerializeField]
        private bool pauseSceneWhileCalculating = true;

        [Header("算法成功后的原按钮操作")]

        [Tooltip("把按钮原来的ChangeWindow、Play、SetActive等操作移到这里")]
        [SerializeField]
        private UnityEvent onAlgorithmSucceeded;

        private bool _algorithmRunning;
        private float _timeScaleBeforeCalculation = 1f;

        private readonly Dictionary<Behaviour, bool>
            _inputBehaviourStates =
                new Dictionary<Behaviour, bool>();

        private SceneNumericalExampleService
            _sceneNumericalExampleService;

        private void Awake()
        {
            if (runButton == null)
                runButton = GetComponent<Button>();

            RestApiClient client =
                RestApiClient.EnsureInstance();

            _sceneNumericalExampleService =
                new SceneNumericalExampleService(client);

            if (calculationLockCanvas != null)
            {
                // 防止误把算法按钮放进遮罩
                if (calculationLockCanvas == gameObject ||
                    transform.IsChildOf(
                        calculationLockCanvas.transform))
                {
                    Debug.LogError(
                        "[SceneAlgorithmButton] " +
                        "AlgorithmButton不能是计算遮罩的子物体。");

                    calculationLockCanvas = null;
                }
                else
                {
                    calculationLockCanvas.SetActive(false);
                }
            }
        }

        private void OnEnable()
        {
            if (runButton != null)
                runButton.onClick.AddListener(
                    OnRunAlgorithmButtonClick);
        }

        private void OnDisable()
        {
            if (runButton != null)
                runButton.onClick.RemoveListener(
                    OnRunAlgorithmButtonClick);
        }

        public void OnRunAlgorithmButtonClick()
        {
            if (_algorithmRunning)
                return;

            if (!RestSession.IsAuthenticated)
            {
                ShowError(
                    "尚未登录远程 REST 服务器，" +
                    "或者登录 Token 已失效。");
                return;
            }

            string taskId =
                (courseTaskId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(taskId))
            {
                ShowError(
                    "任务编号为空，请在按钮组件的 " +
                    "Course Task Id 中填写任务编号。");
                return;
            }

            if (_sceneNumericalExampleService == null)
            {
                ShowError("远程算例服务尚未初始化。");
                return;
            }

            BeginCalculationLock("正在获取任务 " +taskId + " 的算例，请稍候……");

            _sceneNumericalExampleService.GetByTaskId(
                taskId,
                delegate(
                    SceneNumericalExampleDto[] examples)
                {
                    OnRemoteExamplesReceived(
                        taskId,
                        examples);
                },
                delegate (RestApiError error)
                {
                    FailCalculation(
                        error == null
                            ? "远程算例请求失败。"
                            : error.Message);
                });
        }

        /// <summary>
        /// 任务列表按钮可以在调用调度按钮前使用本方法设置 taskId。
        /// </summary>
        public void SetTaskId(string taskId)
        {
            courseTaskId =
                (taskId ?? string.Empty).Trim();
        }

        private void OnRemoteExamplesReceived(
            string taskId,
            SceneNumericalExampleDto[] examples)
        {
            if (examples == null || examples.Length == 0)
            {
                FailCalculation("远程服务器没有返回任务 " + taskId +  " 的算例。");
                return;
            }

            // 保存完整远程算例，供调试和后续场景读取。
            SceneNumericalExampleRuntime.Set(
                taskId,
                examples);

            Debug.Log(
                "[SceneAlgorithmButton] 远程算例获取成功，" +
                "taskId=" +
                taskId +
                "，数量=" +
                examples.Length);

            SceneExampleAlgorithmCoordinator coordinator =
                SceneExampleAlgorithmCoordinator.Instance;
            if (coordinator == null)
            {
                coordinator =
                    FindObjectOfType<
                        SceneExampleAlgorithmCoordinator>();
            }

            if (coordinator == null)
            {
                FailCalculation("没有找到 SceneExampleAlgorithmCoordinator。");
                return;
            }

            SetStatus(
                "已获取 " +
                examples.Length +
                " 条算例，正在检查本地算法并执行调度……");

            // 传入远端返回的全部算例，不截取 12/20/24 条。
            coordinator.Run(
                taskId,
                examples,
                delegate(SchedulingResultDto result)
                {
                    OnAlgorithmCompleted(result);
                },
                delegate(string progress)
                {
                    UpdateCalculationMessage(progress);
                },
                delegate(string error)
                {
                    FailCalculation(error);
                });
        }

        private void OnAlgorithmCompleted(
            SchedulingResultDto result)
        {
            int operationCount =
                result != null &&
                result.operations != null
                    ? result.operations.Length
                    : 0;

            if (operationCount <= 0)
            {
                FailCalculation(
                    "本地算法没有返回有效调度指令。");
                return;
            }

            UpdateCalculationMessage( "算法计算完成，共生成 " + operationCount + " 条调度指令。");

            EndCalculationLock();

            SetStatus( "调度完成，共生成 " + operationCount + " 条算法指令，正在执行下一步……");

            // 此时才执行按钮原来的操作
            if (onAlgorithmSucceeded != null)
                onAlgorithmSucceeded.Invoke();

            if (enterVrSceneAfterSuccess &&
                !string.IsNullOrWhiteSpace(vrSceneName))
            {
                // RestApiClient 是 DontDestroyOnLoad 对象。
                // 由它持有协程，按钮随旧场景销毁后协程仍会继续。
                RestApiClient coroutineOwner =
                    RestApiClient.EnsureInstance();
                coroutineOwner.StartCoroutine(
                    LoadSceneAndInstallTargets(
                        vrSceneName,
                        requiredBlockName,
                        blockInitializationTimeoutSeconds,
                        operationCount));
            }
        }

        private static IEnumerator LoadSceneAndInstallTargets(
            string sceneName,
            string blockName,
            float timeoutSeconds,
            int operationCount)
        {
            blockName =
                string.IsNullOrWhiteSpace(blockName)
                    ? "A40"
                    : blockName.Trim();

            AsyncOperation loadOperation =
                SceneManager.LoadSceneAsync(sceneName);
            if (loadOperation == null)
            {
                Debug.LogError(
                    "[SceneAlgorithmButton] 无法加载VR场景：" +
                    sceneName);
                yield break;
            }

            while (!loadOperation.isDone)
                yield return null;

            DischargeTargetSlotQueue.Clear();

            Debug.Log(
                "[SceneAlgorithmButton] VR场景已加载，" +
                "正在等待箱区初始化：" +
                blockName);

            float deadline =
                Time.realtimeSinceStartup +
                Mathf.Max(1f, timeoutSeconds);

            BlockBuffer block = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (BlockManager.Instance != null)
                {
                    block =
                        BlockManager.Instance.FindBlockBuffer(
                            blockName);
                    if (block != null)
                    {
                        break;
                    }
                }

                yield return null;
            }

            if (block == null)
            {
                Debug.LogError(
                    "[SceneAlgorithmButton] 等待超时，" +
                    "VR场景没有完成箱区初始化：" +
                    blockName);
                yield break;
            }

            // 确保场景初始堆场箱已完成装载，再检查实际占用。
            yield return null;

            List<DischargeTargetSlot> targets;
            string error;
            if (!AlgorithmDischargePlan.TryBuildTargets(
                    out targets,
                    out error))
            {
                Debug.LogError(
                    "[SceneAlgorithmButton] " +
                    "无法创建目标箱位队列：" +
                    error);
                yield break;
            }

            DischargeTargetSlotQueue.LoadTargets(targets);

            Debug.Log(
                "[SceneAlgorithmButton] 目标箱位队列已就绪，" +
                "调度指令数=" +
                operationCount +
                "，卸船目标箱位数=" +
                targets.Count +
                "，箱区=" +
                blockName);
        }

        private void SetBusy(bool busy)
        {
            if (runButton != null)
                runButton.interactable = !busy;
        }

        private void SetStatus(string message)
        {
            Debug.Log("[SceneAlgorithmButton] " + message);
            if (statusText != null)
                statusText.text = message;
        }

        private void ShowError(string message)
        {
            Debug.LogError(
                "[SceneAlgorithmButton] " + message);
            if (statusText != null)
                statusText.text = "调度失败：" + message;
        }


        //页面锁定方法
        private void BeginCalculationLock(string message)
        {
            if (_algorithmRunning)
                return;

            _algorithmRunning = true;

            // 禁止重复点击算法按钮
            SetBusy(true);

            // 显示全屏遮罩
            if (calculationLockCanvas != null)
                calculationLockCanvas.SetActive(true);

            UpdateCalculationMessage(message);

            // 保存原来的时间速度，然后暂停场景
            _timeScaleBeforeCalculation = Time.timeScale;

            if (pauseSceneWhileCalculating)
                Time.timeScale = 0f;

            // 临时关闭相机、移动等输入脚本
            _inputBehaviourStates.Clear();

            if (inputBehavioursToDisable == null)
                return;

            foreach (Behaviour behaviour in
                     inputBehavioursToDisable)
            {
                if (behaviour == null ||
                    behaviour == this ||
                    _inputBehaviourStates.ContainsKey(behaviour))
                {
                    continue;
                }

                _inputBehaviourStates.Add(
                    behaviour,
                    behaviour.enabled);

                behaviour.enabled = false;
            }
        }

        //解锁方法
        private void EndCalculationLock()
        {
            if (!_algorithmRunning)
                return;

            _algorithmRunning = false;

            // 恢复Unity时间速度
            if (pauseSceneWhileCalculating)
                Time.timeScale = _timeScaleBeforeCalculation;

            // 恢复相机、移动等输入脚本原来的状态
            foreach (
                KeyValuePair<Behaviour, bool> item
                in _inputBehaviourStates)
            {
                if (item.Key != null)
                    item.Key.enabled = item.Value;
            }

            _inputBehaviourStates.Clear();

            // 关闭遮罩
            if (calculationLockCanvas != null)
                calculationLockCanvas.SetActive(false);

            // 恢复算法按钮
            SetBusy(false);
        }

        //显示计算进度
        private void UpdateCalculationMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                message = "算法正在计算，请稍候……";

            SetStatus(message);

            if (calculationLockText != null)
                calculationLockText.text = message;
        }

        //统一处理失败
        private void FailCalculation(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                message = "算法运行失败，未返回具体错误。";

            EndCalculationLock();
            ShowError(message);
        }

        //防止场景意外销毁一直保持暂停
        private void OnDestroy()
        {
            if (_algorithmRunning)
                EndCalculationLock();
        }
    }
}
