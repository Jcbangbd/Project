using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Vtp.VR.Integration;

// 仅显式调用的隔离工程迁移/检查入口，不在用户打开的编辑器自动改场景。
public static class ExperiencePolishMigration
{
    public static void ApplyAndVerifyLogin()
    {
        Apply();
        ExperienceLoginVerification.Run();
    }
    public static void Apply()
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("请在隔离批处理工程中运行。");
        PatchPrefab("Assets/Resources/PortLoginView.prefab",root=>ConfigureLoginView(root.GetComponent<PortLoginView>()));
        PatchPrefab("Assets/Resources/ExperimentAssessmentCanvas.prefab",root=>
        {
            var page=root.GetComponentInChildren<ExperimentExampleSelectionCanvasView>(true);
            var surface=page.transform.Find("ExampleSelectionSurface");
            AddPositionHelp(surface);
            var summary=page.SummaryText.rectTransform;
            summary.offsetMax=new Vector2(-145,summary.offsetMax.y);
            if(page.FallbackNotice!=null)page.FallbackNotice.Hide();
        });
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/2.unity",OpenSceneMode.Single);
        var account=UnityEngine.Object.FindObjectOfType<AccountManager>(true);
        if(account==null)throw new InvalidOperationException("登录场景没有AccountManager。");
        var canvas=account.GetComponent<Canvas>();
        if(canvas==null)throw new InvalidOperationException("登录管理器不在原Canvas中。");
        var view=canvas.GetComponentInChildren<PortLoginView>(true);
        if(view==null)
        {
            var old=account.loginPanel;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/PortLoginView.prefab");
            view=((GameObject)PrefabUtility.InstantiatePrefab(prefab,canvas.transform)).GetComponent<PortLoginView>();
            view.name="PortLoginView";
            view.transform.SetAsFirstSibling();
            view.Bind(account);
            if(old!=null)UnityEngine.Object.DestroyImmediate(old);
            var legacyTitle=canvas.transform.Find("Text (TMP)");
            if(legacyTitle!=null&&legacyTitle.GetComponent<TMP_Text>()!=null&&legacyTitle.GetComponent<TMP_Text>().text.Contains("智慧港口"))
                UnityEngine.Object.DestroyImmediate(legacyTitle.gameObject);
        }
        ConfigureLoginView(view);
        view.Bind(account);
        ExperimentUiTheme.Stretch(view.GetComponent<RectTransform>());
        view.transform.localScale=Vector3.one;
        view.gameObject.SetActive(true);view.form.SetActive(true);
        view.userName.SetTextWithoutNotify(string.Empty);view.password.SetTextWithoutNotify(string.Empty);
        foreach(var component in view.GetComponentsInChildren<Component>(true))
            if(component!=null)PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        var scaler=canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        if(account.alterPanel!=null)
        {
            var background=account.alterPanel.GetComponent<Image>();
            if(background!=null)background.sprite=view.backgroundImage.sprite;
            account.alterPanel.SetActive(false);account.alterPanel.transform.SetAsLastSibling();
        }
        if(account.statusText!=null){account.statusText.gameObject.SetActive(false);account.statusText.transform.SetAsLastSibling();}
        if(account.loadingPanel!=null){account.loadingPanel.SetActive(false);account.loadingPanel.transform.SetAsLastSibling();}
        EditorUtility.SetDirty(account);
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("登录场景保存失败。");
        var report=new StringBuilder();Dump(canvas.transform,report,0);
        Directory.CreateDirectory(".codex-tmp/ExperiencePolish");
        File.WriteAllText(".codex-tmp/ExperiencePolish/scene-after.txt",report.ToString());
        AssetDatabase.SaveAssets();
        Debug.Log("EXPERIENCE_MIGRATION_COMPLETE");
    }

    private static void PatchPrefab(string path,Action<GameObject> patch)
    {
        var root=PrefabUtility.LoadPrefabContents(path);
        try{patch(root);if(PrefabUtility.SaveAsPrefabAsset(root,path)==null)throw new InvalidOperationException("预制体保存失败："+path);}
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }

    public static void ConfigureLoginView(PortLoginView view)
    {
        if(view==null)throw new InvalidOperationException("登录视图组件缺失。");
        ((RectTransform)view.form.transform).sizeDelta=new Vector2(650,540);
        view.userNameLabel=LoginLabel(view,"UserNameLabel","用户名",.57f,.70f);
        view.passwordLabel=LoginLabel(view,"PasswordLabel","密码",.37f,.50f);
        Place(view.userName.GetComponent<RectTransform>(),.29f,.57f,.93f,.70f);
        Place(view.password.GetComponent<RectTransform>(),.29f,.37f,.93f,.50f);
        ((TMP_Text)view.userName.placeholder).text="请输入用户名";
        ((TMP_Text)view.password.placeholder).text="请输入密码";
    }
    private static TMP_Text LoginLabel(PortLoginView view,string name,string label,float y0,float y1)
    {
        var existing=view.form.transform.Find(name);
        var text=existing==null?ExperimentUiTheme.Text(name,view.form.transform,label,30,ExperimentUiTheme.TextColor):existing.GetComponent<TMP_Text>();
        text.font=PersistentFont();
        text.fontSharedMaterial=text.font.material;
        text.text=label;text.fontSize=30;text.alignment=TextAlignmentOptions.MidlineLeft;text.raycastTarget=false;
        Place(text.rectTransform,.065f,y0,.27f,y1);return text;
    }
    public static void AddPositionHelp(Transform surface)
    {
        var existing=surface.Find("PositionMeaningHelp");
        if(existing!=null)
        {
            foreach(var text in existing.GetComponentsInChildren<TMP_Text>(true))
            {text.font=PersistentFont();text.fontSharedMaterial=text.font.material;}
            return;
        }
        var icon=new GameObject("PositionMeaningHelp",typeof(RectTransform),typeof(Image),typeof(Button),typeof(PositionMeaningTooltip));
        icon.layer=LayerMask.NameToLayer("UI");icon.transform.SetParent(surface,false);
        var rect=icon.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(1,1);rect.pivot=new Vector2(.5f,.5f);
        rect.anchoredPosition=new Vector2(-94,-137);rect.sizeDelta=new Vector2(44,44);
        var ring=icon.GetComponent<Image>();ring.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Shift - Complete Sci-Fi UI/Textures/Border/Circle/Circle Outline - Stroke 8px.png");
        ring.color=ExperimentUiTheme.Cyan;icon.GetComponent<Button>().targetGraphic=ring;
        var mark=ExperimentUiTheme.Text("Exclamation",icon.transform,"!",32,ExperimentUiTheme.TextColor);
        mark.font=PersistentFont();
        ExperimentUiTheme.Stretch(mark.rectTransform);mark.alignment=TextAlignmentOptions.Center;mark.fontStyle=FontStyles.Bold;mark.raycastTarget=false;
        var panel=new GameObject("PositionMeaningPanel",typeof(RectTransform),typeof(Image));panel.layer=icon.layer;panel.transform.SetParent(icon.transform,false);
        var panelRect=panel.GetComponent<RectTransform>();panelRect.anchorMin=panelRect.anchorMax=new Vector2(1,0);panelRect.pivot=new Vector2(1,1);
        panelRect.anchoredPosition=new Vector2(0,-10);panelRect.sizeDelta=new Vector2(760,280);
        ExperimentUiTheme.SciFiSurface(panel.GetComponent<Image>(),ExperimentUiTheme.Panel,true);
        panel.GetComponent<Image>().raycastTarget=false;
        var description=ExperimentUiTheme.Text("PositionMeaningText",panel.transform,PositionMeaningTooltip.Explanation,27,ExperimentUiTheme.TextColor);
        description.font=PersistentFont();
        ExperimentUiTheme.Stretch(description.rectTransform);description.rectTransform.offsetMin=new Vector2(24,16);description.rectTransform.offsetMax=new Vector2(-24,-16);
        description.alignment=TextAlignmentOptions.TopLeft;description.raycastTarget=false;
        icon.GetComponent<PositionMeaningTooltip>().Configure(panel);
    }
    private static TMP_FontAsset PersistentFont()
    {
        const string path="Assets/Resources/Fonts/ExperienceHelp SDF.asset";
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if(font!=null)return font;
        var skin=Resources.Load<ExperimentUiSkin>("ExperimentUiSkin");
        if(skin==null||skin.fontSource==null)throw new InvalidOperationException("缺少中文字体源文件。");
        font=TMP_FontAsset.CreateFontAsset(skin.fontSource);
        font.name="ExperienceHelp SDF";
        string characters=new string((PositionMeaningTooltip.Explanation+"用户名密码!0123456789 /：").Where(c=>!char.IsControl(c)).Distinct().ToArray());
        string missing;
        if(!font.TryAddCharacters(characters,out missing))throw new InvalidOperationException("说明字体缺字："+missing);
        // 直接烘焙并保存所需字形，编辑态和运行态均不依赖临时动态字体或测试补字。
        font.atlasPopulationMode=AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(font,path);
        foreach(var atlas in font.atlasTextures)
        {atlas.name="ExperienceHelp Atlas";if(!EditorUtility.IsPersistent(atlas))AssetDatabase.AddObjectToAsset(atlas,font);}
        font.material.name="ExperienceHelp Material";
        if(!EditorUtility.IsPersistent(font.material))AssetDatabase.AddObjectToAsset(font.material,font);
        EditorUtility.SetDirty(font);AssetDatabase.SaveAssets();
        return font;
    }
    private static void Place(RectTransform rect,float x0,float y0,float x1,float y1)
    {rect.anchorMin=new Vector2(x0,y0);rect.anchorMax=new Vector2(x1,y1);rect.offsetMin=rect.offsetMax=Vector2.zero;}

    public static void Inspect()
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("请在隔离批处理工程中运行。");
        var report=new StringBuilder();
        foreach(var path in new[]{"Assets/Scenes/2.unity","Assets/Scenes/SceneEntrance.unity","Assets/Scenes/SceneStage1.unity"})
        {
            var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            report.AppendLine("SCENE "+path);
            foreach(var root in scene.GetRootGameObjects())
            {
                foreach(var account in root.GetComponentsInChildren<AccountManager>(true))
                {
                    report.AppendLine("ACCOUNT "+PathOf(account.transform));
                    report.AppendLine("LOGIN "+(account.loginPanel==null?"null":PathOf(account.loginPanel.transform)));
                    report.AppendLine("ALTER "+(account.alterPanel==null?"null":PathOf(account.alterPanel.transform)));
                    report.AppendLine("STATUS "+(account.statusText==null?"null":PathOf(account.statusText.transform)));
                    report.AppendLine("LOADING "+(account.loadingPanel==null?"null":PathOf(account.loadingPanel.transform)));
                    report.AppendLine("USERNAME "+(account.userNameLoginInput==null?"null":PathOf(account.userNameLoginInput.transform)));
                    var canvas=account.loginPanel==null?null:account.loginPanel.GetComponentInParent<Canvas>(true);
                    if(canvas!=null)Dump(canvas.transform,report,0);
                }
            }
        }
        Directory.CreateDirectory(".codex-tmp/ExperiencePolish");
        File.WriteAllText(".codex-tmp/ExperiencePolish/scene-before.txt",report.ToString());
        Debug.Log("EXPERIENCE_INSPECTION_COMPLETE");
    }
    private static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    private static void Dump(Transform t,StringBuilder output,int depth)
    {
        if(depth>4)return;
        var r=t as RectTransform;
        output.AppendLine(new string(' ',depth*2)+t.name+" active="+t.gameObject.activeSelf+
            (r==null?"":" size="+r.sizeDelta+" anchors="+r.anchorMin+"/"+r.anchorMax)+
            " components="+string.Join(",",Array.ConvertAll(t.GetComponents<Component>(),x=>x==null?"MISSING":x.GetType().Name)));
        for(int i=0;i<t.childCount;i++)Dump(t.GetChild(i),output,depth+1);
    }
}
