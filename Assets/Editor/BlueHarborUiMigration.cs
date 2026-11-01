using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Vtp.VR.Integration;

// 显式的表现层迁移。只在隔离工程执行，不自动运行、不替换业务事件。
public static class BlueHarborUiMigration
{
    private const string Art = "Assets/Art/UI/BlueHarbor/";
    private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/AlibabaPuHuiTi-3-55-Regular SDF.asset");
    private static Color Ink => ExperimentUiTheme.TextColor;
    private static Color Accent => ExperimentUiTheme.Cyan;

    public static void Apply()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("请在隔离批处理工程中执行迁移。");
        Import(Art + "NightPortHero.png"); Import(Art + "DischargeBlueprintStrip.png");
        string[] names = { "PortLoginView", "ExperimentAssessmentCanvas", "PreExperimentQuestionnaire", "OperationFlowView", "FlowImageZoom", "AlgorithmRunOverlay", "ExperimentCompletionView", "ExperimentExampleRuntimeRow", "ExperimentExampleSelectionRow" };
        foreach (string name in names)
        {
            string path = "Assets/Resources/" + name + ".prefab";
            if (!File.Exists(path)) continue;
            Patch(path, root =>
            {
                Recolor(root);
                var login = root.GetComponent<PortLoginView>();
                if (login != null) ConfigureLogin(login);
                var overlay = root.GetComponent<AlgorithmRunOverlay>();
                if (overlay != null) ConfigureOverlay(overlay);
            });
        }
        BuildIntroduction();
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/PortUiFrostedBackdrop.mat");
        material.SetColor("_Tint", new Color(.025f, .10f, .14f, .62f));
        material.SetFloat("_Radius", 1.4f); EditorUtility.SetDirty(material);
        PatchLoginScene();
        AssetDatabase.SaveAssets();
        Debug.Log("BLUE_HARBOR_MIGRATION_COMPLETE");
    }

    public static void BuildIntroduction()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<OperationFlowCatalog>("Assets/Resources/OperationFlowCatalog.asset");
        if (Font == null || catalog == null) throw new InvalidOperationException("缺少既有字体或作业流程数据。");
        var root = E("ExperimentIntroductionView", null);
        var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true; canvas.sortingOrder = 1000;
        var scaler = root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        root.AddComponent<GraphicRaycaster>();
        var view = root.AddComponent<ExperimentIntroductionView>();
        var surface = E("IntroductionSurface", root.transform); R(surface.transform, 0, 0, 1, 1);
        Fill(surface, ExperimentUiTheme.Page);
        var hero = E("NightPortPanorama", surface.transform).AddComponent<RawImage>();
        hero.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "NightPortHero.png");
        // 适配英雄区宽高比时裁掉上下留白，不拉伸港机。
        hero.uvRect = new Rect(0, .135f, 1, .73f); hero.raycastTarget = false;
        R(hero.transform, 0, .422f, 1, .912f);
        var copyBase = E("HeroCopyBase", surface.transform).AddComponent<Image>();
        copyBase.color = new Color(.02f, .09f, .12f, .70f); copyBase.raycastTarget = false;
        R(copyBase.transform, 0, .422f, .29f, .912f);
        var copyShade = E("HeroCopyShade", surface.transform).AddComponent<Image>();
        copyShade.sprite = Sprite("Assets/Shift - Complete Sci-Fi UI/Textures/Glow & Shadow/Gradient Left.png");
        copyShade.color = new Color(.02f, .09f, .12f, .70f); copyShade.raycastTarget = false;
        R(copyShade.transform, .29f, .422f, .43f, .912f);
        var header = E("NavigationBar", surface.transform); R(header.transform, 0, .912f, 1, 1);
        Fill(header, ExperimentUiTheme.Header);
        var brand = T("SystemTitle", header.transform, "自动化集装箱码头 VR 系统", 36, Ink, true);
        R(brand.transform, .033f, .36f, .447f, .92f);
        var english = T("EnglishTitle", header.transform, "AUTOMATED CONTAINER TERMINAL", 17, ExperimentUiTheme.Muted);
        english.characterSpacing = 4; R(english.transform, .035f, .10f, .44f, .37f);
        view.tabs = new Button[5];
        for (int i = 0; i < 5; i++)
        {
            view.tabs[i] = Button("FlowTab_" + i, header.transform, catalog.entries[i].title, false, 22);
            R(view.tabs[i].transform, .46f + i * .104f, .12f, .56f + i * .104f, .84f);
            var tabStyle = view.tabs[i].GetComponent<SciFiReferenceButton>(); tabStyle.navigationTab = true;
            var activeLine = E("ActiveTabLine", view.tabs[i].transform).AddComponent<Image>();
            activeLine.color = Accent; activeLine.raycastTarget = false; R(activeLine.transform, 0, 0, 1, .035f);
            tabStyle.glow = activeLine; tabStyle.SetSelected(i == 0);
        }
        Rail("HeaderBaseline", surface.transform, 0, .911f, 1, .9125f, ExperimentUiTheme.StrongBorder);
        var flowRoot = E("OperationFlowView", surface.transform); R(flowRoot.transform, 0, 0, 1, .912f);
        var flow = flowRoot.AddComponent<OperationFlowView>(); flow.catalog = catalog; view.flow = flow;
        var eyebrow = T("Eyebrow", flowRoot.transform, "作业流程介绍", 23, Accent);
        R(eyebrow.transform, .035f, .911f, .32f, .956f);
        Rail("EyebrowAccent", flowRoot.transform, .035f, .89f, .058f, .895f, Accent);
        flow.heading = T("Heading", flowRoot.transform, catalog.entries[0].title, 64, Ink, true);
        R(flow.heading.transform, .035f, .79f, .365f, .90f);
        view.taskContext = T("TaskContext", flowRoot.transform, "作业流程介绍 · 卸船", 27, Accent);
        view.taskContext.enableWordWrapping = false; view.taskContext.overflowMode = TextOverflowModes.Ellipsis;
        view.taskContext.enableAutoSizing = true; view.taskContext.fontSizeMin = 20; view.taskContext.fontSizeMax = 27;
        R(view.taskContext.transform, .035f, .738f, .35f, .786f);
        Rail("DescriptionRule", flowRoot.transform, .035f, .727f, .29f, .728f, ExperimentUiTheme.Border);
        var descriptionScroll = E("DescriptionScroll", flowRoot.transform);
        R(descriptionScroll.transform, .035f, .47f, .303f, .717f);
        var scroll = descriptionScroll.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true;
        scroll.scrollSensitivity = 25;
        var viewport = E("Viewport", descriptionScroll.transform); R(viewport.transform, 0, 0, 1, 1);
        Fill(viewport, new Color(0, 0, 0, .001f)); viewport.AddComponent<RectMask2D>();
        flow.description = T("Description", viewport.transform, catalog.entries[0].description, 26, Ink);
        flow.description.alignment = TextAlignmentOptions.TopLeft; flow.description.lineSpacing = 3;
        var desc = flow.description.rectTransform; desc.anchorMin = new Vector2(0, 1); desc.anchorMax = Vector2.one;
        desc.pivot = new Vector2(.5f, 1); desc.sizeDelta = Vector2.zero;
        flow.description.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport.GetComponent<RectTransform>(); scroll.content = desc;
        var strip = E("DiagramCard", flowRoot.transform); R(strip.transform, .025f, .126f, .975f, .458f);
        Fill(strip, ExperimentUiTheme.Page);
        Rail("TimelineTop", strip.transform, 0, .998f, 1, 1, ExperimentUiTheme.StrongBorder);
        Rail("TimelineBottom", strip.transform, 0, .002f, 1, .005f, ExperimentUiTheme.Border);
        var stepsHeading = T("StepsHeading", strip.transform, "岸桥卸船作业九个步骤", 28, Ink, true);
        R(stepsHeading.transform, .014f, .84f, .67f, .985f);
        flow.zoomButton = Button("Zoom", strip.transform, "放大流程图", false, 23, "Search");
        R(flow.zoomButton.transform, .865f, .84f, .994f, .97f);
        flow.diagram = E("Diagram", strip.transform).AddComponent<Image>();
        flow.diagram.preserveAspect = true; flow.diagram.sprite = catalog.entries[0].diagram;
        // 其他四种作业保留已有权威流程图，深色底上的浅色图是原始资料。
        R(flow.diagram.transform, .015f, .02f, .985f, .82f); flow.diagram.raycastTarget = false;
        var diagram = strip.AddComponent<DischargeFlowDiagram>(); diagram.originalDiagram = flow.diagram;
        diagram.stepLayout = E("DischargeStepLayout", strip.transform); R(diagram.stepLayout.transform, 0, .01f, 1, .82f);
        var blueprint = E("BlueprintMachinery", diagram.stepLayout.transform).AddComponent<RawImage>();
        blueprint.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "DischargeBlueprintStrip.png");
        blueprint.uvRect = new Rect(0, 218f / 724f, 1, 264f / 724f);
        blueprint.color = new Color(.78f, .94f, .96f, .82f); blueprint.raycastTarget = false;
        R(blueprint.transform, .006f, .03f, .994f, .68f);
        string[] captions = { "岸桥AGV就位", "海侧小车吊具抓箱", "卸箱至中转平台", "陆侧小车吊具抓箱", "卸箱至AGV", "AGV驶至海侧交换区", "AGV卸箱至支架", "轨道吊抓箱", "卸箱至堆场" };
        for (int i = 0; i < captions.Length; i++)
        {
            float x = (i + .5f) / 9;
            if (i < 8) Rail("StepConnection_" + i, diagram.stepLayout.transform, x, .917f, x + 1f / 9, .921f, ExperimentUiTheme.StrongBorder);
            var node = E("StepNode_" + i, diagram.stepLayout.transform).AddComponent<Image>();
            node.sprite = Sprite("Assets/Shift - Complete Sci-Fi UI/Textures/Border/Circle/Circle Filled.png"); node.color = Accent; node.raycastTarget = false;
            var nr = node.rectTransform; nr.anchorMin = nr.anchorMax = new Vector2(x, .92f); nr.sizeDelta = new Vector2(29, 29);
            var number = T("StepNumber_" + i, node.transform, (i + 1).ToString(), 20, ExperimentUiTheme.PrimaryText, true);
            R(number.transform, 0, 0, 1, 1); number.alignment = TextAlignmentOptions.Center;
            var caption = T("StepCaption_" + i, diagram.stepLayout.transform, captions[i], 20, Ink);
            caption.alignment = TextAlignmentOptions.Center; R(caption.transform, i / 9f, .71f, (i + 1) / 9f, .85f);
        }
        view.learnButton = Button("LearnButton", surface.transform, "学习模式", false, 31, "Tutorial");
        R(view.learnButton.transform, .035f, .029f, .225f, .096f);
        view.experimentButton = Button("ExperimentButton", surface.transform, "查看算例，开始实验", true, 32, "Play");
        R(view.experimentButton.transform, .675f, .025f, .976f, .108f);
        Save(root, "ExperimentIntroductionView");
    }

    private static void ConfigureLogin(PortLoginView view)
    {
        view.backgroundImage.sprite = Sprite(Art + "NightPortHero.png");
        view.backgroundImage.color = Color.white;
        var ratio = view.backgroundImage.GetComponent<AspectRatioFitter>(); if (ratio != null) ratio.aspectRatio = 8f / 3f;
        var tint = view.transform.Find("BackgroundTint"); if (tint != null) tint.GetComponent<Image>().color = new Color(.015f, .08f, .12f, .30f);
        var form = view.form.GetComponent<RectTransform>(); form.anchorMin = form.anchorMax = form.pivot = new Vector2(.24f, .47f);
        form.anchoredPosition = Vector2.zero; form.sizeDelta = new Vector2(650, 540);
        view.form.GetComponent<Image>().color = new Color(.04f, .14f, .19f, .97f);
        var title = view.transform.Find("SystemTitle").GetComponent<TMP_Text>();
        title.color = Ink; title.fontSize = 46; R(title.transform, .035f, .80f, .46f, .91f);
        title.alignment = TextAlignmentOptions.Center;
        ExperiencePolishMigration.ConfigureLoginView(view);
    }

    private static void ConfigureOverlay(AlgorithmRunOverlay view)
    {
        foreach (var text in view.GetComponentsInChildren<TMP_Text>(true)) text.color = Ink;
        view.algorithmText.color = view.elapsedText.color = ExperimentUiTheme.Muted;
        var panel = view.statusPanel.Find("PanelSurface").GetComponent<Image>(); panel.color = ExperimentUiTheme.PanelRaised;
        var light = panel.transform.Find("InnerLight"); if (light != null) light.GetComponent<Image>().color = new Color(.15f, .65f, .70f, .045f);
        view.SetStage(0, view.statusText.text);
    }

    private static void PatchLoginScene()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/2.unity", OpenSceneMode.Single);
        var account = UnityEngine.Object.FindObjectOfType<AccountManager>(true);
        if (account == null) throw new InvalidOperationException("登录场景的原账户控制器丢失。");
        var view = account.GetComponentInChildren<PortLoginView>(true);
        Recolor(view.gameObject); ConfigureLogin(view); view.Bind(account);
        foreach (var c in view.GetComponentsInChildren<Component>(true)) if (c != null) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        if (account.alterPanel != null)
        {
            Recolor(account.alterPanel);
            var bg = account.alterPanel.GetComponent<Image>(); if (bg != null) { bg.sprite = view.backgroundImage.sprite; bg.color = Color.white; }
        }
        EditorUtility.SetDirty(account); EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("登录场景保存失败。");
    }

    private static void Recolor(GameObject root)
    {
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            Color old = text.color;
            text.color = old.r > old.g * 1.55f && old.r > .5f ? (Color)ExperimentUiTheme.Error
                : text.name.Contains("Hint") || text.name.Contains("Placeholder") || text.name.Contains("Elapsed") || text.name.Contains("Summary")
                    ? (Color)ExperimentUiTheme.Muted : Ink;
        }
        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            string n = image.name;
            if (image.GetComponent<TMP_SubMeshUI>() != null) continue;
            if (n == "Diagram" || n == "SmartPortBackground" || n == "NightPortPanorama") continue;
            if (n == "PositionMeaningHelp") image.color = Accent;
            else if (n.Contains("Glow") || n == "PanelShadow") image.color = new Color(Accent.r, Accent.g, Accent.b, .08f);
            else if (n.Contains("Border") || n.Contains("Divider") || n.Contains("Rule") || n.Contains("Connection")) image.color = ExperimentUiTheme.Border;
            else if (n.Contains("Accent") || n.Contains("Rail") || n == "Handle" || n == "Mark") image.color = Accent;
            else if (n == "Arrow" || n.Contains("Icon") || n.Contains("Check")) image.color = Ink;
            else image.color = MapFill(image.color);
        }
        foreach (var outline in root.GetComponentsInChildren<Outline>(true)) outline.effectColor = ExperimentUiTheme.Border;
        foreach (var button in root.GetComponentsInChildren<Button>(true))
        {
            var state = button.GetComponent<ExperimentUiButtonTextState>();
            if (state != null) ExperimentUiTheme.StyleButton(button, state.Primary);
            else
            {
                var colors = button.colors; colors.normalColor = MapFill(colors.normalColor);
                colors.highlightedColor = ExperimentUiTheme.PanelHover; colors.pressedColor = ExperimentUiTheme.PanelPressed;
                colors.selectedColor = colors.normalColor; colors.disabledColor = ExperimentUiTheme.PanelRaised; button.colors = colors;
                if (button.targetGraphic != null) button.targetGraphic.color = Color.white;
            }
        }
        foreach (var input in root.GetComponentsInChildren<TMP_InputField>(true))
        {
            var image = input.GetComponent<Image>(); if (image != null) image.color = ExperimentUiTheme.PanelInput;
            input.caretColor = Accent; input.textComponent.color = Ink;
            if (input.placeholder is TMP_Text hint) hint.color = ExperimentUiTheme.Muted;
            var colors = input.colors; colors.normalColor = Color.white; colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white; colors.selectedColor = Color.white; colors.disabledColor = ExperimentUiTheme.PanelRaised; input.colors = colors;
        }
        // Prefab资源只引用持久字体；绝不保存运行时字体副本。
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            if (text.font == null || !EditorUtility.IsPersistent(text.font)) text.font = Font;
    }

    private static Color MapFill(Color old)
    {
        if (old.a < .03f) return old;
        Color[] current = { ExperimentUiTheme.Page, ExperimentUiTheme.Header, ExperimentUiTheme.Panel, ExperimentUiTheme.PanelRaised, ExperimentUiTheme.PanelInput, ExperimentUiTheme.TableHeader, ExperimentUiTheme.Alternate, ExperimentUiTheme.Border, ExperimentUiTheme.Primary, ExperimentUiTheme.Cyan };
        if (current.Any(c => Mathf.Abs(c.r-old.r)+Mathf.Abs(c.g-old.g)+Mathf.Abs(c.b-old.b)<.01f)) return old;
        Color result;
        if (old.r > .65f && old.g > .60f && old.b < .30f) result = Accent;
        else if (old.b > old.r * 1.2f && old.g > .4f && old.r < .50f) result = Accent;
        else if (old.r < .15f && old.g < .15f && old.b < .15f) result = ExperimentUiTheme.Page;
        else if (Mathf.Max(old.r, old.g, old.b) < .60f) result = ExperimentUiTheme.Header;
        else if (old.r < .90f && old.b > old.r && old.g > .72f) result = ExperimentUiTheme.TableHeader;
        else result = ExperimentUiTheme.PanelRaised;
        result.a = old.a; return result;
    }

    private static Button Button(string name, Transform parent, string label, bool primary, float size, string iconName = null)
    {
        var obj = E(name, parent); var image = obj.AddComponent<Image>();
        image.sprite = Sprite("Assets/Shift - Complete Sci-Fi UI/Textures/Border/Cut/Cut Frame Filled Big.png"); image.type = Image.Type.Sliced;
        var button = obj.AddComponent<Button>(); button.targetGraphic = image;
        var text = T("Label", obj.transform, label, size, primary ? (Color)ExperimentUiTheme.PrimaryText : Ink, true);
        R(text.transform, iconName == null ? .03f : .17f, 0, .98f, 1); text.alignment = TextAlignmentOptions.Center;
        var border = E("ButtonBorder", obj.transform).AddComponent<Image>();
        border.sprite = Sprite("Assets/Shift - Complete Sci-Fi UI/Textures/Border/Cut/Cut Frame - 2px.png");
        border.type = Image.Type.Sliced; border.raycastTarget = false; R(border.transform, 0, 0, 1, 1);
        var style = obj.AddComponent<SciFiReferenceButton>(); style.button = button; style.label = text; style.border = border;
        if (iconName != null)
        {
            style.icon = E("Icon", obj.transform).AddComponent<Image>(); style.icon.sprite = Sprite("Assets/Shift - Complete Sci-Fi UI/Textures/Icon/" + iconName + ".png");
            style.icon.preserveAspect = true; style.icon.raycastTarget = false; R(style.icon.transform, .05f, .23f, .16f, .77f);
        }
        style.SetSelected(primary); return button;
    }
    private static void Import(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { AssetDatabase.ImportAsset(path); importer = (TextureImporter)AssetImporter.GetAtPath(path); }
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
    }
    private static Sprite Sprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("素材缺失：" + path);
        if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.mipmapEnabled = false; importer.SaveAndReimport(); }
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path); if (sprite == null) throw new InvalidOperationException("Sprite缺失：" + path); return sprite;
    }
    private static GameObject E(string name, Transform parent)
    { var obj = new GameObject(name, typeof(RectTransform)); obj.layer = LayerMask.NameToLayer("UI"); if (parent != null) obj.transform.SetParent(parent, false); return obj; }
    private static TMP_Text T(string name, Transform parent, string value, float size, Color color, bool bold = false)
    { var text = E(name, parent).AddComponent<TextMeshProUGUI>(); text.font = Font; text.text = value; text.fontSize = size; text.color = color; text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal; text.raycastTarget = false; text.alignment = TextAlignmentOptions.MidlineLeft; return text; }
    private static void Fill(GameObject obj, Color color) { obj.AddComponent<Image>().color = color; }
    private static void R(Transform transform, float x0, float y0, float x1, float y1)
    { var rect = (RectTransform)transform; rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1); rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static void Rail(string name, Transform parent, float x0, float y0, float x1, float y1, Color color)
    { var obj = E(name, parent); R(obj.transform, x0, y0, x1, y1); Fill(obj, color); obj.GetComponent<Image>().raycastTarget = false; }
    private static void Patch(string path, Action<GameObject> apply)
    { var root = PrefabUtility.LoadPrefabContents(path); try { apply(root); if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new InvalidOperationException(path); } finally { PrefabUtility.UnloadPrefabContents(root); } }
    private static void Save(GameObject root, string name)
    { if (PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/" + name + ".prefab") == null) throw new InvalidOperationException(name); UnityEngine.Object.DestroyImmediate(root); }
}
