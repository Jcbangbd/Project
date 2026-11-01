using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;

[AddComponentMenu("DefinedObject/PickableItem")]
public class PickableItem : DefinedItem, IPointerClickHandler, ISelectHandler, IDeselectHandler
{
    //protected static PointerEventData.InputButton currentButton = PointerEventData.InputButton.Left;
    protected static PointerEventData.InputButton pickedButton;
    protected static int pickedID;

    public override DefinedType UserType
    {
        get { return DefinedType.Undefined; }
    }

    protected override void Awake()
    {
#if DEBUG
        //1、一个GameObject不能有多个此元素
        PickableItem[] items = GetComponents<PickableItem>();
        if (items.Length > 1 && items[0] != this)
        {
            enabled = false;
            Debug.LogWarning("Pickable Object : Multiple PickableItem components on a single GameObject is not allowed! PickableItem has been disabled on a GameObject with name '" + gameObject.name + "'.");
        }
#endif
        if (!GetComponent<Collider>())//selectable && 
        {
            gameObject.AddComponent<MeshCollider>();
        }
        //this.NestedRenderer = GetComponent<MeshRenderer>();
        //if (!this.NestedRenderer)
        //    this.NestedRenderer = GetComponent<SkinnedMeshRenderer>();
    }

    public virtual void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            EventSystem.current.SetSelectedGameObject(gameObject, eventData);
        }
        else
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    public void OnUserPointerClick()
    {
        EventSystem.current.SetSelectedGameObject(gameObject, null);
    }

    public virtual void OnSelect(BaseEventData eventData)//
    {
        //int materialsNumber = NestedRenderer.sharedMaterials.Length;
        //for (int i = 0; i < materialsNumber; i++)
        //{
        //    //获取当前材质球颜色
        //    Color color = Color.green;// NestedRenderer.materials[i].color;
        //    //设置透明度  0-1;  0 = 完全透明
        //    //color.a = 0.01f;
        //    //置当前材质球颜色
        //    NestedRenderer.sharedMaterials[i].SetColor("_Color", color);
        //}

        //this.materialList = this.NestedRenderer.sharedMaterials;
        //MaterialSwitcher.Instance.Switch(this.NestedRenderer, 0);
        pickedID = gameObject.GetInstanceID();
        pickedButton = PointerEventData.InputButton.Left;
        CameraManager.Instance.FocusOn(transform, null, null);
    }

    public virtual void OnDeselect(BaseEventData eventData)//
    {
        //PointerEventData pointerEventData = eventData as PointerEventData;
        //if (pointerEventData != null && pointerEventData.button != PointerEventData.InputButton.Left)
        //    return;

        //int materialsNumber = NestedRenderer.sharedMaterials.Length;
        //for (int i = 0; i < materialsNumber; i++)
        //{
        //    NestedRenderer.sharedMaterials[i].SetColor("_Color", this.originColor[i]); //Color.white
        //}
        //MaterialController.CancelSetGrayMaterial(this.NestedRenderer);

        //this.NestedRenderer.sharedMaterials = this.materialList;
        //this.materialList = null;
        pickedID = int.MinValue;
        CameraManager.Instance.UnFocus();
    }

    public override string ToString()
    {
        return name;
    }
}
