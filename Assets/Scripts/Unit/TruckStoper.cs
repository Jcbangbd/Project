using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


public class TruckStoper : EquipBase
{
    public static Dictionary<int, TruckStoper> EquipList = new Dictionary<int, TruckStoper>();

    public override DefinedType EquipmentType
    {
        get { return DefinedType.TruckStoper; }
    }

    public override void AddToManager()
    {
        if (!EquipList.ContainsKey(EquipID))
            EquipList.Add(EquipID, this);
        if (EquipManager.Instance.FindEquip(this.EquipID) == null)
            EquipManager.Instance.AddEquip(this);
    }

    protected override void Start()
    {
        base.Start();

        this.AddToManager();
    }



}

