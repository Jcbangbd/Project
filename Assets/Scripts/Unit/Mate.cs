using UnityEngine;
using System.Collections;

[AddComponentMenu("Equipment/AGVMate")]
public class Mate : EquipBase
{
    public override DefinedType EquipmentType
    {
        get { return DefinedType.Mate; }
    }

    public override void AddToManager()
    {
        EquipManager.Instance.AddEquip(this);
    }

    protected override void Start()
    {
        base.Start();

        if (EquipManager.Instance.FindEquipMate(this.EquipID) == null)
            this.AddToManager();
    }

    public override void AddContainer(int bufferID, int bay, Container container)
    {
        base.AddContainer(bufferID, bay, container);

        this.NestedBuffer[bufferID].AddContainer(bay, container);
    }
    
}
