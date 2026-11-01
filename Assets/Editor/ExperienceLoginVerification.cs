using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Vtp.VR.Integration;

[InitializeOnLoad]
public static class ExperienceLoginVerification
{
    private const string Pending="ExperienceLoginVerification.Pending";
    private static IEnumerator _steps;
    private static readonly List<string> Checks=new List<string>();
    private static string Output=>Path.GetFullPath(".codex-tmp/ExperiencePolish");
    static ExperienceLoginVerification(){if(SessionState.GetBool(Pending,false))EditorApplication.playModeStateChanged+=OnPlay;}
    public static void Run()
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("仅隔离批处理工程使用。");
        Directory.CreateDirectory(Output);Checks.Clear();
        EditorSceneManager.OpenScene("Assets/Scenes/2.unity",OpenSceneMode.Single);
        Validate("Saved edit-mode scene");
        File.WriteAllLines(Path.Combine(Output,"login-checks.txt"),Checks);
        SessionState.SetBool(Pending,true);EditorApplication.playModeStateChanged-=OnPlay;EditorApplication.playModeStateChanged+=OnPlay;EditorApplication.isPlaying=true;
    }
    private static void OnPlay(PlayModeStateChange state)
    {if(state==PlayModeStateChange.EnteredPlayMode){Checks.Clear();Checks.AddRange(File.ReadAllLines(Path.Combine(Output,"login-checks.txt")));_steps=Verify();EditorApplication.update+=Tick;}}
    private static void Tick()
    {
        try
        {
            if(_steps.MoveNext())return;
            File.WriteAllLines(Path.Combine(Output,"login-checks.txt"),Checks);
            Debug.Log("EXPERIENCE_LOGIN_VERIFICATION_PASSED "+Checks.Count);Finish(0);
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText(Path.Combine(Output,"login-failure.txt"),error.ToString());Finish(1);}
    }
    private static void Finish(int code){SessionState.SetBool(Pending,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    private static PortLoginView Validate(string phase)
    {
        var account=UnityEngine.Object.FindObjectOfType<AccountManager>(true);
        var views=UnityEngine.Object.FindObjectsOfType<PortLoginView>(true);
        Check(account!=null&&views.Length==1,phase+": exactly one native login view");
        var view=views[0];
        Check(account.loginPanel==view.form&&account.userNameLoginInput==view.userName&&account.passwordLoginInput==view.password,phase+": account fields already reference saved new inputs");
        Check(account.loginButton==view.login&&account.openAlterButton==view.changePassword,phase+": saved button references are correct");
        Check(view.userNameLabel!=null&&view.userNameLabel.text=="用户名"&&view.passwordLabel!=null&&view.passwordLabel.text=="密码",phase+": persistent left-hand field labels exist");
        Check(EditorUtility.IsPersistent(view.userNameLabel.font)&&EditorUtility.IsPersistent(view.passwordLabel.font),phase+": labels retain a saved Chinese font asset");
        Check("用户名密码".All(c=>view.userNameLabel.font.HasCharacter(c,false,false)),phase+": label glyphs are already baked without runtime font mutation: "+AssetDatabase.GetAssetPath(view.userNameLabel.font));
        Check(view.userNameLabel.rectTransform.anchorMax.x<view.userName.GetComponent<RectTransform>().anchorMin.x&&view.passwordLabel.rectTransform.anchorMax.x<view.password.GetComponent<RectTransform>().anchorMin.x,phase+": labels are left of their fields");
        Check(account.transform.Find("LoginPanel")==null,phase+": old login page was removed, not just hidden at runtime");
        Check(view.backgroundImage!=null&&view.backgroundImage.sprite.name=="NightPortHero",phase+": selected Blue Harbor background is serialized in the scene");
        Check(view.gameObject.activeInHierarchy&&view.form.activeInHierarchy,phase+": new login page is visible before authentication");
        return view;
    }
    private static IEnumerator Verify()
    {
        int target=Time.frameCount+5;while(Time.frameCount<target)yield return null;
        var view=Validate("Actual play mode");
        Capture();
        var account=UnityEngine.Object.FindObjectOfType<AccountManager>(true);
        EventSystem.current.SetSelectedGameObject(view.userName.gameObject);
        typeof(AccountManager).GetMethod("MoveLoginFocus",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(account,new object[]{1});
        Check(EventSystem.current.currentSelectedGameObject==view.password.gameObject,"Tab-order focus advances from username to password");
        view.changePassword.onClick.Invoke();
        Check(account.alterPanel.activeInHierarchy&&!view.form.activeInHierarchy,"Change-password button retains original function");
        account.backToLoginButton.onClick.Invoke();
        Check(!account.alterPanel.activeInHierarchy&&view.form.activeInHierarchy,"Back button restores the saved new login form");
        view.userName.SetTextWithoutNotify(string.Empty);view.password.SetTextWithoutNotify(string.Empty);
        view.login.onClick.Invoke();
        Check(account.statusText.text.Contains("不能为空"),"Login validation is still connected, without sending credentials");
        Check(UnityEngine.Object.FindObjectsOfType<PortLoginView>(true).Length==1,"Opening and using the scene creates no second login page");
    }
    private static void Capture()
    {
        var camera=UnityEngine.Object.FindObjectsOfType<Camera>().First();
        var canvases=UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        var target=new RenderTexture(1280,720,24);target.Create();var oldTarget=camera.targetTexture;var oldAspect=camera.aspect;
        try
        {
            camera.targetTexture=target;camera.aspect=1280f/720;
            foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=Mathf.Max(1,camera.nearClipPlane+.5f);Refresh(canvas);}
            Canvas.ForceUpdateCanvases();camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,"login-saved-scene-runtime.png"),image.EncodeToPNG());
            RenderTexture.active=previous;UnityEngine.Object.Destroy(image);
        }
        finally{camera.targetTexture=oldTarget;camera.aspect=oldAspect;foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;Refresh(canvas);}Canvas.ForceUpdateCanvases();target.Release();UnityEngine.Object.Destroy(target);}
    }
    private static void Refresh(Canvas canvas){var scaler=canvas.GetComponent<CanvasScaler>();if(scaler!=null){scaler.enabled=false;scaler.enabled=true;}}
    private static void Check(bool value,string description){if(!value)throw new InvalidOperationException(description);Checks.Add("PASS: "+description);}
}
