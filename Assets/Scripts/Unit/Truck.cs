using UnityEngine;
using System.Collections;

[AddComponentMenu("Equipment/Truck")]
public class Truck : EquipBase
{
    public override DefinedType EquipmentType
    {
        get { return DefinedType.Truck; }
    }

    public override void AddToManager()
    {
        EquipManager.Instance.AddEquip(this);
    }
    //protected override void Awake()
    //{
    //    base.Awake();
    //}

    protected override void Start()
    {
        base.Start();
        if (EquipManager.Instance.FindEquipTruck(this.EquipID) == null)
            this.AddToManager();
    }

    public override void AddContainer(int bufferID, int bay, Container container)
    {
        base.AddContainer(bufferID, bay, container);

        this.NestedBuffer[bufferID].AddContainer(bay, container);
    }

    public void SetVisible(bool isShow)
    {
        if (!isShow)
        {
            this.DestroyAllContainer();
        }
        if (gameObject.activeInHierarchy != isShow)
            gameObject.SetActive(isShow);
    }


}
