using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SwitchToggle : MonoBehaviour 
{
    public GameObject isOnGameObject;
    public GameObject isOffGameObject;

    private Toggle toggle;

    public System.Action<bool> OnSwitchOnOff { get; set; }

    void Awake()
    {
        toggle = GetComponent<Toggle>();
    }

    void OnEnable()
    {
        isOnGameObject.SetActive(false);
        isOffGameObject.SetActive(true);
        this.toggle.isOn = false;
    }

    public void OnValueChange(bool isOn)
    {
        isOnGameObject.SetActive(isOn);
        isOffGameObject.SetActive(!isOn);
        if (this.OnSwitchOnOff != null)
            this.OnSwitchOnOff(isOn);
    }

}
