using UnityEngine;
using System.Collections;

[AddComponentMenu("Equipment/STS")]
public class STS : EquipBase 
{
    public override DefinedType EquipmentType
    {
        get { return DefinedType.STS; }
    }

    public override void AddToManager()
    {
        EquipManager.Instance.AddEquip(this);
    }

    protected override void Start()
    {
        base.Start();
        if (EquipManager.Instance.FindEquipSTS(this.EquipID) == null)
            this.AddToManager();
    }

    public override void AddContainer(int bufferID, int bay, Container container)
    {
        base.AddContainer(bufferID, bay, container);

        this.NestedBuffer[bufferID].AddContainer(bay, container);
    }
}
