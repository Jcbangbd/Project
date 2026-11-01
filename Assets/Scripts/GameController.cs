using UnityEngine;
using System.Collections;
using ZPMC.VR.Unity.Scripts;
using ZPMC.VR.Unity;
//using UnityEditor;

[AddComponentMenu("UserMiscellaneous/GameController")]
public class GameController : MonoBehaviour
{
    [System.NonSerialized]
    public static GameController Instance;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        //UMP_LevelInfo.LoadOnBegin();
        //if (UMP_Manager.Instance.LastLevel)
        //{
        //    //if (!GameController.Instance.gameObject.GetComponent<SimDriver>())
        //    //    GameController.Instance.gameObject.AddComponent<SimDriver>();
        //    UMP_Manager.Instance.LastLevel.Initialize();
        //}
    }

    void Update()
    {
        if (Input.GetKey(KeyCode.Escape))
            if (Screen.fullScreen)
                Screen.fullScreen = false;
    }

    void OnDisable()
    {
        //SettingsLoader.Instance().Clear();
    }

    //void OnGUI()
    //{
        //Rect pos = new Rect(Screen.width / 2 - 200, 35, 400, 20);
        //GUI.Label(new Rect(pos.x - 80, pos.y - 7, 50, 20), "Speed:");
        //GUI.Label(new Rect(pos.x - 30, pos.y - 7, 30, 20), string.Format("{0:F1}", this.gameSpeed));
        //float temp = GUI.HorizontalSlider(pos, this.gameSpeed, 0.1f, 10);
        //if (temp != this.gameSpeed)
        //{
        //    this.gameSpeed = temp;
        //    Time.timeScale = gameSpeed;
        //}
        //if (GUI.Button(new Rect(Screen.width / 2, 10, 50, 20), "Vi"))
        //{
        //    Truck car = EquipManager.GetGlobalInstance().FindEquipTruck(301);

        //    car.SetVisible(!car.gameObject.activeInHierarchy);
        //}
        //GUI.Label(new Rect(10, 100, 200, 200),  SqliteHelper.PortDataFilePath);
    //}

    //[MenuItem("User/Create")]
    //public static void CreateDataBase()
    //{
    //    DataAccess.GetGlobalInstance().CreateDataBase();
    //}

    //[MenuItem("User/Create")]
    //public static void Test()
    //{
    //    Mate mate = EquipManager.Instance.FindEquipMate(662);
    //    if (mate)
    //    {
    //        KinematicAnimation k = (KinematicAnimation)mate.FindKinematic(0);
    //        k.SetNormalizedTime(Mathf.Clamp01(k.NormalizedTime + 0.1f));
    //    }
    //}
}
