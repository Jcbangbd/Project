using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UserSettings : MonoBehaviour 
{
    [SerializeField]
    Slider volumeSlider = null;
    //[SerializeField]
    //Toggle fullScreenToggle = null;
    //[SerializeField]
    //Dropdown cameraDrop = null;
    //bool  m_FullScreen;

    void Awake()
    {
        AudioListener.volume = 0.5f;
    }

    void OnEnable()
    {
        if (this.volumeSlider)
            this.volumeSlider.value = this.volumeSlider.maxValue * AudioListener.volume;

        //if (this.fullScreenToggle)
        //    this.fullScreenToggle.isOn = Screen.fullScreen;

        //if (this.cameraDrop)
        //    cameraDrop.value = (int) CameraManager.Instance.currentCameraMode;
    }

    //// Use this for initialization
    //void Start ()
    //{
    //}

    public void OnVolumeSliderChanged(float v)
    {
        //UnityEngine.Debug.Log(v);
        if (this.volumeSlider)
            v /= this.volumeSlider.maxValue;
        AudioListener.volume = v;
    }

    public void OnFullScreenChanged(bool v)
    {
        Screen.fullScreen = v;
    }

    //public 

}
