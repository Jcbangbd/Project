using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Vtp.VR.Integration;

namespace Vtp.VR.Editor
{
    /// <summary>
    /// 在编辑期生成评分流程使用的独立 Canvas Prefab。
    /// 运行时控制器只绑定这些组件、填充数据和切换显隐，不再拼装页面结构。
    /// </summary>
    public static class ExperimentAssessmentCanvasPrefabBuilder
    {
        private const string PrefabPath =
            "Assets/Resources/ExperimentAssessmentCanvas.prefab";
        private const string SelectionRowPath =
            "Assets/Resources/ExperimentExampleSelectionRow.prefab";
        private const string RuntimeRowPath =
            "Assets/Resources/ExperimentExampleRuntimeRow.prefab";
        private const string FontPath =
            "Assets/Resources/Fonts/AlibabaPuHuiTi-3-55-Regular SDF.asset";
        private const int PrefabVersion = 2;

        private static TMP_FontAsset _font;

        [InitializeOnLoadMethod]
        private static void BuildMissingOrOutdatedPrefab()
        {
            EditorApplication.delayCall += () =>
            {
                if (NeedsBuild())
                    Build();
            };
        }

        private static bool NeedsBuild()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                return true;

            ExperimentAssessmentCanvasHost host =
                prefab.GetComponent<ExperimentAssessmentCanvasHost>();
            if (host == null || host.PrefabVersion != PrefabVersion)
                return true;

