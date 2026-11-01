using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vtp.VR.Integration
{
    /// <summary>
    /// “实验算例选择”页面的可序列化 Canvas 视图。
    /// 页面结构由 Prefab 保存，业务脚本只读取引用并填充数据。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExperimentExampleSelectionCanvasView : MonoBehaviour
    {
        [Header("页面文字")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private TMP_Text statusText;

        [Header("列表")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform content;
        [SerializeField] private Scrollbar verticalScrollbar;
        [SerializeField] private GameObject rowPrefab;

        [Header("操作")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button enterButton;
        [SerializeField]
        private ExperimentLocalFallbackNoticeView fallbackNotice;

        public TMP_Text TitleText => titleText;
        public TMP_Text SummaryText => summaryText;
        public TMP_Text StatusText => statusText;
        public ScrollRect ScrollRect => scrollRect;
        public RectTransform Content => content;
        public Scrollbar VerticalScrollbar => verticalScrollbar;
        public GameObject RowPrefab => rowPrefab;
        public Button BackButton => backButton;
        public Button RefreshButton => refreshButton;
        public Button EnterButton => enterButton;
        public ExperimentLocalFallbackNoticeView FallbackNotice =>
            fallbackNotice;

        public void Configure(
            TMP_Text pageTitle,
            TMP_Text summary,
            TMP_Text status,
            ScrollRect listScrollRect,
            RectTransform listContent,
            Scrollbar listScrollbar,
            GameObject listRowPrefab,
            Button back,
            Button refresh,
            Button enter,
            ExperimentLocalFallbackNoticeView notice)
        {
            titleText = pageTitle;
            summaryText = summary;
            statusText = status;
            scrollRect = listScrollRect;
            content = listContent;
            verticalScrollbar = listScrollbar;
            rowPrefab = listRowPrefab;
            backButton = back;
            refreshButton = refresh;
            enterButton = enter;
            fallbackNotice = notice;
        }
    }
}
