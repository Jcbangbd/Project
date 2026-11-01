using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Vtp.VR.Integration
{
    /// <summary>
    /// 为新增的任务、答题、算法和结果核对页面提供独立 Canvas。
    /// 每个场景只创建一个，不把这些页面挂到项目原有 Canvas 中。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public sealed class ExperimentAssessmentCanvasHost : MonoBehaviour
    {
        public const string CanvasName = "ExperimentAssessmentCanvas";
        public const string TaskSelectionLayer = "TaskSelectionLayer";
        public const string QuestionnaireLayer = "QuestionnaireLayer";
        public const string AlgorithmLayer = "AlgorithmAssessmentLayer";
        public const string ResultLayer = "ScheduleResultAssessmentLayer";
        public const string ExampleSelectionLayer =
            "ExperimentExampleSelectionLayer";
        public const string RuntimeExampleLayer =
            "ExperimentRuntimeExampleLayer";
        // TMP_Dropdown 的弹出列表固定使用 30000，主 Canvas 必须低于它，
        // 否则列表虽已创建却会被主页面挡住。
        private const int AssessmentSortingOrder = 25000;

        [SerializeField]
        [HideInInspector]
        private int prefabVersion;

        public int PrefabVersion => prefabVersion;

        public void SetPrefabVersion(int version)
        {
            prefabVersion = version;
        }

        public static Canvas GetOrCreate(Component context)
        {
            Scene targetScene = context != null
                ? context.gameObject.scene
                : SceneManager.GetActiveScene();

            ExperimentAssessmentCanvasHost[] hosts =
                Resources.FindObjectsOfTypeAll<ExperimentAssessmentCanvasHost>();
            for (int index = 0; index < hosts.Length; index++)
            {
                ExperimentAssessmentCanvasHost host = hosts[index];
                if (host == null || !host.gameObject.scene.IsValid())
                    continue;
                if (host.gameObject.scene != targetScene)
                    continue;

                host.Configure();
                return host.GetComponent<Canvas>();
            }

            GameObject prefab = Resources.Load<GameObject>(CanvasName);
            GameObject root = prefab != null
                ? Instantiate(prefab)
                : new GameObject(
                    CanvasName,
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster),
                    typeof(ExperimentAssessmentCanvasHost));
            root.name = CanvasName;

            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
                root.layer = uiLayer;

            if (targetScene.IsValid() && targetScene.isLoaded &&
                root.scene != targetScene)
            {
                SceneManager.MoveGameObjectToScene(root, targetScene);
            }

            ExperimentAssessmentCanvasHost created =
                root.GetComponent<ExperimentAssessmentCanvasHost>();
            created.Configure();
            return root.GetComponent<Canvas>();
        }

        public static Transform GetLayer(Canvas canvas, string layerName)
        {
            if (canvas == null)
                return null;

            Transform existing = canvas.transform.Find(layerName);
            if (existing != null)
                return existing;

            GameObject layer = new GameObject(layerName, typeof(RectTransform));
            layer.layer = canvas.gameObject.layer;
            layer.transform.SetParent(canvas.transform, false);
            RectTransform rect = layer.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return layer.transform;
        }

        private void Awake()
        {
            Configure();
        }

        private void Reset()
        {
            Configure();
        }

        private void Configure()
        {
            gameObject.name = CanvasName;

            Canvas canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = AssessmentSortingOrder;
            canvas.pixelPerfect = false;

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
            raycaster.ignoreReversedGraphics = true;
            raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
        }
    }
}
