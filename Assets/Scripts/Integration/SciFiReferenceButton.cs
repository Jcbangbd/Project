using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vtp.VR.Integration
{
    /// <summary>只控制方案图风格按钮的颜色与选中态，不替换业务点击事件。</summary>
    public sealed class SciFiReferenceButton : MonoBehaviour
    {
        public Button button;
        public TMP_Text label;
        public Image icon;
        public Image border;
        public Image glow;
        public bool selected;
        public bool navigationTab;
        public Color ink = new Color32(25, 66, 101, 255);
        public Color accent = new Color32(0, 174, 232, 255);
        private bool _lastInteractable;

        public void SetSelected(bool value)
        {
            selected = value;
            Refresh();
        }

        private void OnEnable() { Refresh(); }
        private void LateUpdate()
        {
            if (button != null && _lastInteractable != button.interactable) Refresh();
        }

        private void Refresh()
        {
            if (button == null) return;
            _lastInteractable = button.interactable;
            var colors = button.colors;
            colors.normalColor = selected ? (navigationTab ? (Color)ExperimentUiTheme.Selected : ExperimentUiTheme.Primary) : ExperimentUiTheme.PanelRaised;
            colors.highlightedColor = selected ? new Color32(121, 238, 234, 255) : (Color)ExperimentUiTheme.PanelHover;
            colors.pressedColor = ExperimentUiTheme.PanelPressed;
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = ExperimentUiTheme.PanelRaised;
            colors.colorMultiplier = 1;
            colors.fadeDuration = .12f;
            button.colors = colors;
            if (button.targetGraphic != null) button.targetGraphic.color = Color.white;
            Color textColor = button.interactable ? (selected ? (navigationTab ? (Color)ExperimentUiTheme.Cyan : (Color)ExperimentUiTheme.PrimaryText) : (Color)ExperimentUiTheme.TextColor) : (Color)ExperimentUiTheme.Muted;
            if (label != null) label.color = textColor;
            if (icon != null) icon.color = textColor;
            if (border != null) border.color = selected ? (Color)ExperimentUiTheme.Cyan : ExperimentUiTheme.Border;
            if (glow != null) glow.gameObject.SetActive(selected && button.interactable);
        }
    }
}