            bool hasSelection =
                prefab.GetComponentInChildren<
                    ExperimentExampleSelectionCanvasView>(true) != null;
            bool hasRuntime =
                prefab.GetComponentInChildren<
                    ExperimentExampleRuntimeCanvasView>(true) != null;
            bool hasSelectionRow =
                AssetDatabase.LoadAssetAtPath<GameObject>(SelectionRowPath) != null;
            bool hasRuntimeRow =
                AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeRowPath) != null;
            return !hasSelection || !hasRuntime ||
                   !hasSelectionRow || !hasRuntimeRow;
        }

        [MenuItem("Tools/Assessment UI/Rebuild Dedicated Canvas Prefab")]
        public static void Build()
        {
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            GameObject selectionRow = BuildRowPrefab(
                SelectionRowPath,
                "ExperimentExampleSelectionRow",
                new[]
                {
                    "NoText", "CraneText", "TypeText",
                    "StartText", "EndText", "DurationText"
                },
                new[] { 0.7f, 1.15f, 1.5f, 1.2f, 1.2f, 0.8f },
                52f);
            GameObject runtimeRow = BuildRowPrefab(
                RuntimeRowPath,
                "ExperimentExampleRuntimeRow",
                new[]
                {
                    "Column0", "Column1", "Column2", "Column3",
                    "Column4", "Column5", "Column6"
                },
                new[] { 0.72f, 1.0f, 1.55f, 1.35f, 1.35f, 0.82f, 1.0f },
                50f);

            GameObject root = new GameObject(
                ExperimentAssessmentCanvasHost.CanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(ExperimentAssessmentCanvasHost));

            root.GetComponent<ExperimentAssessmentCanvasHost>()
                .SetPrefabVersion(PrefabVersion);

            Canvas canvas = root.GetComponent<Canvas>();
            CreateLayer(canvas, ExperimentAssessmentCanvasHost.TaskSelectionLayer);
            CreateLayer(canvas, ExperimentAssessmentCanvasHost.QuestionnaireLayer);
            CreateLayer(canvas, ExperimentAssessmentCanvasHost.AlgorithmLayer);
            CreateLayer(canvas, ExperimentAssessmentCanvasHost.ResultLayer);

            Transform selectionLayer = CreateLayer(
                canvas,
                ExperimentAssessmentCanvasHost.ExampleSelectionLayer);
            Transform runtimeLayer = CreateLayer(
                canvas,
                ExperimentAssessmentCanvasHost.RuntimeExampleLayer);

            BuildSelectionPage(selectionLayer, selectionRow);
            BuildRuntimePage(runtimeLayer, runtimeRow);
            AssignPersistentFont(root);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[Assessment UI] 已生成独立 Canvas、实验算例页与仿真算例浮层：" +
                PrefabPath);
        }

        public static void BuildFromCommandLine()
        {
            Build();
        }

        private static Transform CreateLayer(Canvas canvas, string layerName)
        {
            return ExperimentAssessmentCanvasHost.GetLayer(canvas, layerName);
        }

        private static GameObject BuildRowPrefab(
            string path,
            string objectName,
            string[] cellNames,
            float[] widths,
            float height)
        {
            GameObject row = new GameObject(objectName, typeof(RectTransform));
            RectTransform rect = row.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, height);

            Image image = row.AddComponent<Image>();
            ExperimentUiTheme.SciFiSurface(
                image,
                ExperimentUiTheme.PanelRaised,
                false);

            LayoutElement rowElement = row.AddComponent<LayoutElement>();
            rowElement.minHeight = height;
            rowElement.preferredHeight = height;

            HorizontalLayoutGroup layout =
                row.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 2, 2);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            for (int index = 0;
                 index < cellNames.Length && index < widths.Length;
                 index++)
            {
                TMP_Text text = CreateText(
                    cellNames[index],
                    row.transform,
                    "-",
                    19f,
                    ExperimentUiTheme.TextColor,
                    TextAlignmentOptions.Center);
                text.enableWordWrapping = false;
                text.enableAutoSizing = true;
                text.fontSizeMin = 12f;
                text.fontSizeMax = 19f;

                LayoutElement element =
                    text.gameObject.AddComponent<LayoutElement>();
                element.flexibleWidth = widths[index];
                element.minWidth = 0f;
            }

            AssignPersistentFont(row);
            PrefabUtility.SaveAsPrefabAsset(row, path);
            Object.DestroyImmediate(row);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void BuildSelectionPage(
            Transform layer,
            GameObject rowPrefab)
        {
            GameObject page = CreateObject(
                "ExperimentExampleSelectionPage",
                layer);
            ExperimentUiTheme.Stretch(page.GetComponent<RectTransform>());
            ExperimentExampleSelectionCanvasView view =
                page.AddComponent<ExperimentExampleSelectionCanvasView>();

            RectTransform surface =
                ExperimentUiTheme.DesignSurface(page.transform, "ExampleSelectionSurface");
            ExperimentUiViewport viewport =
                surface.GetComponent<ExperimentUiViewport>();
            if (viewport != null)
                Object.DestroyImmediate(viewport);
            surface.localScale = Vector3.one;
            ExperimentUiTheme.Stretch(surface);
            RectTransform header =
                ExperimentUiTheme.HeaderBar(surface, "实验算例");
            TMP_Text title =
                header.Find("Heading").GetComponent<TMP_Text>();

            TMP_Text summary = CreateText(
                "SummaryText",
                surface,
                "请选择教师任务并核对算例数据。",
                25f,
                ExperimentUiTheme.Muted,
                TextAlignmentOptions.MidlineLeft);
            ExperimentUiTheme.Top(summary.rectTransform, 112f, 62f, 62f, 50f);
            summary.enableWordWrapping = false;
            summary.enableAutoSizing = true;
            summary.fontSizeMin = 16f;
            summary.rectTransform.offsetMax = new Vector2(-145f, summary.rectTransform.offsetMax.y);
            ExperiencePolishMigration.AddPositionHelp(surface);

            GameObject tableCard = CreateObject("ExampleTableCard", surface);
            RectTransform tableRect = tableCard.GetComponent<RectTransform>();
            Fill(tableRect, 174f, 154f, 54f, 54f);
            Image tableImage = tableCard.AddComponent<Image>();
            ExperimentUiTheme.SciFiSurface(
                tableImage,
                ExperimentUiTheme.PanelRaised,
                true);

            CreateHeaderRow(
                tableCard.transform,
                new[] { "序号", "岸桥", "任务类型", "起始位置", "目标位置", "时长" },
                new[] { 0.7f, 1.15f, 1.5f, 1.2f, 1.2f, 0.8f },
                14f,
                14f);

            ScrollRect scroll = CreateScrollView(
                tableCard.transform,
                "ExampleScrollView",
                74f,
                14f,
                14f,
                14f,
                out RectTransform content,
                out Scrollbar scrollbar);

            RectTransform footer = ExperimentUiTheme.FooterBar(surface);
            TMP_Text status = CreateText(
                "StatusText",
                footer,
                string.Empty,
                20f,
                ExperimentUiTheme.Muted,
                TextAlignmentOptions.MidlineLeft);
            status.rectTransform.anchorMin = new Vector2(0f, 0f);
            status.rectTransform.anchorMax = new Vector2(0.52f, 1f);
            status.rectTransform.offsetMin = new Vector2(54f, 18f);
            status.rectTransform.offsetMax = new Vector2(-12f, -18f);
            status.enableAutoSizing = true;
            status.fontSizeMin = 14f;

            Button back = CreateButton(footer, "BackButton", "返回", false);
            ExperimentUiTheme.FooterButton(
                back.GetComponent<RectTransform>(), 598f, 210f);
            Button refresh = CreateButton(
                footer,
                "RefreshButton",
                "重新获取",
                false);
            ExperimentUiTheme.FooterButton(
                refresh.GetComponent<RectTransform>(), 362f, 210f);
            Button enter = CreateButton(
                footer,
                "EnterButton",
                "设置参数",
                true);
            ExperimentUiTheme.FooterButton(
                enter.GetComponent<RectTransform>(), 54f, 280f);

            ExperimentLocalFallbackNoticeView notice =
                BuildFallbackNotice(page.transform);

            view.Configure(
                title,
                summary,
                status,
                scroll,
                content,
                scrollbar,
                rowPrefab,
                back,
                refresh,
                enter,
                notice);
            page.SetActive(false);
        }

        private static void BuildRuntimePage(
            Transform layer,
            GameObject rowPrefab)
        {
            GameObject page = CreateObject(
                "ExperimentRuntimeExamplePage",
                layer);
            RectTransform pageRect = page.GetComponent<RectTransform>();
            ExperimentUiTheme.Stretch(pageRect);
            Image blocker = page.AddComponent<Image>();
            blocker.color = new Color(0.03f, 0.11f, 0.16f, 0.42f);
            blocker.raycastTarget = true;

            ExperimentExampleRuntimeCanvasView view =
                page.AddComponent<ExperimentExampleRuntimeCanvasView>();

            GameObject card = CreateObject("RuntimeExampleCard", page.transform);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.045f, 0.055f);
            cardRect.anchorMax = new Vector2(0.955f, 0.945f);
            cardRect.offsetMin = Vector2.zero;
            cardRect.offsetMax = Vector2.zero;
            Image cardImage = card.AddComponent<Image>();
            ExperimentUiTheme.SciFiSurface(
                cardImage,
                ExperimentUiTheme.Panel,
                true);

            ExperimentUiTheme.HeaderBar(card.transform, "实验算例");

            TMP_Text source = CreateText(
                "SourceText",
                card.transform,
                string.Empty,
                20f,
                ExperimentUiTheme.Muted,
                TextAlignmentOptions.MidlineLeft);
            ExperimentUiTheme.Top(source.rectTransform, 98f, 34f, 34f, 42f);
            source.enableWordWrapping = false;
            source.enableAutoSizing = true;
            source.fontSizeMin = 14f;

            CreateHeaderRow(
                card.transform,
                new[]
                {
                    "序号", "岸桥", "任务类型", "起始位置",
                    "目标位置", "时长", "状态"
                },
                new[] { 0.72f, 1.0f, 1.55f, 1.35f, 1.35f, 0.82f, 1.0f },
                148f,
                28f);

            ScrollRect scroll = CreateScrollView(
                card.transform,
                "RuntimeExampleScrollView",
                206f,
                94f,
                28f,
                28f,
                out RectTransform content,
                out Scrollbar scrollbar);

            Button close = CreateButton(
                card.transform,
                "CloseButton",
                "关闭",
                true);
            RectTransform closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = closeRect.anchorMax =
                new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.sizeDelta = new Vector2(240f, 58f);
            closeRect.anchoredPosition = new Vector2(0f, 20f);

            view.Configure(
                source,
                scroll,
                content,
                scrollbar,
                close,
                rowPrefab);
            page.SetActive(false);
        }

        private static ExperimentLocalFallbackNoticeView BuildFallbackNotice(
            Transform parent)
        {
            GameObject overlay = CreateObject(
                "LocalFallbackNotice",
                parent);
            ExperimentUiTheme.Stretch(overlay.GetComponent<RectTransform>());
            Image overlayImage = overlay.AddComponent<Image>();
            overlayImage.color = new Color(0.03f, 0.11f, 0.16f, 0.40f);
            overlayImage.raycastTarget = true;

            GameObject box = CreateObject("NoticeCard", overlay.transform);
            RectTransform boxRect = box.GetComponent<RectTransform>();
            boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(980f, 290f);
            Image boxImage = box.AddComponent<Image>();
            ExperimentUiTheme.SciFiSurface(
                boxImage,
                ExperimentUiTheme.PanelRaised,
                true);

            Image accent = CreateObject("NoticeAccent", box.transform)
                .AddComponent<Image>();
            accent.rectTransform.anchorMin = new Vector2(0f, 0f);
            accent.rectTransform.anchorMax = new Vector2(0f, 1f);
            accent.rectTransform.pivot = new Vector2(0f, 0.5f);
            accent.rectTransform.sizeDelta = new Vector2(6f, -28f);
            accent.rectTransform.anchoredPosition = new Vector2(14f, 0f);
            accent.color = ExperimentUiTheme.Yellow;
            accent.raycastTarget = false;

            TMP_Text title = CreateText(
                "NoticeTitle",
                box.transform,
                "算例来源提示",
                28f,
                ExperimentUiTheme.TextColor,
                TextAlignmentOptions.MidlineLeft,
                FontStyles.Bold);
            ExperimentUiTheme.Top(title.rectTransform, 24f, 46f, 46f, 44f);

            TMP_Text message = CreateText(
                "NoticeMessage",
                box.transform,
                string.Empty,
                23f,
                ExperimentUiTheme.Error,
                TextAlignmentOptions.Center);
            ExperimentUiTheme.Top(message.rectTransform, 82f, 54f, 54f, 92f);
            message.enableWordWrapping = true;
            message.enableAutoSizing = true;
            message.fontSizeMin = 16f;

            Button confirm = CreateButton(
                box.transform,
                "ConfirmButton",
                "知道了",
                true);
            RectTransform confirmRect = confirm.GetComponent<RectTransform>();
            confirmRect.anchorMin = confirmRect.anchorMax =
                new Vector2(0.5f, 0f);
            confirmRect.pivot = new Vector2(0.5f, 0f);
            confirmRect.sizeDelta = new Vector2(220f, 58f);
            confirmRect.anchoredPosition = new Vector2(0f, 26f);

            ExperimentLocalFallbackNoticeView notice =
                overlay.AddComponent<ExperimentLocalFallbackNoticeView>();
            notice.Configure(message, confirm);
            overlay.SetActive(false);
            return notice;
        }

        private static ScrollRect CreateScrollView(
            Transform parent,
            string name,
            float top,
            float bottom,
            float left,
            float right,
            out RectTransform content,
            out Scrollbar scrollbar)
        {
            GameObject scrollObject = CreateObject(name, parent);
            RectTransform scrollRectTransform =
                scrollObject.GetComponent<RectTransform>();
            Fill(scrollRectTransform, top, bottom, left, right);
            Image scrollImage = scrollObject.AddComponent<Image>();
            ExperimentUiTheme.SciFiSurface(
                scrollImage,
                ExperimentUiTheme.PanelInput,
                true);

            GameObject viewportObject =
                CreateObject("Viewport", scrollObject.transform);
            RectTransform viewport =
                viewportObject.GetComponent<RectTransform>();
            ExperimentUiTheme.Stretch(viewport);
            viewport.offsetMin = new Vector2(8f, 8f);
            viewport.offsetMax = new Vector2(-8f, -8f);
            Image viewportImage = viewportObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
            viewportObject.AddComponent<RectMask2D>();

            GameObject contentObject =
                CreateObject("Content", viewportObject.transform);
            content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            VerticalLayoutGroup layout =
                contentObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.spacing = 7f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter =
                contentObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 72f;
            ExperimentUiTheme.Scrollbar(scroll);
            scrollbar = scroll.verticalScrollbar;
            return scroll;
        }

        private static void CreateHeaderRow(
            Transform parent,
            string[] labels,
            float[] widths,
            float top,
            float horizontalMargin)
        {
            GameObject row = CreateObject("TableHeader", parent);
            RectTransform rect = row.GetComponent<RectTransform>();
            ExperimentUiTheme.Top(
                rect,
                top,
                horizontalMargin,
                horizontalMargin,
                48f);
            Image image = row.AddComponent<Image>();
            ExperimentUiTheme.SciFiSurface(
                image,
                ExperimentUiTheme.TableHeader,
                false);

            HorizontalLayoutGroup layout =
                row.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 1, 1);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            for (int index = 0;
                 index < labels.Length && index < widths.Length;
                 index++)
            {
                TMP_Text cell = CreateText(
                    "Header" + index,
                    row.transform,
                    labels[index],
                    19f,
                    ExperimentUiTheme.TextColor,
                    TextAlignmentOptions.Center,
                    FontStyles.Bold);
                cell.enableWordWrapping = false;
                LayoutElement element =
                    cell.gameObject.AddComponent<LayoutElement>();
                element.flexibleWidth = widths[index];
                element.minWidth = 0f;
            }
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            bool primary)
        {
            GameObject objectValue = CreateObject(name, parent);
            Image image = objectValue.AddComponent<Image>();
            Button button = objectValue.AddComponent<Button>();
            button.targetGraphic = image;
            TMP_Text text = CreateText(
                "Text",
                objectValue.transform,
                label,
                22f,
                primary
                    ? ExperimentUiTheme.PrimaryText
                    : ExperimentUiTheme.TextColor,
                TextAlignmentOptions.Center,
                FontStyles.Bold);
            ExperimentUiTheme.Stretch(text.rectTransform);
            ExperimentUiTheme.StyleButton(button, primary);
            return button;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            string value,
            float size,
            Color color,
            TextAlignmentOptions alignment,
            FontStyles style = FontStyles.Normal)
        {
            GameObject textObject = CreateObject(name, parent);
            TextMeshProUGUI text =
                textObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateObject(string name, Transform parent)
        {
            GameObject result = new GameObject(name, typeof(RectTransform));
            result.transform.SetParent(parent, false);
            result.layer = parent.gameObject.layer;
            return result;
        }

        private static void Fill(
            RectTransform rect,
            float top,
            float bottom,
            float left,
            float right)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void AssignPersistentFont(GameObject root)
        {
            if (_font == null || root == null)
                return;

            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int index = 0; index < texts.Length; index++)
                if (texts[index].font == null || !EditorUtility.IsPersistent(texts[index].font))
                    texts[index].font = _font;
        }
    }
}
