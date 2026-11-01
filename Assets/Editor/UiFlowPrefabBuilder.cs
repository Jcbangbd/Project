using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Vtp.VR.Integration;

public static class UiFlowPrefabBuilder
{
    private static TMP_FontAsset Font;
    private static OperationFlowCatalog Catalog;
    [MenuItem("Tools/Experiment UI/Build approved port UI")]
    public static void Build()
    {
        Font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/AlibabaPuHuiTi-3-55-Regular SDF.asset");
        Vtp.VR.Editor.ExperimentAssessmentCanvasPrefabBuilder.Build();
        BuildCatalog();
        BuildFlow(); BuildZoom(); BuildLogin(); BuildIntroduction(); BuildLoading();
        var root=E("PreExperimentQuestionnaire",null);
        root.AddComponent<PreExperimentQuestionnaireController>().BuildLayoutForEditor();
        root.SetActive(false);
        Save(root,"PreExperimentQuestionnaire");
        BlueHarborUiMigration.Apply();
        AssetDatabase.SaveAssets();
        Debug.Log("UI_FLOW_PREFABS_BUILT");
    }
    public static void BuildAndVerify()
    {
        Build();
        UiFlowRegressionChecks.ValidateSceneBindings();
        ExperimentUiVisualVerification.Run();
    }
    public static void BuildReferenceAlignmentAndVerify()
    {
        Font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/AlibabaPuHuiTi-3-55-Regular SDF.asset");
        Catalog=AssetDatabase.LoadAssetAtPath<OperationFlowCatalog>("Assets/Resources/OperationFlowCatalog.asset");
        if(Font==null || Catalog==null)throw new InvalidOperationException("缺少现有字体或流程数据。");
        BuildIntroduction();
        BuildLoading();
        BlueHarborUiMigration.Apply();
        AssetDatabase.SaveAssets();
        Debug.Log("UI_REFERENCE_ALIGNMENT_BUILT");
        UiFlowRegressionChecks.ValidateSceneBindings();
        ExperimentUiVisualVerification.Run();
    }
    private static void BuildCatalog()
    {
        Catalog=AssetDatabase.LoadAssetAtPath<OperationFlowCatalog>("Assets/Resources/OperationFlowCatalog.asset");
        if(Catalog==null) { Catalog=ScriptableObject.CreateInstance<OperationFlowCatalog>(); AssetDatabase.CreateAsset(Catalog,"Assets/Resources/OperationFlowCatalog.asset"); }
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/SceneStage1.unity",OpenSceneMode.Single);
        Transform canvas=null; foreach(var root in scene.GetRootGameObjects()) if(root.name=="Canvas") canvas=root.transform;
        string[] paths={"DischargeShip","LoadShip","Dispatch","Collect","ReOrder"};
        string[] ids={"DISCHARGE","LOAD_SHIP","LOAD_TRUCK","UNLOAD_TRUCK","REHANDLE"};
        string[] types={"卸船","装船","装车","卸车","翻箱"};
        string[] titles={"岸桥卸船流程","岸桥装船流程","场桥出箱流程","场桥进箱流程","场内移箱流程"};
        string[] shortText={"岸桥从船舶取箱，经中转平台交给AGV；AGV运至海侧交互区，再由轨道吊堆存。",
            "轨道吊从堆场取箱，经AGV送至岸桥，双小车协同将集装箱装入船舶。",
            "轨道吊从堆场取箱并装至外集卡，集卡经出港闸口离港。",
            "外集卡载箱进港，抵达交换区后，由轨道吊将集装箱卸入堆场。",
            "轨道吊在堆场内取箱，移动至目标作业点，再将集装箱放入目标箱位。"};
        Catalog.entries=new OperationFlowCatalog.Entry[5];
        for(int i=0;i<5;i++)
        {
            var text=canvas.Find("Play/Scenario/Panel/"+paths[i]+"/Panel/Image/Text").GetComponent<Text>();
            var sprite=Sprite("Assets/UMenu Pro/Content/"+(i+1)+".png");
            Catalog.entries[i]=new OperationFlowCatalog.Entry { optionId=ids[i],taskType=types[i],title=titles[i],
                description=i==4?shortText[i]:text.text.Replace("海测","海侧"),shortDescription=shortText[i],diagram=sprite };
        }
        EditorUtility.SetDirty(Catalog);
    }
    private static void BuildFlow()
    {
        var root=E("OperationFlowView",null); root.AddComponent<LayoutElement>().preferredHeight=280;
        Surface(root,ExperimentUiTheme.PanelRaised);
        var view=root.AddComponent<OperationFlowView>(); view.catalog=Catalog;
        view.heading=T("Heading",root.transform,"作业流程与介绍",32); R(view.heading.rectTransform,.02f,.82f,.73f,.99f);
        view.diagram=E("Diagram",root.transform).AddComponent<Image>(); view.diagram.preserveAspect=true;
        R(view.diagram.rectTransform,.02f,.05f,.71f,.79f);
        var descriptionArea=E("DescriptionScroll",root.transform);R(descriptionArea.GetComponent<RectTransform>(),.74f,.06f,.98f,.78f);
        var scroll=descriptionArea.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=25;
        var viewport=E("Viewport",descriptionArea.transform);Stretch(viewport);viewport.AddComponent<Image>().color=new Color(1,1,1,.01f);viewport.AddComponent<RectMask2D>();
        view.description=T("Description",viewport.transform,"",27);
        view.description.rectTransform.anchorMin=new Vector2(0,1);view.description.rectTransform.anchorMax=Vector2.one;view.description.rectTransform.pivot=new Vector2(.5f,1);view.description.rectTransform.sizeDelta=Vector2.zero;
        view.description.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport=viewport.GetComponent<RectTransform>();scroll.content=view.description.rectTransform;
        ExperimentUiTheme.Scrollbar(scroll);
        view.description.alignment=TextAlignmentOptions.TopLeft;
        view.zoomButton=B("Zoom",root.transform,"放大流程图",false); R(view.zoomButton.GetComponent<RectTransform>(),.79f,.83f,.98f,.98f);
        Save(root,"OperationFlowView");
    }
    private static void BuildZoom()
    {
        var root=CanvasRoot("FlowImageZoom",32000); Surface(root,ExperimentUiTheme.Panel);
        var view=root.AddComponent<FlowImageZoom>();
        view.title=T("Title",root.transform,"流程图",40); R(view.title.rectTransform,.04f,.89f,.75f,.98f);
        view.image=E("Diagram",root.transform).AddComponent<Image>(); view.image.preserveAspect=true;
        R(view.image.rectTransform,.03f,.08f,.97f,.86f);
        view.close=B("Close",root.transform,"关闭",true); R(view.close.GetComponent<RectTransform>(),.81f,.9f,.96f,.97f);
        Save(root,"FlowImageZoom");
    }
    private static void BuildLogin()
    {
        var root=E("PortLoginView",null); Stretch(root);
        var view=root.AddComponent<PortLoginView>();
        var background=E("SmartPortBackground",root.transform).AddComponent<Image>();
        view.backgroundImage=background;
        background.sprite=Sprite("Assets/Art/UI/Login/SmartPortBackground.png");
        Stretch(background.gameObject);
        var aspect=background.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent; aspect.aspectRatio=1672f/941f;
        var tint=E("BackgroundTint",root.transform); Stretch(tint); tint.AddComponent<Image>().color=new Color(.8f,.9f,.96f,.10f);
        var title=T("SystemTitle",root.transform,"智慧港口 VR 系统",48); title.alignment=TextAlignmentOptions.Center;
        R(title.rectTransform,.2f,.81f,.8f,.9f);
        var form=E("LoginForm",root.transform); Surface(form,new Color(1,1,1,.96f)); view.form=form;
        var rect=form.GetComponent<RectTransform>(); rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.47f); rect.sizeDelta=new Vector2(540,540);
        var label=T("Title",form.transform,"用户登录",40); R(label.rectTransform,.1f,.80f,.9f,.94f); label.alignment=TextAlignmentOptions.Center;
        view.userName=Input(form.transform,"Username","请输入登录 ID",false); R(view.userName.GetComponent<RectTransform>(),.1f,.57f,.9f,.70f);
        view.password=Input(form.transform,"Password","请输入密码",true); R(view.password.GetComponent<RectTransform>(),.1f,.37f,.9f,.50f);
        view.login=B("LoginButton",form.transform,"登录",true); R(view.login.GetComponent<RectTransform>(),.1f,.16f,.49f,.28f);
        view.changePassword=B("ChangePasswordButton",form.transform,"修改密码",false); R(view.changePassword.GetComponent<RectTransform>(),.53f,.16f,.9f,.28f);
        ExperiencePolishMigration.ConfigureLoginView(view);
        Save(root,"PortLoginView");
    }
    private static void BuildIntroduction()
    {
        var root=CanvasRoot("ExperimentIntroductionView",1000); var view=root.AddComponent<ExperimentIntroductionView>();
        var surface=E("IntroductionSurface",root.transform); R(surface.GetComponent<RectTransform>(),0,0,1,.943f);
        var background=surface.AddComponent<Image>();background.sprite=Sprite("Assets/Art/UI/Introduction/PortIntroductionBackdrop.png");background.color=Color.white;
        var title=T("SystemTitle",surface.transform,"自动化集装箱码头 VR 系统",76); title.fontStyle=FontStyles.Bold;
        title.color=ReferenceInk;R(title.rectTransform,.068f,.858f,.93f,.967f);
        var english=T("EnglishTitle",surface.transform,"AUTOMATED CONTAINER TERMINAL",27);
        english.characterSpacing=12;english.color=new Color32(75,137,179,255); R(english.rectTransform,.071f,.802f,.74f,.857f);
        var headerRail=E("HeaderBaseline",surface.transform).AddComponent<Image>();headerRail.color=new Color32(77,194,243,180);R(headerRail.rectTransform,0,.782f,1,.784f);headerRail.raycastTarget=false;
        view.tabs=new Button[5];
        for(int i=0;i<5;i++)
        {
            view.tabs[i]=ReferenceButton("FlowTab_"+i,surface.transform,Catalog.entries[i].title,i==0,null,32);
            R(view.tabs[i].GetComponent<RectTransform>(),.031f+i*.190f,.672f,.219f+i*.190f,.749f);
        }
        var flowRoot=E("OperationFlowView",surface.transform);
        R(flowRoot.GetComponent<RectTransform>(),.020f,.157f,.980f,.655f);
        ReferenceSurface(flowRoot,new Color(1,1,1,.82f),new Color32(80,199,248,255));
        var flow=flowRoot.AddComponent<OperationFlowView>();view.flow=flow;flow.catalog=Catalog;
        var marker=E("HeadingAccent",flowRoot.transform).AddComponent<Image>();marker.color=ReferenceAccent;R(marker.rectTransform,.020f,.865f,.028f,.94f);marker.raycastTarget=false;
        flow.heading=T("Heading",flowRoot.transform,Catalog.entries[0].title,44);flow.heading.fontStyle=FontStyles.Bold;flow.heading.color=ReferenceInk;R(flow.heading.rectTransform,.041f,.848f,.73f,.977f);
        var descriptionCard=E("DescriptionCard",flowRoot.transform);R(descriptionCard.GetComponent<RectTransform>(),.020f,.065f,.252f,.822f);
        ReferenceSurface(descriptionCard,new Color32(229,244,253,245),new Color32(196,229,246,255));
        var descriptionScroll=E("DescriptionScroll",descriptionCard.transform);R(descriptionScroll.GetComponent<RectTransform>(),.075f,.025f,.945f,.975f);
        var scroll=descriptionScroll.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=25;
        var viewport=E("Viewport",descriptionScroll.transform);Stretch(viewport);viewport.AddComponent<Image>().color=new Color(1,1,1,.001f);viewport.AddComponent<RectMask2D>();
        flow.description=T("Description",viewport.transform,Catalog.entries[0].description,26);flow.description.color=ReferenceInk;flow.description.alignment=TextAlignmentOptions.TopLeft;flow.description.lineSpacing=2;
        flow.description.rectTransform.anchorMin=new Vector2(0,1);flow.description.rectTransform.anchorMax=Vector2.one;flow.description.rectTransform.pivot=new Vector2(.5f,1);flow.description.rectTransform.sizeDelta=Vector2.zero;
        flow.description.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport=viewport.GetComponent<RectTransform>();scroll.content=flow.description.rectTransform;
        ExperimentUiTheme.Scrollbar(scroll);
        var diagramCard=E("DiagramCard",flowRoot.transform);R(diagramCard.GetComponent<RectTransform>(),.262f,.065f,.98f,.822f);
        ReferenceSurface(diagramCard,new Color32(237,249,255,245),new Color32(196,229,246,255));
        flow.diagram=E("Diagram",diagramCard.transform).AddComponent<Image>();flow.diagram.sprite=Catalog.entries[0].diagram;flow.diagram.preserveAspect=true;flow.diagram.raycastTarget=false;R(flow.diagram.rectTransform,.020f,.055f,.98f,.945f);
        BuildDischargeDiagram(diagramCard.transform, flow.diagram);
        flow.zoomButton=ReferenceButton("Zoom",flowRoot.transform,"放大流程图",false,"Search",24);R(flow.zoomButton.GetComponent<RectTransform>(),.866f,.857f,.979f,.956f);
        view.learnButton=ReferenceButton("LearnButton",surface.transform,"学习模式",false,"Tutorial",36);R(view.learnButton.GetComponent<RectTransform>(),.042f,.040f,.247f,.128f);
        view.experimentButton=ReferenceButton("ExperimentButton",surface.transform,"仿真决策实验",true,"Play",36);R(view.experimentButton.GetComponent<RectTransform>(),.697f,.036f,.964f,.132f);
        Save(root,"ExperimentIntroductionView");
    }
    private static void BuildLoading()
    {
        var root=CanvasRoot("AlgorithmRunOverlay",31000); var view=root.AddComponent<AlgorithmRunOverlay>();
        var group=root.AddComponent<CanvasGroup>();group.ignoreParentGroups=true;group.blocksRaycasts=true;group.interactable=true;
        var backdrop=root.AddComponent<Image>();backdrop.color=Color.white;
        var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/PortUiFrostedBackdrop.shader");
        if(shader==null)throw new InvalidOperationException("缺少模态背景柔化Shader。");
        var glass=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/PortUiFrostedBackdrop.mat");
        if(glass==null){glass=new Material(shader);AssetDatabase.CreateAsset(glass,"Assets/Resources/PortUiFrostedBackdrop.mat");}
        glass.shader=shader;glass.SetColor("_Tint",new Color(.70f,.88f,.98f,.30f));glass.SetFloat("_Radius",1.4f);EditorUtility.SetDirty(glass);backdrop.material=glass;
        var panel=E("StatusPanel",root.transform);
        var r=panel.GetComponent<RectTransform>(); r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f); r.sizeDelta=new Vector2(920,480);view.statusPanel=r;
        var shadow=E("PanelShadow",panel.transform).AddComponent<Image>();shadow.sprite=Sprite("Assets/Shift - Complete Sci-Fi UI/Textures/Glow & Shadow/Square Shadow.png");shadow.type=Image.Type.Sliced;shadow.color=new Color(.12f,.50f,.68f,.22f);Stretch(shadow.gameObject);shadow.rectTransform.offsetMin=new Vector2(-32,-40);shadow.rectTransform.offsetMax=new Vector2(32,24);shadow.raycastTarget=false;
        var panelSurface=E("PanelSurface",panel.transform);Stretch(panelSurface);ReferenceSurface(panelSurface,new Color32(245,252,255,255),new Color32(55,201,255,255));
        var gradient=E("InnerLight",panelSurface.transform).AddComponent<Image>();gradient.sprite=Sprite("Assets/Shift - Complete Sci-Fi UI/Textures/Glow & Shadow/Gradient Top.png");gradient.color=new Color(.75f,.91f,1,.18f);Stretch(gradient.gameObject);gradient.raycastTarget=false;
        var title=T("Heading",panel.transform,"正在生成调度方案",42);title.color=ReferenceInk;title.fontStyle=FontStyles.Bold;R(title.rectTransform,.35f,.731f,.94f,.89f);
        var track=Circle("SpinnerTrack",panel.transform,"Circle Outline - Stroke 20px",new Color32(195,236,252,255));PlaceNode(track.rectTransform,.278f,.812f,64);
        var spinner=Circle("Spinner",panel.transform,"Circle Outline - Stroke 20px",ReferenceAccent);PlaceNode(spinner.rectTransform,.278f,.812f,64);
        spinner.type=Image.Type.Filled;spinner.fillMethod=Image.FillMethod.Radial360;spinner.fillAmount=.32f;view.spinner=spinner.rectTransform;
        view.algorithmText=T("Algorithm",panel.transform,"所选算法",28);view.algorithmText.color=new Color32(93,132,159,255);R(view.algorithmText.rectTransform,.35f,.653f,.94f,.749f);
        string[] stages={"准备数据","执行算法","校验结果"};view.stageTexts=new TMP_Text[3];
        view.stageNodes=new Image[3];view.stageRings=new Image[3];view.stageChecks=new Image[3];view.stageHalos=new Image[3];view.stageLinks=new Image[2];
        for(int i=0;i<2;i++)
        {
            var line=E("StageConnection_"+i,panel.transform).AddComponent<Image>();line.color=new Color32(206,226,240,255);line.raycastTarget=false;
            R(line.rectTransform,.21f+i*.29f,.515f,.50f+i*.29f,.525f);view.stageLinks[i]=line;
        }
        for(int i=0;i<3;i++)
        {
            float x=.21f+i*.29f;
            view.stageHalos[i]=E("StageHalo_"+i,panel.transform).AddComponent<Image>();view.stageHalos[i].sprite=Sprite("Assets/Shift - Complete Sci-Fi UI/Textures/Glow & Shadow/Circle Glow.png");PlaceNode(view.stageHalos[i].rectTransform,x,.52f,76);view.stageHalos[i].raycastTarget=false;
            view.stageNodes[i]=Circle("StageNode_"+i,panel.transform,"Circle Filled",Color.white);PlaceNode(view.stageNodes[i].rectTransform,x,.52f,36);
            view.stageRings[i]=Circle("StageRing_"+i,panel.transform,"Circle Outline - Stroke 8px",ReferenceAccent);PlaceNode(view.stageRings[i].rectTransform,x,.52f,38);
            view.stageChecks[i]=E("StageCompleted_"+i,panel.transform).AddComponent<Image>();view.stageChecks[i].sprite=Sprite("Assets/Shift - Complete Sci-Fi UI/Textures/Icon/Check.png");view.stageChecks[i].color=Color.white;view.stageChecks[i].preserveAspect=true;PlaceNode(view.stageChecks[i].rectTransform,x,.52f,25);view.stageChecks[i].raycastTarget=false;
            view.stageTexts[i]=T("Stage_"+i,panel.transform,stages[i],29);view.stageTexts[i].fontStyle=FontStyles.Bold;R(view.stageTexts[i].rectTransform,x-.13f,.35f,x+.13f,.463f);view.stageTexts[i].alignment=TextAlignmentOptions.Center;
        }
        view.statusText=T("Status",panel.transform,"正在执行所选算法，请稍候……",30);view.statusText.color=ReferenceInk;R(view.statusText.rectTransform,.07f,.176f,.93f,.30f);view.statusText.alignment=TextAlignmentOptions.Center;view.statusText.overflowMode=TextOverflowModes.Ellipsis;
        view.elapsedText=T("Elapsed",panel.transform,"已用时 00:00",26);R(view.elapsedText.rectTransform,.1f,.075f,.9f,.175f);view.elapsedText.alignment=TextAlignmentOptions.Center;view.elapsedText.color=new Color32(93,132,159,255);
        view.SetStage(0,"正在准备本次算例与设备资源……");
        root.SetActive(false); Save(root,"AlgorithmRunOverlay");
    }
    private static readonly Color ReferenceInk=new Color32(23,62,96,255);
    private static readonly Color ReferenceAccent=new Color32(0,177,233,255);
    private static void ReferenceSurface(GameObject obj,Color fill,Color border)
    {
        var image=obj.AddComponent<Image>();image.sprite=Sprite("Assets/Shift - Complete Sci-Fi UI/Textures/Border/Cut/Cut Frame Filled Big.png");image.type=Image.Type.Sliced;image.color=fill;
        var outline=obj.AddComponent<Outline>();outline.effectColor=border;outline.effectDistance=new Vector2(1.8f,-1.8f);outline.useGraphicAlpha=false;
    }
    private static void BuildDischargeDiagram(Transform parent,Image original)
    {
        var switcher=parent.gameObject.AddComponent<DischargeFlowDiagram>();switcher.originalDiagram=original;
        var root=E("DischargeNineSteps",parent);R(root.GetComponent<RectTransform>(),.02f,.02f,.98f,.98f);switcher.stepLayout=root;
        var line=E("MainSequence",root.transform).AddComponent<Image>();line.color=new Color32(228,157,51,255);line.raycastTarget=false;R(line.rectTransform,.055f,.496f,.945f,.504f);
        string[] captions={"1. 岸桥AGV就位","2. 海侧小车吊具抓箱作业","3. 卸箱至中转平台","4. 陆侧小车吊具抓箱作业","5. 卸箱至AGV","6. AGV行驶至海侧交换区","7. AGV卸箱至支架","8. 轨道吊抓箱作业","9. 卸箱至堆场"};
        float[] sourceCenters={96.5f,218f,347.5f,473f,586.5f,712f,842f,960.5f,1093.5f};
        float[] labelY={.20f,.74f,.035f,.90f,.20f,.74f,.035f,.90f,.20f};
        for(int i=0;i<9;i++)
        {
            float x=.055f+i*.11125f;bool above=labelY[i]>.5f;
            Color category=i<4?(Color)new Color32(130,43,194,255):i<7?(Color)new Color32(26,158,139,255):new Color32(35,118,207,255);
            float a=above?.62f:labelY[i]+.055f;float b=above?labelY[i]-.055f:.36f;
            for(int dot=0;dot<7;dot++)
            {var dotted=Circle("Node"+i+"_Dot"+dot,root.transform,"Circle Filled",category);PlaceNode(dotted.rectTransform,x,Mathf.Lerp(a,b,dot/6f),6);}
            var icon=E("OriginalIcon_"+i,root.transform).AddComponent<RawImage>();icon.texture=original.sprite.texture;
            icon.uvRect=new Rect((sourceCenters[i]-49f)/1192f,1f-279f/445f,98f/1192f,130f/445f);icon.raycastTarget=false;
            PlaceNode(icon.rectTransform,x,.50f,108);icon.rectTransform.sizeDelta=new Vector2(108,108*130f/98f);
            var caption=E("StepCaption_"+i,root.transform);R(caption.GetComponent<RectTransform>(),Mathf.Clamp(x-.11f,0,.78f),labelY[i]-.05f,Mathf.Clamp(x+.11f,.22f,1),labelY[i]+.05f);
            var fill=caption.AddComponent<Image>();fill.color=new Color32(239,248,255,255);fill.raycastTarget=false;
            var stroke=caption.AddComponent<Outline>();stroke.effectColor=category;stroke.effectDistance=new Vector2(1,-1);stroke.useGraphicAlpha=false;
            var text=T("Caption",caption.transform,captions[i],22);text.color=category;text.alignment=TextAlignmentOptions.Center;R(text.rectTransform,.01f,.02f,.99f,.98f);text.enableWordWrapping=false;
        }
        switcher.Show("DISCHARGE");
    }
    private static Button ReferenceButton(string name,Transform parent,string label,bool selected,string iconName,float size)
    {
        var obj=E(name,parent);var image=obj.AddComponent<Image>();image.sprite=Sprite("Assets/Shift - Complete Sci-Fi UI/Textures/Border/Cut/Cut Frame Filled Big.png");image.type=Image.Type.Sliced;
        var button=obj.AddComponent<Button>();button.targetGraphic=image;
        var glow=E("ActiveGlow",obj.transform).AddComponent<Image>();glow.sprite=Sprite("Assets/Shift - Complete Sci-Fi UI/Textures/Border/Cut/Cut Frame Glow - 3px.png");glow.type=Image.Type.Sliced;glow.color=new Color(0,.75f,1,.65f);Stretch(glow.gameObject);glow.rectTransform.offsetMin=new Vector2(-7,-7);glow.rectTransform.offsetMax=new Vector2(7,7);glow.raycastTarget=false;
        var edge=E("Frame",obj.transform).AddComponent<Image>();edge.sprite=Sprite("Assets/Shift - Complete Sci-Fi UI/Textures/Border/Cut/Cut Frame - 2px.png");edge.type=Image.Type.Sliced;Stretch(edge.gameObject);edge.raycastTarget=false;
        var text=T("Label",obj.transform,label,size);text.fontStyle=FontStyles.Bold;text.alignment=TextAlignmentOptions.Center;R(text.rectTransform,iconName==null?.02f:.28f,.05f,.96f,.95f);
        Image icon=null;
        if(iconName!=null){icon=E("Icon",obj.transform).AddComponent<Image>();icon.sprite=Sprite("Assets/Shift - Complete Sci-Fi UI/Textures/Icon/"+iconName+".png");icon.preserveAspect=true;icon.raycastTarget=false;R(icon.rectTransform,.14f,.24f,.27f,.76f);}
        var style=obj.AddComponent<SciFiReferenceButton>();style.button=button;style.label=text;style.border=edge;style.glow=glow;style.icon=icon;style.SetSelected(selected);
        return button;
    }
    private static Image Circle(string name,Transform parent,string spriteName,Color color)
    {
        var image=E(name,parent).AddComponent<Image>();image.sprite=Sprite("Assets/Shift - Complete Sci-Fi UI/Textures/Border/Circle/"+spriteName+".png");image.color=color;image.raycastTarget=false;return image;
    }
    private static void PlaceNode(RectTransform rect,float x,float y,float size)
    {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(x,y);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(size,size);}
    private static TMP_InputField Input(Transform parent,string name,string placeholder,bool password)
    {
        var obj=E(name,parent); obj.AddComponent<Image>(); var input=obj.AddComponent<TMP_InputField>();
        var area=E("Text Area",obj.transform); R(area.GetComponent<RectTransform>(),.04f,.05f,.96f,.95f); area.AddComponent<RectMask2D>();
        var text=T("Text",area.transform,"",30); Stretch(text.gameObject);
        var hint=T("Placeholder",area.transform,placeholder,30); Stretch(hint.gameObject); hint.color=ExperimentUiTheme.Muted;
        input.textViewport=area.GetComponent<RectTransform>(); input.textComponent=text; input.placeholder=hint;
        input.contentType=password?TMP_InputField.ContentType.Password:TMP_InputField.ContentType.Standard;
        ExperimentUiTheme.StyleInput(input); return input;
    }
    private static GameObject CanvasRoot(string name,int order)
    {
        var root=E(name,null); var canvas=root.AddComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.overrideSorting=true; canvas.sortingOrder=order;
        var scaler=root.AddComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
        root.AddComponent<GraphicRaycaster>(); return root;
    }
    private static Sprite Sprite(string path)
    {
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        if(importer.textureType!=TextureImporterType.Sprite) { importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single; importer.mipmapEnabled=false; importer.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    private static GameObject E(string name,Transform parent)
    { var obj=new GameObject(name,typeof(RectTransform)); if(parent!=null)obj.transform.SetParent(parent,false); obj.layer=LayerMask.NameToLayer("UI"); return obj; }
    private static TMP_Text T(string name,Transform parent,string value,float size)
    { var text=E(name,parent).AddComponent<TextMeshProUGUI>();text.font=Font;text.text=value;text.fontSize=size;text.color=ExperimentUiTheme.TextColor;text.raycastTarget=false;text.enableWordWrapping=true;text.alignment=TextAlignmentOptions.MidlineLeft;return text; }
    private static Button B(string name,Transform parent,string label,bool primary)
    {var obj=E(name,parent);obj.AddComponent<Image>();var button=obj.AddComponent<Button>();button.targetGraphic=obj.GetComponent<Image>();var text=T("Label",obj.transform,label,30);Stretch(text.gameObject);text.alignment=TextAlignmentOptions.Center;ExperimentUiTheme.StyleButton(button,primary);return button;}
    private static void Surface(GameObject obj,Color color) {ExperimentUiTheme.SciFiSurface(obj.AddComponent<Image>(),color,true);}
    private static void Stretch(GameObject obj) {R(obj.GetComponent<RectTransform>(),0,0,1,1);}
    private static void R(RectTransform rect,float x0,float y0,float x1,float y1) {rect.anchorMin=new Vector2(x0,y0);rect.anchorMax=new Vector2(x1,y1);rect.offsetMin=rect.offsetMax=Vector2.zero;}
    private static void Save(GameObject obj,string name)
    {
        foreach(var text in obj.GetComponentsInChildren<TMP_Text>(true))
            if(text.font==null||!EditorUtility.IsPersistent(text.font))text.font=Font;
        var saved=PrefabUtility.SaveAsPrefabAsset(obj,"Assets/Resources/"+name+".prefab");
        if(saved==null)throw new InvalidOperationException("预制体保存失败："+name);
        UnityEngine.Object.DestroyImmediate(obj);AssetDatabase.SaveAssets();
    }
}
