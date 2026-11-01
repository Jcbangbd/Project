using UnityEngine;
using System.Collections;

public class FreeCameraSplit : FreeCamera
{
    [SerializeField]
    SwitchToggle AttchingSwitch = null;

    protected override void Awake()
    {
        base.Awake();

        this.AttchingSwitch.OnSwitchOnOff += this.OnSwithOnOff;
    }

    void OnEnable()
    {
        this.AttchingSwitch.gameObject.SetActive(true);
        //this.bindCamera.rect = new Rect(this.bindCamera.rect.x, this.bindCamera.rect.y, 0.5f, 0.5f);
    }

    //protected override void Update()
    //{
    //    base.Update();
    //}

    //public bool CheckContainMouse()
    //{
    //    //Vector3 mousePos = this.bindCamera.ScreenToViewportPoint(Input.mousePosition);
    //    bool isIn = this.bindCamera.pixelRect.Contains(Input.mousePosition);//,true
    //    return isIn;
    //}

    void OnDisable()
    {
        this.AttchingSwitch.gameObject.SetActive(false);
    }

    private void OnSwithOnOff(bool isOn)
    {
        //CameraManager.Instance.ChangeActiveCamera(this, isOn);
    }
}
