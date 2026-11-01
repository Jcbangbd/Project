using UnityEngine;

namespace Vtp.VR.Integration
{
    // 将 Unity 自带 UI 精灵的序列化引用打入构建，避免依赖仅编辑器可用的加载路径。
    public sealed class ExperimentUiSkin : ScriptableObject
    {
        public Sprite surface;
        public Sprite knob;
        public Sprite checkmark;
        public Sprite dropdownArrow;
        public Font fontSource;

        [Header("Assessment Shift Theme")]
        public Sprite assessmentBackground;
        public Sprite assessmentPanel;
        public Sprite assessmentBorder;
        public Sprite assessmentGlow;
        public Sprite assessmentDropdown;
        public Sprite assessmentSquare;
        public Sprite assessmentSquareBorder;

        public Sprite GetSprite(string name)
        {
            switch (name)
            {
                case "UISprite": return surface;
                case "Knob": return knob;
                case "Checkmark": return checkmark;
                case "DropdownArrow": return dropdownArrow;
                case "AssessmentBackground": return assessmentBackground;
                case "AssessmentPanel": return assessmentPanel;
                case "AssessmentBorder": return assessmentBorder;
                case "AssessmentGlow": return assessmentGlow;
                case "AssessmentDropdown": return assessmentDropdown;
                case "AssessmentSquare": return assessmentSquare;
                case "AssessmentSquareBorder": return assessmentSquareBorder;
                default: return null;
            }
        }
    }
}
