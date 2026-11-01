using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TaskManager : MonoBehaviour 
{
    [System.NonSerialized]
    public static TaskManager Instance;

    private SortedList<int, TaskInfo> marshal;

    [SerializeField]
    private GameObject ItemPrefab = null;
    [SerializeField]
    private RectTransform ContentRect = null;
    
    private int indxHead = 1;
    private int indxTail = 1;
    private TaskInfo infoHead;
    private TaskInfo infoTail;
    private int taskCount;
    public int TaskRevision { get; private set; }
    public float MotionEndTime { get; private set; }

    // 只读进度供实验流程使用，不改变任务更新和设备运动。
    public bool TryGetCompletion(out int total, out int completed, out float endTime)
    {
        total = taskCount; completed = 0; endTime = 0;
        if (marshal == null || total == 0) return false;
        foreach (var task in marshal.Values)
        {
            if (task == null) return false;
            endTime = Mathf.Max(endTime, task.EndTime);
            if (task.CurrentState < 0) completed++;
        }
        return true;
    }

    private RectTransform myRect;
    [SerializeField]
    private GameObject openButton = null;
    [SerializeField]
    private GameObject closeButton = null;
    private Coroutine wndRoutine;

    const int qtyInPage = 10;
    private int pageCount;
    private int currentPageIndx;
    [SerializeField]
    private Text currentPageText = null;
    [SerializeField]
    private Button PreButton = null;
    [SerializeField]
    private Button NextButton = null;
    [SerializeField]
    private Toggle isAuto = null;

    Dictionary<int, TaskItem> taskItemShow;

    void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
        
        this.taskItemShow = new Dictionary<int, TaskItem>(qtyInPage);
        for (int i = 0; i < qtyInPage; i++)
        {
            GameObject nGO = GameObject.Instantiate(ItemPrefab) as GameObject;
            nGO.transform.SetParent(ContentRect, false);
            TaskItem task = nGO.GetComponent<TaskItem>();

            this.taskItemShow.Add(i + 1, task);
        }
        
        this.myRect = GetComponent<RectTransform>();
    }

    void OnEnable()
    {
        this.myRect.position = new Vector3(Screen.width, this.myRect.position.y, this.myRect.position.z);
        this.openButton.SetActive(false);
        this.closeButton.SetActive(true);
    }

    public void ImportTask(ICollection<TaskInfo> list)
    {
        TaskRevision++;
        // 在导入当帧读取完整运动计划，避免流程观察器晚一帧才读到已出队的最后一段运动。
        MotionEndTime = 0;
        var records = ZPMC.VR.Unity.EventScripter.GetGlobalInstance().recordMarshal;
        if (!records.IsEmpty)
            foreach (var record in records.ConvertToArray())
                if (record.EventType != SimRecord.TitleVisible && !float.IsNaN(record.EndTime) && !float.IsInfinity(record.EndTime))
                    MotionEndTime = Mathf.Max(MotionEndTime, record.EndTime);
        this.taskCount = list.Count;
        this.marshal = new SortedList<int, TaskInfo>(list.Count);
        int indx = 1;
        foreach (var item in list)
        {
            this.marshal.Add(indx, item);
            indx++;
        }
        this.gameObject.SetActive(true);
        this.pageCount = (this.taskCount - 1) / qtyInPage + 1;

        this.indxHead = 1;
        this.indxTail = 1;
        if (this.taskCount > 0)
        {
            this.infoHead = this.marshal[1];
            this.infoTail = this.marshal[1];
            this.OnSwitchPage(1);
        }
    }

    /// <summary>
    /// 供三维“查看算例”面板读取原任务系统的实时状态。
    /// 只暴露状态，不改变任务推进和设备运动逻辑。
    /// </summary>
    public bool TryGetTaskState(int oneBasedIndex, out int state)
    {
        state = 1;
        if (marshal == null ||
            oneBasedIndex < 1 ||
            oneBasedIndex > taskCount ||
            !marshal.ContainsKey(oneBasedIndex))
        {
            return false;
        }

        TaskInfo task = marshal[oneBasedIndex];
        if (task == null)
            return false;

        state = task.CurrentState;
        return true;
    }

    private void OnSwitchPage(int indxPage)
    {
        if (indxPage > this.pageCount || indxPage < 1)
            return;
        if (this.currentPageIndx == indxPage)
            return;
        if (this.currentPageText)
            this.currentPageText.text = string.Format("{0}/{1}", indxPage, this.pageCount);

        this.currentPageIndx = indxPage;
        int fIndex = 0, tIndex = 0, count = 0;

        fIndex = (indxPage - 1) * qtyInPage + 1;
        tIndex = Mathf.Min(indxPage * qtyInPage, this.taskCount);

        while (fIndex <= tIndex)
        {
            count++;
            TaskInfo item = this.marshal[fIndex];
            this.taskItemShow[count].BindInfo = item;
            this.taskItemShow[count].SetAttribute(fIndex, item.FromCode, item.ToCode);
            this.taskItemShow[count].SetAttribute(item.CurrentState);
            this.taskItemShow[count].gameObject.SetActive(true);

            fIndex++;
        }

        while (count < qtyInPage)
        {
            count++;
            this.taskItemShow[count].gameObject.SetActive(false);
        }

        if (this.PreButton)
            this.PreButton.interactable = this.currentPageIndx == 1 ? false : true;
        if (this.NextButton)
            this.NextButton.interactable = this.currentPageIndx == this.pageCount ? false : true;
    }

    public void OnForwardPage()
    {
        if (this.currentPageIndx < this.pageCount)
            this.OnSwitchPage(this.currentPageIndx + 1);
    }

    public void OnBackwardPage()
    {
        if (this.currentPageIndx > 1)
            this.OnSwitchPage(this.currentPageIndx - 1);
    }

    public void OnAutoPage(bool isOn)
    {
        if (isOn)
        {
            int temp = (indxHead - 2) / qtyInPage + 1;
            if (temp != currentPageIndx)
                this.OnSwitchPage(temp);
        }
    }

	// Update is called once per frame
	void Update () 
    {
        float currentTime=TimeHelper.Instance.RectifiedTime;
        bool flag = false;
        if (this.taskCount >= indxHead)
        {
            while (infoHead.StartTime < currentTime)
            {
                flag = true;
                infoHead.CurrentState = 0;//.BindTask.SetAttribute(0);
                if (infoHead.BindTask)
                    infoHead.BindTask.SetAttribute(0);
                this.indxHead++;
                if (this.taskCount >= indxHead)
                    this.infoHead = this.marshal[this.indxHead];
                else
                    break;
            }
        }

        if (this.taskCount >= indxTail)
        {
            while (infoTail.EndTime < currentTime)
            {
                infoTail.CurrentState = -1;//.BindTask.SetAttribute(-1);
                if (infoTail.BindTask)
                    infoTail.BindTask.SetAttribute(-1);
                this.indxTail++;
                if (this.taskCount >= indxTail)
                    this.infoTail = this.marshal[this.indxTail];
                else
                    break;
            }
        }

        if (isAuto.isOn && flag)
        {
            int temp= (indxHead - 2) / qtyInPage + 1;
            if (temp != currentPageIndx)
                this.OnSwitchPage(temp);
        }

        //if (flag && this.scroll.value > 1- this.itemHeight * this.indxHead/this.RelativeContent.rect.height)
        //{
        //    this.scroll.value = 1 - this.itemHeight * this.indxTail / RelativeContent.rect.height; //.rect.Set(RelativeContent.rect.x, this.itemHeight * this.indxHead, RelativeContent.rect.width, RelativeContent.rect.height);
        //}
	}

    
    bool mopen = true;
    public void WndowAnimation(bool forward)
    {
        if (this.wndRoutine != null)
            this.StopCoroutine(this.wndRoutine);

        if (forward)
        {
            if (forward == mopen)
                return;

            this.wndRoutine = StartCoroutine(this.ShowDialog());
        }
        else
        {
            this.wndRoutine = StartCoroutine(this.HideDialog());
        }
        mopen = forward;
    }
        
    IEnumerator ShowDialog()
    {
        float x ;
        float countTime=0;
        Vector3 iniPos = new Vector3(Screen.width + this.myRect.rect.width, this.myRect.position.y, this.myRect.position.z);//this.myRect.position;
        while (countTime < 1.5f)
        {
            yield return new WaitForEndOfFrame();
            countTime += Time.unscaledDeltaTime;
            x = this.myRect.rect.width * countTime / 1.5f;
            this.myRect.position = new Vector3(iniPos.x - x, iniPos.y, iniPos.z);
        }
        this.myRect.position = new Vector3(Screen.width, iniPos.y, iniPos.z);
        this.wndRoutine = null;
    }

    IEnumerator HideDialog()
    {
        float x ;
        float countTime = 0;
        Vector3 iniPos = new Vector3(Screen.width, this.myRect.position.y, this.myRect.position.z);// this.myRect.position;
        while (countTime < 1.5f)
        {
            yield return new WaitForEndOfFrame();
            countTime += Time.unscaledDeltaTime;

            x = this.myRect.rect.width * countTime / 1.5f;
            this.myRect.position = new Vector3(iniPos.x + x, iniPos.y, iniPos.z);
        }
        this.myRect.position = new Vector3(iniPos.x + this.myRect.rect.width, iniPos.y, iniPos.z);
        this.wndRoutine = null;
    }

    void OnDisable()
    {
        if (this.wndRoutine != null)
        {
            this.StopCoroutine(this.wndRoutine);
            this.wndRoutine = null;
        }
    }
}
