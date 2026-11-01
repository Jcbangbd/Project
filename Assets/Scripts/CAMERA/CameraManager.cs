//#define IsRigibodyCamera
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityStandardAssets.Characters.FirstPerson;
using UnityStandardAssets.Utility;

public class CameraManager : MonoBehaviour
{
    public FreeCamera mainCamera;
    [SerializeField]
    FreeCamera splitCamera1 = null;
    [SerializeField]
    FreeCamera splitCamera2 = null;
    [SerializeField]
    FreeCamera splitCamera3 = null;
    [SerializeField]
    Image splitH = null;
    [SerializeField]
    Image splitV = null;
    [SerializeField]
    GameObject FirstPerson = null;

#if IsRigibodyCamera
    RigidbodyFirstPersonController FirstPersonController;
#else
    FirstPersonController FirstPersonController;
#endif

    [SerializeField]
    WaypointProgressTracker wanderController = null;

    public Vector3 DefaultMainPosition { private get; set; }
    public Vector3 DefaultSubPosition { private get; set; }
    public Vector3 DefaultMainEulerAngles { private get; set; }
    public Vector3 DefaultSubEulerAngles { private get; set; }
    public Vector3 DefaultPersonPosition { private get; set; }

    private UserSplitMode splitMode = UserSplitMode.OnlyMain;
    private FreeCamera currenCamera;

    [HideInInspector]
    public static CameraManager Instance;

    void Awake()
    {
        if (Instance)
        {
            throw new UnityException("There is a Camera Manager yet!");
        }
        Instance = this;

#if IsRigibodyCamera
        this.FirstPersonController = this.FirstPerson.GetComponent<RigidbodyFirstPersonController>();
#else
        this.FirstPersonController = this.FirstPerson.GetComponent<FirstPersonController>();
#endif

        this.OnChangeCameraMode(1);
        this.currentCameraMode = UserCameraMode.FirstPerson;
        this.wanderController.IsOn = false;
        this.wanderController.gameObject.SetActive(false);

        // ★ 初始时记录一次视角
        LogCurrentView();
    }

    bool isPaused = true;
    public void DoPause()
    {
        this.isPaused = true;
        if (this.currentCameraMode == UserCameraMode.FirstPerson)
        {
            this.FirstPersonController.IsOn = false;
            return;
        }
        else if (this.currentCameraMode == UserCameraMode.Wander)
        {
            this.wanderController.IsOn = false;
            return;
        }

        this.currenCamera.IsOn = false;
    }

    public void UnPause()
    {
        this.isPaused = false;
        if (this.currentCameraMode == UserCameraMode.FirstPerson)
        {
            this.FirstPersonController.IsOn = true;
            return;
        }
        else if (this.currentCameraMode == UserCameraMode.Wander)
        {
            this.wanderController.IsOn = true;
            return;
        }

        this.currenCamera.IsOn = true;
    }

    public void FocusOn(Transform obj, Vector3? offset, Vector3? rot)
    {
        if (this.currentCameraMode == UserCameraMode.FirstPerson || this.currentCameraMode == UserCameraMode.Wander)
            return;
        this.currenCamera.FocusOn(obj, offset, rot);
    }

    public void UnFocus()
    {
        if (this.currentCameraMode == UserCameraMode.FirstPerson || this.currentCameraMode == UserCameraMode.Wander)
            return;
        this.currenCamera.UnFocus();
    }

