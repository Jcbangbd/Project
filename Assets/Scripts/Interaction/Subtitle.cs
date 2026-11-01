using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class Subtitle : MonoBehaviour
{
    [Range(0, 10)]
    public float fadeTime = 4f;
    private float actualFadeTime = 0;
    private bool IsOn = false;
    private float fadeAlphaState = 0;
    private float beginTime;
    private float finishTime;
    private Text textScript;
    private Vector2 textPos;
    private RectTransform textRect = null;
    private bool isPause;

    public bool IsPause 
    {
        set
        {
            this.isPause = value;
            if (value && this.IsOn)
                this.textScript.CrossFadeAlpha(0.1f, 0, false);
        }
    }

    // Singleton
    private static Subtitle _instance;
    public static Subtitle Instance
    {
        get { return _instance; }
    }

    private void Awake()
    {
        _instance = this;
        //this.image = GetComponent<Image>();
        this.textScript = GetComponentInChildren<Text>();
        if (!this.textScript)
            enabled = false;
        this.textRect = this.textScript.GetComponent<RectTransform>();
        this.textScript.text = string.Empty;
        this.textPos = this.textRect.anchoredPosition;
    }

    private void Start()
    {
        //this.SetSubtilte("asdkjflasjdflasdjlkfaldjs", 5, 100);
    }

    // Update is called once per frame
    void Update()
    {
        if (!this.IsOn)
        {
            //this.image.CrossFadeAlpha(0, 0, true);
            return;
        }
        if (this.isPause)//|| currentTime <= this.beginTime
            return;
        float currentTime = TimeHelper.Instance.RectifiedTime;
        if (currentTime < this.beginTime + actualFadeTime)
        {
            //if (this.fadeState == 0)
            //{
            //    this.textScript.CrossFadeAlpha(1, this.beginTime + this.actualFadeTime - currentTime, false);                
            //    this.fadeState = 1;
            //}
            this.fadeAlphaState = (currentTime - this.beginTime) / this.actualFadeTime;
            this.textScript.CrossFadeAlpha(this.fadeAlphaState, 0, false);
            this.textPos.y = Mathf.Lerp(0, this.textRect.rect.height, (currentTime - this.beginTime) / this.actualFadeTime);// + this.speed * Time.deltaTime;
            this.textRect.anchoredPosition = this.textPos;
        }
        else if (currentTime < this.finishTime - actualFadeTime)
        {
            if (this.fadeAlphaState <= 1)
                this.textScript.CrossFadeAlpha(1, 0, false);
            this.textPos.y = this.textRect.rect.height;
            this.textRect.anchoredPosition = this.textPos;
        }
        else if (currentTime < this.finishTime)
        {
            //if (this.fadeState <= 2)
            //{
            //    this.textScript.CrossFadeAlpha(0, this.actualFadeTime, false);
            //    this.fadeState = 3;
            //}
            this.fadeAlphaState=(this.finishTime - currentTime) / this.actualFadeTime;
            this.textScript.CrossFadeAlpha(this.fadeAlphaState, 0, false);
            this.textPos.y = Mathf.Lerp(this.textRect.rect.height * 2, this.textRect.rect.height, (this.finishTime - currentTime) / this.actualFadeTime);//+this.speed * Time.deltaTime
            this.textRect.anchoredPosition = this.textPos;
        }
        else
            this.Disappeared();
    }

    public void SetSubtilte(string text, float beginTime, float finishTime)
    {
        //0、prepare
        float currentTime = TimeHelper.Instance.RectifiedTime;
        if (this.IsOn)
            this.Disappeared();
        if (DubControl.UniqueInstance)
            DubControl.UniqueInstance.AdvancePlay();

        this.textScript.text = string.Empty;
        if (finishTime <= currentTime || finishTime <= beginTime)
            return;
        //1、赋值
        this.IsOn = true;
        //this.image.color = new Color(1, 1, 0.66f, 0.75f);
        this.fadeAlphaState = 0;
        this.beginTime = beginTime;
        this.finishTime = finishTime;
        this.textScript.text = text;
        //2、归位
        this.textScript.CrossFadeAlpha(0, 0, false);
        this.textPos.y = 0;// this.speed * (TimeHelper.Instance.RectifiedTime - beginTime);
        this.textRect.anchoredPosition = this.textPos;

        //3、
        this.actualFadeTime = Mathf.Min(this.fadeTime, (finishTime - beginTime) / 2);
    }

    public void Disappeared()
    {
        if (this.IsOn)
        {
            this.textScript.text = string.Empty;
            this.IsOn = false;
        }
    }

}

