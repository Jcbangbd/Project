using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[AddComponentMenu("Equipment/AGV")]
public class AGV : EquipBase
{
    public override DefinedType EquipmentType
    {
        get { return DefinedType.AGV; }
    }

    public override void AddToManager()
    {
        EquipManager.Instance.AddEquip(this);
    }

    protected override void Start()
    {
        base.Start();
        if (EquipManager.Instance.FindEquipAGV(this.EquipID) == null)
            this.AddToManager();
    }

    public override void AddContainer(int bufferID, int bay, Container container)
    {
        base.AddContainer(bufferID, bay, container);

        this.NestedBuffer[bufferID].AddContainer(bay, container);
    }

}
