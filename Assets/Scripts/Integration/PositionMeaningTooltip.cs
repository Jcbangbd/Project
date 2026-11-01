using UnityEngine;
using UnityEngine.EventSystems;

namespace Vtp.VR.Integration
{
    /// <summary>悬浮说明的内容和组件保存在预制体中；运行时仅切换显隐。</summary>
    public sealed class PositionMeaningTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public const string Explanation = "位置格式为 贝位 / 列号\n例如 35 / 4：堆场第35贝、第4列，不包含箱层信息。\n0贝为海侧支架；1至50贝为堆场；51贝为陆侧车道。\n起始位置表示取箱位置，目标位置表示放箱位置。\n这里的贝位编号与三维箱区编号分别管理。";
        [SerializeField] private GameObject tooltipPanel;
        public GameObject Panel => tooltipPanel;
        public bool IsOpen => tooltipPanel != null && tooltipPanel.activeInHierarchy;
        public void Configure(GameObject panel) { tooltipPanel = panel; Show(false); }
        public void OnPointerEnter(PointerEventData eventData) { Show(true); }
        public void OnPointerExit(PointerEventData eventData) { Show(false); }
        public void OnSelect(BaseEventData eventData) { Show(true); }
        public void OnDeselect(BaseEventData eventData) { Show(false); }
        private void OnDisable() { Show(false); }
        private void Update() { if (Input.GetKeyDown(KeyCode.Escape)) Show(false); }
        private void Show(bool visible) { if (tooltipPanel != null) tooltipPanel.SetActive(visible); }
    }
}
