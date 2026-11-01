using UnityEngine;
using UnityEngine.UI;


public class TaskItem : MonoBehaviour
{
    private Text stateText;
    private Image backLayout;
    private Text[] attGO;
    private int taskID;
    public int stateCode;

    private TaskInfo bindInfo;
    public TaskInfo BindInfo
    {
        set
        {
            if (this.bindInfo != null)
                this.bindInfo.BindTask = null;
            this.bindInfo = value;
            this.bindInfo.BindTask = this;
        }
    }

    static Color OverColor = Color.gray;
    static Color FutureColor = new Color(0.96f, 0.87f, 0.702f);//Wheat
    static Color WorkingColor = new Color(0.706f, 0.933f, 0.706f);//DarkSeaGreen2

    void Awake()
    {
        backLayout = GetComponent<Image>();
        attGO = GetComponentsInChildren<Text>();
        this.stateText = attGO[3];
    }

    public void SetAttribute(int id,string fromCode,string toCode)
    {
        this.taskID = id;
        attGO[0].text = id.ToString();
        attGO[1].text = fromCode;
        attGO[2].text = toCode;
    }

    public void SetAttribute(int flag)
    {
        this.stateCode = flag;
        if (flag < 0)
        {
            this.stateText.text = "已完成";
            this.backLayout.color = OverColor;// this.backLayout.CrossFadeColor(OverColor, 1, true, false);
        }
        else if (flag > 0)
        {
            this.stateText.text = "未作业";
            this.backLayout.color = FutureColor;  //.CrossFadeColor(FutureColor, 1, true, false);
        }
        else
        {
            this.stateText.text = "作业中";
            this.backLayout.color = WorkingColor;  //.CrossFadeColor(WorkingColor, 1, true, false);
        }
    }

    public void OnClicked()
    {
        //if (this.stateCode == 0)
        //{
            //TaskManager.Instance.OnClickedTask(this.taskID);
        //}
        if ( this.bindInfo.BindContainer != null && this.bindInfo.BindContainer.isActiveAndEnabled)
        {
            this.bindInfo.BindContainer.OnUserPointerClick();
        }
    }
}
