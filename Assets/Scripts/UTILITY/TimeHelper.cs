using UnityEngine;
using System.Collections;

[AddComponentMenu("UserMiscellaneous/TimeHelper")]
public class TimeHelper : MonoBehaviour 
{
    private float rectifiedTime;
    public float RectifiedTime 
    {
        get { return this.rectifiedTime; } //this.offsetTime + Time.timeSinceLevelLoad;
    }

    private float timeScale = 1;
    public float TimeScale
    {
        set
        {
            if (this.timeScale != value)
            {
                this.lastRealTime = Time.timeSinceLevelLoad;
                this.lastDirtyTime = this.rectifiedTime;
            }
            this.timeScale = value;
        }
    }

    private bool isPaused = false;
    private float lastRealTime = 0;
    private float lastDirtyTime;
    public void DoPause()
    {
        this.isPaused = true;
        this.lastDirtyTime = this.RectifiedTime;
        this.lastRealTime = Time.timeSinceLevelLoad;
    }
    public void Unpause()
    {
        this.isPaused = false;
        this.lastRealTime = Time.timeSinceLevelLoad;
    }

    [System.NonSerialized]
    public static TimeHelper Instance;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (isPaused)
            return;
        this.rectifiedTime = this.lastDirtyTime + (Time.timeSinceLevelLoad - this.lastRealTime) * this.timeScale;
    }

    public static string ConvertToFormalStyle(float inputTime)
    {
        float hourTime = inputTime / 3600; // 以小时表示的时间。
        float minuteTime = (hourTime - Mathf.Floor(hourTime)) * 60;
        float secondTime = (minuteTime - Mathf.Floor(minuteTime)) * 60;
        return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)hourTime, (int)minuteTime, (int)secondTime);    
    }
}
