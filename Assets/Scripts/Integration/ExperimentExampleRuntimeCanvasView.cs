using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vtp.VR.Integration
{
    /// <summary>
    /// 三维仿真中“查看算例”浮层的可序列化 Canvas 视图。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExperimentExampleRuntimeCanvasView : MonoBehaviour
    {
        [SerializeField] private TMP_Text sourceText;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform content;
        [SerializeField] private Scrollbar verticalScrollbar;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject rowPrefab;

        public TMP_Text SourceText => sourceText;
        public ScrollRect ScrollRect => scrollRect;
        public RectTransform Content => content;
        public Scrollbar VerticalScrollbar => verticalScrollbar;
        public Button CloseButton => closeButton;
        public GameObject RowPrefab => rowPrefab;

        public void Configure(
            TMP_Text source,
            ScrollRect listScrollRect,
            RectTransform listContent,
            Scrollbar listScrollbar,
            Button close,
            GameObject listRowPrefab)
        {
            sourceText = source;
            scrollRect = listScrollRect;
            content = listContent;
            verticalScrollbar = listScrollbar;
            closeButton = close;
            rowPrefab = listRowPrefab;
        }
    }
}