    void Update()
    {
        if (isPaused || this.currentCameraMode == UserCameraMode.FirstPerson || this.currentCameraMode == UserCameraMode.Wander)
            return;

        switch (this.splitMode)
        {
            case UserSplitMode.OnlyMain:
                break;
            case UserSplitMode.MainPlus:
            case UserSplitMode.TopDown:
            case UserSplitMode.LeftRight:
                if (this.splitCamera1.CheckContainMouse())
                {
                    this.mainCamera.IsOn = false;
                    this.currenCamera = this.splitCamera1;
                }
                else
                {
                    this.splitCamera1.IsOn = false;
                    this.currenCamera = this.mainCamera;
                }
                break;
            case UserSplitMode.SplitFour:
                if (this.splitCamera1.CheckContainMouse())
                {
                    this.currenCamera = this.splitCamera1;
                    this.currenCamera.IsOn = true;
                    this.mainCamera.IsOn = false;
                    this.splitCamera2.IsOn = false;
                    this.splitCamera3.IsOn = false;
                }
                else if (this.splitCamera2.CheckContainMouse())
                {
                    this.currenCamera = this.splitCamera2;
                    this.currenCamera.IsOn = true;
                    this.mainCamera.IsOn = false;
                    this.splitCamera1.IsOn = false;
                    this.splitCamera3.IsOn = false;
                }
                else if (this.splitCamera3.CheckContainMouse())
                {
                    this.currenCamera = this.splitCamera3;
                    this.currenCamera.IsOn = true;
                    this.mainCamera.IsOn = false;
                    this.splitCamera1.IsOn = false;
                    this.splitCamera2.IsOn = false;
                }
                else if (this.mainCamera.CheckContainMouse())
                {
                    this.currenCamera = this.mainCamera;
                    this.currenCamera.IsOn = true;
                    this.splitCamera1.IsOn = false;
                    this.splitCamera2.IsOn = false;
                    this.splitCamera3.IsOn = false;
                }
                break;
            default:
                break;
        }
        this.currenCamera.IsOn = true;

        // ★ 分屏下用鼠标切换 currentCamera 后，每帧记录一次（同 id 会被忽略）
        LogCurrentView();
    }

    public void OnBeginDemo()
    {
        this.mainCamera.transform.position = this.DefaultMainPosition;
        this.mainCamera.transform.eulerAngles = this.DefaultMainEulerAngles;
        if (this.splitCamera1.gameObject.activeInHierarchy)
        {
            this.splitCamera1.transform.position = this.DefaultSubPosition;
            this.splitCamera1.transform.eulerAngles = this.DefaultSubEulerAngles;
        }
        if (this.FirstPerson.gameObject.activeInHierarchy)
        {
            this.FirstPerson.transform.position = this.DefaultPersonPosition;
        }
        if (this.wanderController.gameObject.activeInHierarchy)
        {
            this.wanderController.Reset();
        }
    }

    public UserCameraMode currentCameraMode { get; private set; }

    public void OnChangeCameraMode(int indx)
    {
        UserCameraMode mode = (UserCameraMode)indx;
        if (currentCameraMode == mode)
            return;

        currentCameraMode = mode;

        if (currentCameraMode != UserCameraMode.FreeCamera)
        {
            this.mainCamera.gameObject.SetActive(false);
            this.splitCamera1.gameObject.SetActive(false);
            this.splitCamera2.gameObject.SetActive(false);
            this.splitCamera3.gameObject.SetActive(false);
            if (this.splitH)
                this.splitH.gameObject.SetActive(false);
            if (this.splitV)
                this.splitV.gameObject.SetActive(false);

            if (this.currentCameraMode == UserCameraMode.FirstPerson)
            {
                this.wanderController.IsOn = false;
                this.wanderController.gameObject.SetActive(false);
                this.FirstPerson.SetActive(true);
                this.FirstPersonController.IsOn = isPaused ? false : true;
            }
            else
            {
                this.FirstPersonController.IsOn = false;
                this.FirstPerson.SetActive(false);
                this.wanderController.gameObject.SetActive(true);
                this.wanderController.IsOn = isPaused ? false : true;
                this.wanderController.Reset();
            }

            // ★ 切到第一人称或漫游后，记录当前视角
            LogCurrentView();
        }
        else
        {
            this.FirstPersonController.IsOn = false;
            this.FirstPerson.SetActive(false);
            this.wanderController.IsOn = false;
            this.wanderController.gameObject.SetActive(false);

            this.OnChangeSplitMode((int)this.splitMode);

            // ★ 切到自由视角 + 分屏设置好之后，记录当前视角
            LogCurrentView();
            return;
        }
    }

