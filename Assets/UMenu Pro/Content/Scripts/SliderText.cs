using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SliderText : MonoBehaviour 
{
    [SerializeField]
    private Slider bindSlider = null;
    private Text bindText;

	// Use this for initialization
	void Awake () 
    {
        this.bindText = GetComponent<Text>();
        if (this.bindSlider)
            this.SetTextValue(this.bindSlider.value);
	}

    //void OnEnable()
    //{

    //}


    public void SetTextValue(float v)
    {
        this.bindText.text = string.Format("{0}", v);//:D0
    }
}
