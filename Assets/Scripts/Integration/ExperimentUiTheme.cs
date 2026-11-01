using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vtp.VR.Integration
{
    /// <summary>仅负责教学页面外观，不参与答案、评分或算法执行。</summary>
    public static class ExperimentUiTheme
    {
        private static TMP_FontAsset _font;
        private static ExperimentUiSkin _skin;

        private static ExperimentUiSkin Skin
        {
            get
            {
                if (_skin == null)
                    _skin = Resources.Load<ExperimentUiSkin>("ExperimentUiSkin");
                return _skin;
            }
        }

        public static TMP_FontAsset Font
        {
            get
            {
                if (_font != null) return _font;
                TMP_FontAsset original = Resources.Load<TMP_FontAsset>("Fonts/AlibabaPuHuiTi-3-55-Regular SDF");
                if (original == null) return null;
                // 只给新页面使用的副本补充动态字形，不修改旧场景字体资源。
                _font = UnityEngine.Object.Instantiate(original);
                _font.name = original.name + " (Experiment UI)";
                _font.fallbackFontAssetTable = original.fallbackFontAssetTable == null
                    ? new System.Collections.Generic.List<TMP_FontAsset>()
                    : new System.Collections.Generic.List<TMP_FontAsset>(original.fallbackFontAssetTable);
                ExperimentUiSkin skin = Skin;
                if (skin != null && skin.fontSource != null)
                {
                    TMP_FontAsset fallback = TMP_FontAsset.CreateFontAsset(skin.fontSource);
                    if (fallback != null) _font.fallbackFontAssetTable.Add(fallback);
                }
                return _font;
            }
        }
        // “全息蓝港”统一配色；仅影响显示，不参与业务状态与评分。
        public static readonly Color32 Page = new Color32(7, 30, 40, 255);
        public static readonly Color32 Header = new Color32(8, 31, 42, 255);
        public static readonly Color32 Panel = new Color32(12, 38, 50, 255);
        public static readonly Color32 PanelRaised = new Color32(16, 46, 59, 255);
        public static readonly Color32 PanelInput = new Color32(9, 32, 43, 255);
        public static readonly Color32 PanelHover = new Color32(25, 65, 78, 255);
        public static readonly Color32 PanelPressed = new Color32(30, 84, 95, 255);
        public static readonly Color32 TextColor = new Color32(230, 241, 243, 255);
        public static readonly Color32 Muted = new Color32(166, 190, 201, 255);
        public static readonly Color32 Border = new Color32(43, 84, 100, 255);
        public static readonly Color32 TableHeader = new Color32(25, 62, 76, 255);
        public static readonly Color32 Alternate = new Color32(18, 43, 55, 255);
        public static readonly Color32 Yellow = new Color32(79, 226, 223, 255);
        public static readonly Color32 Selected = new Color32(21, 76, 88, 255);
        public static readonly Color32 SelectedBorder = new Color32(79, 226, 223, 255);
        public static readonly Color32 Focus = new Color32(79, 226, 223, 255);
        public static readonly Color32 Error = new Color32(255, 146, 151, 255);
        public static readonly Color32 Success = new Color32(120, 222, 182, 255);
        public static readonly Color32 Cyan = new Color32(79, 226, 223, 255);
        public static readonly Color32 Primary = new Color32(65, 216, 220, 255);
        public static readonly Color32 PrimaryText = new Color32(6, 35, 46, 255);
        public static readonly Color32 Footer = new Color32(9, 32, 43, 255);
        public static readonly Color32 StrongBorder = new Color32(66, 125, 143, 255);

        public static GameObject Object(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            obj.layer = parent.gameObject.layer;
            return obj;
        }

        // 独立设计画布只缩放本页，避免修改旧场景 CanvasScaler。
        public static RectTransform DesignSurface(Transform parent, string name)
        {
            RectTransform rect = Object(name, parent).GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1920f, 1080f);
            Image background = rect.gameObject.AddComponent<Image>();
            ApplySimpleSprite(background, "AssessmentBackground", Color.white);

            Image shade = Object("BackgroundShade", rect).AddComponent<Image>();
            Stretch(shade.rectTransform);
            shade.color = new Color(Page.r / 255f, Page.g / 255f, Page.b / 255f, 0.94f);
            shade.raycastTarget = false;

            Image panel = Object("ConsolePanel", rect).AddComponent<Image>();
            Stretch(panel.rectTransform);
            panel.rectTransform.offsetMin = new Vector2(14f, 14f);
            panel.rectTransform.offsetMax = new Vector2(-14f, -14f);
            ApplySlicedSprite(panel, "AssessmentPanel",
                new Color(Panel.r / 255f, Panel.g / 255f, Panel.b / 255f, 0.96f));
            panel.raycastTarget = false;

            Image glow = Object("SurfaceGlow", rect).AddComponent<Image>();
            Stretch(glow.rectTransform);
            glow.rectTransform.offsetMin = new Vector2(7f, 7f);
            glow.rectTransform.offsetMax = new Vector2(-7f, -7f);
            ApplySlicedSprite(
                glow,
                "AssessmentGlow",
                new Color(Cyan.r / 255f, Cyan.g / 255f, Cyan.b / 255f, 0.11f));
            glow.raycastTarget = false;

            Image border = Object("SurfaceBorder", rect).AddComponent<Image>();
            Stretch(border.rectTransform);
            border.rectTransform.offsetMin = new Vector2(12f, 12f);
            border.rectTransform.offsetMax = new Vector2(-12f, -12f);
            ApplySlicedSprite(
                border,
                "AssessmentBorder",
                new Color(Cyan.r / 255f, Cyan.g / 255f, Cyan.b / 255f, 0.52f));
            border.raycastTarget = false;

            Image sideRail = Object("IndustrialSideRail", rect).AddComponent<Image>();
            sideRail.color = new Color(Cyan.r / 255f, Cyan.g / 255f, Cyan.b / 255f, 0.94f);
            sideRail.raycastTarget = false;
            sideRail.rectTransform.anchorMin = new Vector2(0f, 0f);
            sideRail.rectTransform.anchorMax = new Vector2(0f, 1f);
            sideRail.rectTransform.pivot = new Vector2(0f, 0.5f);
            sideRail.rectTransform.sizeDelta = new Vector2(5f, -268f);
            sideRail.rectTransform.anchoredPosition = new Vector2(28f, 2f);

            Image identityRail = Object("TerminalIdentityRail", rect).AddComponent<Image>();
            identityRail.color = Yellow;
            identityRail.raycastTarget = false;
            identityRail.rectTransform.anchorMin = new Vector2(0f, 1f);
            identityRail.rectTransform.anchorMax = new Vector2(0f, 1f);
            identityRail.rectTransform.pivot = new Vector2(0f, 1f);
            identityRail.rectTransform.sizeDelta = new Vector2(92f, 4f);
            identityRail.rectTransform.anchoredPosition = new Vector2(28f, -118f);

            ExperimentUiViewport viewport = rect.gameObject.AddComponent<ExperimentUiViewport>();
            viewport.Refresh();
            return rect;
        }

        public static RectTransform HeaderBar(Transform parent, string title)
        {
            RectTransform bar = Object("Header", parent).GetComponent<RectTransform>();
            Top(bar, 0, 0, 0, 100);
            Image background = bar.gameObject.AddComponent<Image>();
            ApplySlicedSprite(background, "AssessmentPanel", Header);

            Image baseline = Object("HeaderBaseline", bar).AddComponent<Image>();
            Bottom(baseline.rectTransform, 0f, 0f, 0f, 2f);
            baseline.color = new Color(Cyan.r / 255f, Cyan.g / 255f,
                Cyan.b / 255f, 0.45f);
            baseline.raycastTarget = false;

            Image accent = Object("HeaderAccent", bar).AddComponent<Image>();
            Bottom(accent.rectTransform, 0f, 72f, 1588f, 6f);
            accent.color = Cyan;
            accent.raycastTarget = false;

            TMP_Text text = Text("Heading", bar, title, 36, Color.white, FontStyles.Bold);
            Top(text.rectTransform, 12, 72, 440, 76);
            return bar;
        }

        public static RectTransform FooterBar(Transform parent)
        {
            RectTransform bar = Object("Footer", parent).GetComponent<RectTransform>();
            Bottom(bar, 0, 0, 0, 132);
            Image background = bar.gameObject.AddComponent<Image>();
            ApplySlicedSprite(background, "AssessmentPanel", Footer);
            Divider(bar, true);

            Image accent = Object("FooterAccent", bar).AddComponent<Image>();
            Top(accent.rectTransform, 0f, 1596f, 72f, 4f);
            accent.color = Cyan;
            accent.raycastTarget = false;
            return bar;
        }

        public static TMP_Text Text(string name, Transform parent, string value,
            float size, Color color, FontStyles style = FontStyles.Normal)
        {
            TMP_Text text = Object(name, parent).AddComponent<TextMeshProUGUI>();
            text.font = Font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color == Color.black ? (Color)TextColor : color;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public static void Top(RectTransform rect, float top, float left, float right, float height)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        public static void Bottom(RectTransform rect, float bottom, float left, float right, float height)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, bottom + height);
        }

        public static void FooterButton(RectTransform rect, float right, float width)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1, 0.5f);
            rect.pivot = new Vector2(1, 0.5f);
            rect.sizeDelta = new Vector2(width, 64);
            rect.anchoredPosition = new Vector2(-right, 0);
        }

        public static Sprite BuiltinSprite(string name)
        {
            ExperimentUiSkin skin = Skin;
            if (skin != null) return skin.GetSprite(name);
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/" + name + ".psd");
#else
            Debug.LogError("[ExperimentUiTheme] 缺少 ExperimentUiSkin 原生 UI 图标引用资源。");
            return null;
#endif
        }

        public static Outline Surface(Image image, Color color, bool border = true)
        {
            // 平面填色和轻边框，避免原生 UISprite 的旧式灰色立体边缘。
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = color;
            Outline outline = image.GetComponent<Outline>();
            if (border)
            {
                if (outline == null) outline = image.gameObject.AddComponent<Outline>();
                outline.effectDistance = new Vector2(1, -1);
                outline.effectColor = Border;
                outline.useGraphicAlpha = false;
            }
            return outline;
        }

        public static Outline SciFiSurface(
            Image image,
            Color color,
            bool border = true,
            bool dropdown = false)
        {
            if (image == null)
                return null;

            ApplySlicedSprite(
                image,
                dropdown ? "AssessmentDropdown" : "AssessmentPanel",
                ResolveSurfaceColor(color));
            Outline outline = image.GetComponent<Outline>();
            if (border)
            {
                if (outline == null)
                    outline = image.gameObject.AddComponent<Outline>();
                outline.effectDistance = new Vector2(1.2f, -1.2f);
                outline.effectColor = Border;
                outline.useGraphicAlpha = false;
            }
            return outline;
        }

        private static Color ResolveSurfaceColor(Color color)
        {
            if (color.r > 0.92f && color.g > 0.92f && color.b > 0.92f)
            {
                Color resolved = PanelRaised;
                resolved.a = color.a;
                return resolved;
            }
            return color;
        }

        private static void ApplySlicedSprite(
            Image image,
            string spriteName,
            Color color)
        {
            Sprite sprite = BuiltinSprite(spriteName);
            image.sprite = sprite;
            image.type = sprite == null ? Image.Type.Simple : Image.Type.Sliced;
            image.color = color;
        }

        private static void ApplySimpleSprite(
            Image image,
            string spriteName,
            Color color)
        {
            image.sprite = BuiltinSprite(spriteName);
            image.type = Image.Type.Simple;
            image.color = color;
        }

        public static void StyleButton(Button button, bool primary)
        {
            Image image = button.targetGraphic as Image;
            if (image == null) return;
            SciFiSurface(image, primary ? (Color)Primary : (Color)PanelRaised, true);
            ColorBlock colors = button.colors;
            colors.normalColor = primary ? (Color)Primary : (Color)PanelRaised;
            colors.highlightedColor = primary ? new Color32(121, 238, 234, 255) : (Color)PanelHover;
            colors.pressedColor = primary ? new Color32(40, 167, 179, 255) : (Color)PanelPressed;
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = PanelRaised;
            colors.colorMultiplier = 1;
            colors.fadeDuration = 0.12f;
            // Graphic.color 保持白色，避免与 Selectable 的目标颜色重复相乘。
            image.color = Color.white;
            button.colors = colors;
            AddButtonGlow(button.transform, primary);
            AddButtonAccent(button.transform, primary);
            ExperimentUiButtonTextState state =
                button.GetComponent<ExperimentUiButtonTextState>();
            if (state == null)
                state = button.gameObject.AddComponent<ExperimentUiButtonTextState>();
            state.Button = button;
            state.Primary = primary;
        }

        public static void StyleChoice(Button button)
        {
            SciFiSurface(button.targetGraphic as Image, PanelRaised);
            ColorBlock colors = button.colors;
            colors.normalColor = colors.selectedColor = PanelRaised;
            colors.highlightedColor = PanelHover;
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = PanelPressed;
            colors.disabledColor = PanelRaised;
            colors.fadeDuration = 0.12f;
            button.colors = colors;
            EnsureSelectionRail(button.transform);
        }

        public static void SelectedState(Image image, bool selected)
        {
            Color normal = selected ? (Color)Selected : (Color)PanelRaised;
            image.color = normal;
            Outline outline = image.GetComponent<Outline>();
            if (outline != null) outline.effectColor = selected ? (Color)SelectedBorder : (Color)Border;
            Button button = image.GetComponent<Button>();
            if (button != null)
            {
                ColorBlock colors = button.colors;
                colors.normalColor = normal;
                colors.highlightedColor = selected
                    ? (Color)PanelPressed
                    : (Color)PanelHover;
                colors.selectedColor = colors.highlightedColor;
                colors.pressedColor = PanelPressed;
                button.colors = colors;
            }
            Transform marker = image.transform.Find("SelectionIndicator/Mark");
            if (marker != null) marker.gameObject.SetActive(selected);
            Transform rail = image.transform.Find("SelectionRail");
            if (rail != null) rail.gameObject.SetActive(selected);
        }

        public static void ChoiceIndicator(Transform parent, bool multiple)
        {
            RectTransform rect = Object("SelectionIndicator", parent).GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(32, 0);
            rect.sizeDelta = new Vector2(28, 28);
            Image image = rect.gameObject.AddComponent<Image>();
            Surface(image, PanelInput);
            image.sprite = multiple
                ? BuiltinSprite("AssessmentSquareBorder")
                : BuiltinSprite("Knob");
            image.type = multiple ? Image.Type.Sliced : Image.Type.Simple;
            Outline outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;
            image.raycastTarget = false;
            Image mark = Object("Mark", rect).AddComponent<Image>();
            Stretch(mark.rectTransform);
            mark.rectTransform.offsetMin = new Vector2(5, 5);
            mark.rectTransform.offsetMax = new Vector2(-5, -5);
            mark.sprite = BuiltinSprite(multiple ? "Checkmark" : "Knob");
            mark.color = Cyan;
            mark.raycastTarget = false;
            mark.gameObject.SetActive(false);
        }

        public static void StyleInput(TMP_InputField input)
        {
            Outline outline = SciFiSurface(input.GetComponent<Image>(), PanelInput);
            input.caretColor = Cyan;
            input.selectionColor = new Color(Cyan.r / 255f, Cyan.g / 255f,
                Cyan.b / 255f, 0.30f);
            input.textComponent.color = TextColor;
            if (input.placeholder is TMP_Text) ((TMP_Text)input.placeholder).color = Muted;
            Image accent = Object("InputFocusLine", input.transform).AddComponent<Image>();
            Bottom(accent.rectTransform, 0f, 10f, 10f, 2f);
            accent.color = Border;
            accent.raycastTarget = false;
            ExperimentUiInputFocus focus = input.gameObject.AddComponent<ExperimentUiInputFocus>();
            focus.Input = input;
            focus.Border = outline;
            focus.Accent = accent;
        }

        public static void Scrollbar(ScrollRect scroll)
        {
            RectTransform bar = Object("VerticalScrollbar", scroll.transform).GetComponent<RectTransform>();
            bar.anchorMin = new Vector2(1, 0);
            bar.anchorMax = Vector2.one;
            bar.offsetMin = new Vector2(-10, 8);
            bar.offsetMax = new Vector2(-2, -8);
            Image background = bar.gameObject.AddComponent<Image>();
            ApplySlicedSprite(background, "AssessmentSquare", PanelInput);
            RectTransform area = Object("SlidingArea", bar).GetComponent<RectTransform>();
            Stretch(area);
            Image handle = Object("Handle", area).AddComponent<Image>();
            Stretch(handle.rectTransform);
            ApplySlicedSprite(handle, "AssessmentSquare", Cyan);
            UnityEngine.UI.Scrollbar scrollbar = bar.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();
            scrollbar.targetGraphic = handle;
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scroll.viewport.offsetMax = new Vector2(-20, 0);
        }

        public static void Divider(Transform parent, bool top = false)
        {
            Image line = Object("Divider", parent).AddComponent<Image>();
            line.color = Border;
            line.raycastTarget = false;
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            if (top) Top(line.rectTransform, 0, 0, 0, 1);
            else Bottom(line.rectTransform, 0, 0, 0, 1);
        }

        private static void AddButtonGlow(Transform parent, bool primary)
        {
            if (parent == null || parent.Find("ButtonGlow") != null)
                return;

            Image glow = Object("ButtonGlow", parent).AddComponent<Image>();
            Stretch(glow.rectTransform);
            glow.rectTransform.offsetMin = new Vector2(-6f, -6f);
            glow.rectTransform.offsetMax = new Vector2(6f, 6f);
            ApplySlicedSprite(
                glow,
                "AssessmentGlow",
                primary
                    ? new Color(Cyan.r / 255f, Cyan.g / 255f, Cyan.b / 255f, 0.16f)
                    : new Color(Cyan.r / 255f, Cyan.g / 255f, Cyan.b / 255f, 0.045f));
            glow.raycastTarget = false;
            glow.transform.SetAsFirstSibling();
        }

        private static void AddButtonAccent(Transform parent, bool primary)
        {
            if (!primary || parent == null || parent.Find("ButtonAccent") != null)
                return;

            Image accent = Object("ButtonAccent", parent).AddComponent<Image>();
            Bottom(accent.rectTransform, 0f, 18f, 0f, 3f);
            accent.rectTransform.anchorMax = new Vector2(0.35f, 0f);
            accent.color = Yellow;
            accent.raycastTarget = false;
        }

        private static void EnsureSelectionRail(Transform parent)
        {
            if (parent == null || parent.Find("SelectionRail") != null)
                return;

            Image rail = Object("SelectionRail", parent).AddComponent<Image>();
            rail.rectTransform.anchorMin = Vector2.zero;
            rail.rectTransform.anchorMax = new Vector2(0f, 1f);
            rail.rectTransform.pivot = new Vector2(0f, 0.5f);
            rail.rectTransform.sizeDelta = new Vector2(5f, -12f);
            rail.rectTransform.anchoredPosition = new Vector2(2f, 0f);
            rail.color = Cyan;
            rail.raycastTarget = false;
            rail.gameObject.SetActive(false);
        }
    }

}