    public void OnChangeSplitMode(int indx)
    {
        UserSplitMode mode = (UserSplitMode)indx;
        if (this.currentCameraMode != UserCameraMode.FreeCamera)
            return;

        switch (mode)
        {
            case UserSplitMode.OnlyMain:
                this.currenCamera = mainCamera;
                this.currenCamera.gameObject.SetActive(true);
                this.splitCamera1.gameObject.SetActive(false);
                this.splitCamera2.gameObject.SetActive(false);
                this.splitCamera3.gameObject.SetActive(false);
                this.mainCamera.ChangeCameraRect(new Rect(0, 0, 1, 1));
                if (this.splitH)
                    this.splitH.gameObject.SetActive(false);
                if (this.splitV)
                    this.splitV.gameObject.SetActive(false);
                break;
            case UserSplitMode.MainPlus:
                this.currenCamera = mainCamera;
                this.currenCamera.gameObject.SetActive(true);
                this.splitCamera1.gameObject.SetActive(true);
                this.splitCamera2.gameObject.SetActive(false);
                this.splitCamera3.gameObject.SetActive(false);
                this.mainCamera.ChangeCameraRect(new Rect(0, 0, 1, 1));
                this.splitCamera1.ChangeCameraRect(new Rect(0.502f, 0, 0.498f, 0.498f));
                if (this.splitH)
                {
                    this.splitH.gameObject.SetActive(true);
                    this.splitH.rectTransform.anchorMin = new Vector2(0.498f, 0.498f);
                    this.splitH.rectTransform.anchorMax = new Vector2(1f, 0.502f);
                }
                if (this.splitV)
                {
                    this.splitV.gameObject.SetActive(true);
                    this.splitV.rectTransform.anchorMin = new Vector2(0.498f, 0);
                    this.splitV.rectTransform.anchorMax = new Vector2(0.502f, 0.502f);
                }
                break;
            case UserSplitMode.TopDown:
                this.currenCamera = mainCamera;
                this.currenCamera.gameObject.SetActive(true);
                this.splitCamera1.gameObject.SetActive(true);
                this.splitCamera2.gameObject.SetActive(false);
                this.splitCamera3.gameObject.SetActive(false);
                this.mainCamera.ChangeCameraRect(new Rect(0, 0, 1, 0.5f));
                this.splitCamera1.ChangeCameraRect(new Rect(0, 0.5f, 1f, 0.5f));
                if (this.splitH)
                {
                    this.splitH.gameObject.SetActive(true);
                    this.splitH.rectTransform.anchorMin = new Vector2(0f, 0.498f);
                    this.splitH.rectTransform.anchorMax = new Vector2(1f, 0.502f);
                }
                if (this.splitV)
                    this.splitV.gameObject.SetActive(false);
                break;
            case UserSplitMode.LeftRight:
                this.currenCamera = mainCamera;
                this.currenCamera.gameObject.SetActive(true);
                this.splitCamera1.gameObject.SetActive(true);
                this.splitCamera2.gameObject.SetActive(false);
                this.splitCamera3.gameObject.SetActive(false);
                this.mainCamera.ChangeCameraRect(new Rect(0, 0, 0.5f, 1f));
                this.splitCamera1.ChangeCameraRect(new Rect(0.5f, 0, 0.5f, 1f));
                if (this.splitH)
                    this.splitH.gameObject.SetActive(false);
                if (this.splitV)
                {
                    this.splitV.gameObject.SetActive(true);
                    this.splitV.rectTransform.anchorMin = new Vector2(0.498f, 0f);
                    this.splitV.rectTransform.anchorMax = new Vector2(0.502f, 1f);
                }
                break;
            case UserSplitMode.SplitFour:
                this.currenCamera = mainCamera;
                this.currenCamera.gameObject.SetActive(true);
                this.splitCamera1.gameObject.SetActive(true);
                this.splitCamera2.gameObject.SetActive(true);
                this.splitCamera3.gameObject.SetActive(true);
                this.mainCamera.ChangeCameraRect(new Rect(0, 0, 0.5f, 0.5f));
                this.splitCamera1.ChangeCameraRect(new Rect(0, 0.5f, 0.5f, 0.5f));
                this.splitCamera2.ChangeCameraRect(new Rect(0.5f, 0.5f, 0.5f, 0.5f));
                this.splitCamera3.ChangeCameraRect(new Rect(0.5f, 0, 0.5f, 0.5f));
                if (this.splitH)
                {
                    this.splitH.gameObject.SetActive(true);
                    this.splitH.rectTransform.anchorMin = new Vector2(0f, 0.498f);
                    this.splitH.rectTransform.anchorMax = new Vector2(1f, 0.502f);
                }
                if (this.splitV)
                {
                    this.splitV.gameObject.SetActive(true);
                    this.splitV.rectTransform.anchorMin = new Vector2(0.498f, 0f);
                    this.splitV.rectTransform.anchorMax = new Vector2(0.502f, 1f);
                }
                break;
            default:
                break;
        }
        this.splitMode = mode;

        // ★ 分屏布局改变后记录当前视角
        LogCurrentView();
    }

