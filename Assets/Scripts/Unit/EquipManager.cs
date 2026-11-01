using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ZPMC.VR.Unity;

[AddComponentMenu("Equipment/EquipManager")]
public class EquipManager : MonoBehaviour, IEnumerable
{
    private EquipGroup ASC_Group;
    private EquipGroup AGV_Group;
    private EquipGroup Mate_Group;
    private EquipGroup STS_Group;
    private EquipGroup Truck_Group;


    private Dictionary<int, EquipGroup> GroupMarshal;
    private Dictionary<int, EquipBase> EquipMarshal;

    #region Creation
    private static EquipManager instance;
    private EquipManager()
    {
        instance = this;
        this.Reset();
    }

    public static EquipManager Instance
    {
        get { return instance; }
    }

    private void Reset()
    {
        this.GroupMarshal = new Dictionary<int, EquipGroup>();
        this.EquipMarshal = new Dictionary<int, EquipBase>();

        this.STS_Group = new EquipGroup(EquipBase.STS_Code);
        this.GroupMarshal.Add(this.STS_Group.EquipCode, this.STS_Group);

        this.ASC_Group = new EquipGroup(EquipBase.ASC_Code);
        this.GroupMarshal.Add(this.ASC_Group.EquipCode, this.ASC_Group);

        this.AGV_Group = new EquipGroup(EquipBase.AGV_Code);
        this.GroupMarshal.Add(this.AGV_Group.EquipCode, this.AGV_Group);

        this.Mate_Group = new EquipGroup(EquipBase.Mate_Code);
        this.GroupMarshal.Add(this.Mate_Group.EquipCode, this.Mate_Group);

        this.Truck_Group = new EquipGroup(EquipBase.Truck_Code);
        this.GroupMarshal.Add(this.Truck_Group.EquipCode, this.Truck_Group);
    }
    #endregion

    #region Find
    public EquipBase FindEquip(int equipType, int equipID)
    {
        if (!this.GroupMarshal.ContainsKey(equipType))
            return null;
        
        return this.GroupMarshal[equipType][equipID];
    }

    public EquipBase FindEquip(string equipType,int equipID)
    {
        int eType = (int)System.Enum.Parse(typeof(DefinedType), equipType, true);
        return this.FindEquip(eType, equipID);
    }

    public EquipBase FindEquip(int equipID)
    {
        //foreach (var item in this.GroupMarshal.Values)
        //{
        //    foreach (EquipBase equip in item)
        //    {
        //        if (equip.EquipID == equipID)
        //            return equip;
        //    }
        //}
        //return null;
        if (this.EquipMarshal.ContainsKey(equipID))
            return this.EquipMarshal[equipID];
        return null;
    }

    public STS FindEquipSTS(int equipID)
    {
        if (this.STS_Group.Contains(equipID))
            return (STS) this.STS_Group[equipID];
        else
            return null;
    }

    public ASC FindEquipASC(int equipID)
    {
        if (this.ASC_Group.Contains(equipID))
            return (ASC)this.ASC_Group[equipID];
        else
            return null;
    }

    public AGV FindEquipAGV(int equipID)
    {
        if (this.AGV_Group.Contains(equipID))
            return (AGV)this.AGV_Group[equipID];
        else
            return null;
    }

    public Mate FindEquipMate(int equipID)
    {
        if (this.Mate_Group.Contains(equipID))
            return (Mate)this.Mate_Group[equipID];
        else
            return null;
    }

    public Truck FindEquipTruck(int equipID)
    {
        if (this.Truck_Group.Contains(equipID))
            return (Truck)this.Truck_Group[equipID];
        else
            return null;
    }
    public Truck SpawnTruck(int equipID)
    {
        GameObject go = EquipSpawner.Instance.SpawnGameObject("Truck");
        Truck truck = go.GetComponent<Truck>();
        truck.EquipID = equipID;
        truck.name = "Truck" + equipID.ToString();
        truck.AddToManager();
        return truck;
    }
    #endregion

    #region ADD
    public void AddEquip(STS equip)
    {
        this.STS_Group.AddEquip(equip);
        this.EquipMarshal.Add(equip.EquipID, equip);
    }

    public void AddEquip(ASC equip)
    {
        this.ASC_Group.AddEquip(equip);
        this.EquipMarshal.Add(equip.EquipID, equip);
    }

    public void AddEquip(AGV equip)
    {
        this.AGV_Group.AddEquip(equip);
        this.EquipMarshal.Add(equip.EquipID, equip);
    }

    public void AddEquip(Mate equip)
    {
        this.Mate_Group.AddEquip(equip);
        this.EquipMarshal.Add(equip.EquipID, equip);
    }

    public void AddEquip(Truck equip)
    {
        this.Truck_Group.AddEquip(equip);
        this.EquipMarshal.Add(equip.EquipID, equip);
    }

    public void AddEquip(TruckStoper equip)
    {
        this.EquipMarshal.Add(equip.EquipID, equip);
    }
    #endregion

    public static int GetEquipTypeByID(int equipIndex)
    {
        return equipIndex / 100 * 100;
    }

    public EquipGroup GetGroup(int equipType)
    {
        return GroupMarshal[equipType];
    }

    public void DestroyAllContainer()
    {
        foreach (var item in this.GroupMarshal.Values)
        {
            item.DestroyAllContainer();
        }
    }

    public IEnumerator GetEnumerator()
    {
        return this.EquipMarshal.Values.GetEnumerator();
    }

}

public class EquipGroup: IEnumerable
{
    public int EquipCode { get; set; }
    public Dictionary<int, EquipBase> EquipMarshal { get; set; }

    public EquipGroup(int groupID)
    {
        this.EquipCode = groupID;
        this.EquipMarshal = new Dictionary<int, EquipBase>();
    }

    public EquipBase this[int ID]
    {
        get
        {
            if (this.EquipMarshal.ContainsKey(ID))
                return this.EquipMarshal[ID];
            return null;
        }
    }

    public bool Contains(int equipID)
    {
        if (this.EquipMarshal.ContainsKey(equipID))
            return true;
        else
            return false;
    }

    public void AddEquip(EquipBase equip)
    {
        if (this.EquipMarshal.ContainsKey(equip.EquipID))
            throw new System.Exception(string.Format("Error in Equip Manager when Add Equipment.EquipType={0},EquipID={1}", this.EquipCode, equip.EquipID));
        this.EquipMarshal.Add(equip.EquipID, equip);
    }

    public void Clear()
    {
        this.EquipMarshal.Clear();
    }

    public IEnumerator GetEnumerator()
    {
        return this.EquipMarshal.Values.GetEnumerator();
    }

    public void DestroyAllContainer()
    {
        foreach (var item in this.EquipMarshal.Values)
        {
            item.DestroyAllContainer();
        }
    }
}