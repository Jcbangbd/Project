using UnityEngine;
using System.Collections;

public abstract class DefinedItem : MonoBehaviour 
{
    public abstract DefinedType UserType { get; }

    protected virtual void Awake()
    {
    }

	// Use this for initialization
	protected virtual void Start ()
    {
	
	}
	
}

[System.Serializable]
public enum DefinedType
{
    Undefined = -1,
    DefinedElement = 0,
    ASC = EquipBase.ASC_Code,
    STS = EquipBase.STS_Code,
    AGV = EquipBase.AGV_Code,
    Mate = EquipBase.Mate_Code,
    Truck = EquipBase.Truck_Code,
    TruckStoper = EquipBase.TruckStoper,
    EngagedBuffer = 11,
    LiftBuffer = 12,
    BlockBuffer = 13,
    VesselBuffer = 14,

    Container = 5,
    Vessel = 6
}