using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class Background : MonoBehaviour
{
    [Header("进入时要临时隐藏的对象（离开时按原状态恢复）")]
    [SerializeField] private GameObject[] OnPlayObject = null;

    private bool[] flag;

    [SerializeField] private GameObject SpeedController = null;

    // ✅ 新增：是否在 OnEnable 时隐藏 TaskManager（学习模式：true；自定义模式：false）
    [Header("学习模式请勾选；自定义模式请取消勾选")]
    [SerializeField] private bool hideTaskWhenEnable = true;

    private void Awake()
    {
        if (OnPlayObject == null) OnPlayObject = new GameObject[0];
        this.flag = new bool[this.OnPlayObject.Length];
    }

    private void OnEnable()
    {
        StartCoroutine(this.OnUserEnable());

        // ✅ 只有勾选了“hideTaskWhenEnable”的窗口才会隐藏任务面板
        if (hideTaskWhenEnable && TaskManager.Instance)
        {
            TaskManager.Instance.gameObject.SetActive(false);
        }
    }

    // ✅ 可选兜底：如果当前窗口不需要隐藏任务面板，就进入时确保任务面板是打开的
    private void Start()
    {
        if (!hideTaskWhenEnable && TaskManager.Instance)
        {
            TaskManager.Instance.gameObject.SetActive(true);
        }
    }

    private IEnumerator OnUserEnable()
    {
        yield return null;

        // 记录并隐藏需要临时关闭的对象
        for (int i = 0; i < this.OnPlayObject.Length; i++)
        {
            var go = OnPlayObject[i];
            if (!go) continue;

            this.flag[i] = go.activeInHierarchy;
            go.SetActive(false);
        }

        if (this.SpeedController)
            this.SpeedController.SetActive(false);

        if (DubControl.UniqueInstance)
            DubControl.UniqueInstance.Stop();

        // musicScript.Pause(); // 你原来的注释保留
    }

    private void OnDisable()
    {
        // 恢复进入前的显示状态
        for (int i = 0; i < this.OnPlayObject.Length; i++)
        {
            var go = OnPlayObject[i];
            if (!go) continue;

            if (this.flag[i])
                go.SetActive(true);
        }

        if (!UMP_Manager.Instance.ShowSubtitle)
        {
            if (this.SpeedController)
                this.SpeedController.SetActive(true);
        }
        else
        {
            UMP_Manager.Instance.SetRunSpeed(1);
        }

        // 注意：这里不再对 TaskManager 做任何强制开/关，
        // 是否隐藏完全由 OnEnable 中 hideTaskWhenEnable 控制
    }
}