    public void OnChangeUserView(int indx)
    {
        if (this.currentCameraMode == UserCameraMode.Wander)
            return;

        if (this.currentCameraMode != UserCameraMode.FirstPerson)
        {
            if (!this.mainCamera.isActiveAndEnabled)
                return;
            switch (indx)
            {
                case 0: // 进港闸口
                    this.mainCamera.transform.position = new Vector3(-219, 28, -357f);
                    this.mainCamera.transform.eulerAngles = new Vector3(2167.086f, -94f, 0);
                    break;
                case 1: // 出港闸口
                    this.mainCamera.transform.position = new Vector3(2167.086f, 24.90154f, -514f);
                    this.mainCamera.transform.eulerAngles = new Vector3(21.562f, -55.409f, 0);
                    break;
                case 2: // 码头前沿
                    this.mainCamera.transform.position = new Vector3(1137.059f, 52.2803f, -103.0995f);
                    this.mainCamera.transform.eulerAngles = new Vector3(15.147f, 63.724f, 0);
                    break;
                case 3: // 堆场后方
                    this.mainCamera.transform.position = new Vector3(1210f, 10f, -496f);
                    this.mainCamera.transform.eulerAngles = new Vector3(0f, 20f, 0);
                    break;
                default:
                    break;
            }
        }
        else
        {
            switch (indx)
            {
                case 0: // 进港闸口
                    this.FirstPerson.transform.position = new Vector3(-310f, 1.6f, -318f);
                    break;
                case 1: // 出港闸口
                    this.FirstPerson.transform.position = new Vector3(2120f, 1.6f, -495f);
                    break;
                case 2: // 码头前沿
                    this.FirstPerson.transform.position = new Vector3(1180f, 1.6f, -20f);
                    break;
                case 3: // 堆场后方
                    this.FirstPerson.transform.position = new Vector3(1210f, 1.6f, -520f);
                    break;
                default:
                    break;
            }
        }

        // ★ 快速定位后记录当前视角（第一人称或自由摄像的当前位置）
        LogCurrentView();
    }

    public void OnPositionClicked()
    {
        if (this.currentCameraMode == UserCameraMode.FreeCamera)
        {
            this.mainCamera.transform.position = this.DefaultMainPosition;
            this.mainCamera.transform.eulerAngles = this.DefaultMainEulerAngles;
            this.splitCamera1.transform.position = this.DefaultSubPosition;
            this.splitCamera1.transform.eulerAngles = this.DefaultSubEulerAngles;
        }
        else if (this.currentCameraMode == UserCameraMode.Wander)
        {
            this.wanderController.Reset();
        }
        else
        {
            this.FirstPerson.transform.position = this.DefaultPersonPosition;
        }
    }

    // ======== 新增：获取当前视角的名字，并统一调用 Logger ========

    private string GetCurrentViewId()
    {
        if (currentCameraMode == UserCameraMode.FirstPerson)
            return "FirstPerson";

        if (currentCameraMode == UserCameraMode.Wander)
            return "Wander";

        if (currenCamera != null)
            return "Free_" + splitMode + "_" + currenCamera.name;

        return "UnknownView";
    }

    private void LogCurrentView()
    {
        CameraStayLogger.Enter(GetCurrentViewId());
    }

    public enum UserCameraMode
    {
        Wander = 0,
        FirstPerson = 1,
        FreeCamera = 2
    }

    public enum UserSplitMode
    {
        OnlyMain = 0,
        MainPlus = 1,
        SplitFour = 2,
        TopDown = 3,
        LeftRight = 4,
    }
}
