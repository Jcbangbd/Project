using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Vtp.VR.Integration;

public class UMP_Manager : MonoBehaviour
{
    [SerializeField]
    private GameObject[] Windows = null;

    [Space(5)]
    [SerializeField]
    private GameObject backGroundMap = null;

    [Space(5)]
    public Transform RouteIn;
    public Transform RouteOut;
    public Transform RouteSide;
    public Transform Swerve1;

    [Space(5)]
    private int CurrentWindow = -1;
    public UMP_LevelInfo LastLevel { get; private set; }

    public bool IsPaused
    {
        get
        {
            if (this.CurrentWindow < 0)
                return true;
            else
                return false;
        }
    }

    [Space(5)]
    [SerializeField]
    private GameObject subtitle = null;
    public bool HasSubtilte { private get; set; }

    [Space(5)]
    public RectTransform[] Equip2Obj;

    [System.NonSerialized]
    public static UMP_Manager Instance;

    public bool ShowSubtitle
    {
        get
        {
            if (this.LastLevel && this.LastLevel.hasSubtitle && this.subtitle)
                return true;
            else
                return false;
        }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            DestroyImmediate(gameObject);
            return;
        }
    }

    IEnumerator Start()
    {
        yield return null;
        ChangeWindow(1);
    }

    public void ChangeWindow(int id)
    {
        SceneDecisionWorkspaceController workspace =
            SceneDecisionWorkspaceController.Instance;
        bool returnToIntroduction =
            workspace != null &&
            workspace.WantsReturnToIntroduction;

        if (returnToIntroduction)
        {
            // 左上角三角按钮打开菜单前，先解除三维准备阶段的锁定。
            workspace.PrepareReturnToIntroduction();
        }

        if (this.CurrentWindow == id)
        {
            if (returnToIntroduction)
            {
                workspace.ShowExperimentIntroduction();
            }
            else
            {
                ExperimentIntroductionView.RestoreForMenu(Windows[id]);
            }

            return;
        }

        Vtp.VR.Integration.ExperimentIntroductionView.HideAll();
        for (int i = 0; i < Windows.Length; i++)
        {
            Windows[i].SetActive(false);
        }

        if (this.CurrentWindow < 0)
        {
            if (!this.backGroundMap.activeInHierarchy)
                this.backGroundMap.SetActive(true);
            this.DoPause();
        }
        this.CurrentWindow = id;
        Windows[id].SetActive(true);
        ExperimentIntroductionView.RestoreForMenu(Windows[id]);
        if (this.subtitle)
            this.subtitle.SetActive(false);

        if (returnToIntroduction)
        {
            // Windows[id] 激活后再恢复岸桥卸船介绍，避免被窗口切换覆盖。
            workspace.ShowExperimentIntroduction();
        }
    }

    public void ChangeWindowWithFlowEnd(int id)
    {
        CameraStayLogger.EndFlow();
        ChangeWindow(id);
    }

    public void UserLoadingScene(UMP_LevelInfo currentLevel)
    {
        this.LastLevel = currentLevel;
        StartCoroutine(this.OnUserLoadingScene());
    }

    private IEnumerator OnUserLoadingScene()
    {
        yield return StartCoroutine(this.LastLevel.UserLoadedScene());
        this.CloseWindow();
    }

    public void CloseWindow()
    {
        Vtp.VR.Integration.ExperimentIntroductionView.HideAll();
        // 离开菜单 / 返回主界面时结束当前视角 & 当前流程一次
        CameraStayLogger.End();
        CameraStayLogger.EndFlow();

        this.UnPause();

        if (this.CurrentWindow >= 0)
            this.Windows[this.CurrentWindow].SetActive(false);
        if (this.backGroundMap)
            this.backGroundMap.SetActive(false);
        this.CurrentWindow = -1;
        if (this.LastLevel && this.LastLevel.hasSubtitle && this.subtitle)
            this.subtitle.SetActive(true);
    }

    public void ReStartScene()
    {
        if (this.LastLevel)
            this.LastLevel.OpenLevel();
    }

    private void DoPause()
    {
        CameraManager.Instance.DoPause();
        SimDriver.Instance.IsPause = true;
        Simulator.Instance.isPause = true;
        TimeHelper.Instance.DoPause();
        if (Subtitle.Instance)
            Subtitle.Instance.IsPause = true;
    }

    private void UnPause()
    {
        CameraManager.Instance.UnPause();
        SimDriver.Instance.IsPause = false;
        Simulator.Instance.isPause = false;
        TimeHelper.Instance.Unpause();
        Subtitle.Instance.IsPause = false;
        this.SetRunSpeed(this.SpeedItemID);
    }

    private int SpeedItemID = 1;
    public void SetRunSpeed(int selectedID)
    {
        this.SpeedItemID = selectedID;
        TimeHelper.Instance.TimeScale = this.SpeedItemID;
    }

    /// <summary>
    /// Quit
    /// </summary>
    public void QuitApp()
    {
        CameraStayLogger.EndFlow();
        // 退出前打印全部统计（里面会顺便调用 End / EndFlow）
        CameraStayLogger.LogAllTotals();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    [System.Serializable]
    public class LevelInfo
    {
        public string LevelName;
        [Space(5)]
        public string Title;
        public string Description;
        public Sprite Preview;
        public string ScriptMethod;
    }
}
