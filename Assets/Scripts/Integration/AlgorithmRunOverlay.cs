using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vtp.VR.Rest;

namespace Vtp.VR.Integration
{
    public sealed class AlgorithmRunOverlay : MonoBehaviour
    {
        public TMP_Text algorithmText;
        public TMP_Text statusText;
        public TMP_Text elapsedText;
        public TMP_Text[] stageTexts;
        public RectTransform spinner;
        public Image[] stageNodes;
        public Image[] stageRings;
        public Image[] stageHalos;
        public Image[] stageChecks;
        public Image[] stageLinks;
        public RectTransform statusPanel;
        private float _started;
        private int _stage;
        public int CurrentStage { get { return _stage; } }
        public bool IsVisible
        {
            get
            {
                var canvas=GetComponent<Canvas>();
                var rect=transform as RectTransform;
                return gameObject.activeInHierarchy && canvas!=null && canvas.enabled &&
                    canvas.isRootCanvas && canvas.renderMode==RenderMode.ScreenSpaceOverlay &&
                    rect!=null && rect.rect.width>1 && rect.rect.height>1 &&
                    transform.lossyScale.x>0 && transform.lossyScale.y>0;
            }
        }
        public void EnsureFullScreenCanvas()
        {
            // 停用的根Canvas可被Unity序列化为零尺寸/零缩放；不能将其作为旧Canvas的子节点加载。
            // 独立ScreenSpaceOverlay Canvas会由Unity和CanvasScaler按当前屏幕重新计算布局。
            if(transform.parent!=null)transform.SetParent(null,false);
            transform.localScale=Vector3.one;
            transform.localRotation=Quaternion.identity;
            var rect=(RectTransform)transform;
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
            rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=Vector2.zero;rect.sizeDelta=Vector2.zero;
            var canvas=GetComponent<Canvas>();
            canvas.enabled=true;canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera=null;canvas.overrideSorting=true;canvas.sortingOrder=31000;
            var group=GetComponent<CanvasGroup>();
            if(group==null)group=gameObject.AddComponent<CanvasGroup>();
            group.alpha=1;group.ignoreParentGroups=true;group.blocksRaycasts=true;group.interactable=true;
            if(Application.isPlaying)DontDestroyOnLoad(gameObject);
            gameObject.SetActive(true);
            var scaler=GetComponent<CanvasScaler>();
            if(scaler!=null){scaler.enabled=false;scaler.enabled=true;}
            Canvas.ForceUpdateCanvases();
        }
        public void Begin(string algorithm, int count)
        {
            _started = Time.realtimeSinceStartup;
            algorithmText.text = algorithm + " · " + count + " 条" + SceneNumericalExampleRuntime.TaskType + "任务";
            EnsureFullScreenCanvas();
            transform.SetAsLastSibling();
            SetStage(0, "正在准备本次算例与设备资源……");
        }
        public void SetStage(int stage, string message)
        {
            _stage = Mathf.Clamp(stage, 0, 2);
            statusText.text = message;
            for (int i = 0; i < stageTexts.Length; i++)
            {
                bool completed = i < _stage;
                bool active = i == _stage;
                stageTexts[i].color = i <= _stage ? ExperimentUiTheme.TextColor : ExperimentUiTheme.Muted;
                if (stageNodes != null && i < stageNodes.Length)
                    stageNodes[i].color = i <= _stage ? ExperimentUiTheme.Cyan : ExperimentUiTheme.PanelRaised;
                if (stageRings != null && i < stageRings.Length)
                    stageRings[i].color = active ? (Color)ExperimentUiTheme.TextColor : i < _stage ? (Color)ExperimentUiTheme.Cyan : ExperimentUiTheme.StrongBorder;
                if (stageHalos != null && i < stageHalos.Length) stageHalos[i].gameObject.SetActive(active);
                if (stageChecks != null && i < stageChecks.Length) stageChecks[i].gameObject.SetActive(completed);
            }
            if (stageLinks != null) for (int i = 0; i < stageLinks.Length; i++)
                stageLinks[i].color = i < _stage ? ExperimentUiTheme.Cyan : ExperimentUiTheme.Border;
        }
        public void Report(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            int stage = message.Contains("校验") || message.Contains("分配岸桥") ? 2
                : message.Contains("执行") || message.Contains("启动") || message.Contains("计算") ? 1 : _stage;
            SetStage(Mathf.Max(_stage, stage), message);
        }
        public void Finish() { gameObject.SetActive(false); }
        private void Update()
        {
            if (spinner != null) spinner.Rotate(0, 0, -150 * Time.unscaledDeltaTime);
            if (stageHalos != null && _stage < stageHalos.Length)
                stageHalos[_stage].color = new Color(.31f, .89f, .87f, .30f + .12f * Mathf.Sin(Time.unscaledTime * 3));
            if (statusPanel != null)
            {
                var canvas = GetComponent<Canvas>();
                float scale = canvas == null ? 1 : Mathf.Max(.01f, canvas.scaleFactor);
                float availableWidth = transform is RectTransform rect ? rect.rect.width : Screen.width / scale;
                float availableHeight = transform is RectTransform rect2 ? rect2.rect.height : Screen.height / scale;
                float fit = Mathf.Min(1, (availableWidth - 48) / 920f, (availableHeight - 48) / 480f);
                statusPanel.localScale = Vector3.one * Mathf.Max(.1f, fit);
            }
            int seconds = Mathf.Max(0, (int)(Time.realtimeSinceStartup - _started));
            elapsedText.text = "已用时 " + (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
        }
    }
}
