using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ZPMC.VR.Unity;

[AddComponentMenu("Buffer/VesselBuffer")]
public class VesselBuffer : MapBuffer
{
    public Vessel AttachedVessel { get; set; }

    public override MapBufferType MapType
    {
        get { return MapBufferType.Vessel; }
    }

    public override DefinedType UserType
    {
        get { return DefinedType.VesselBuffer; }
    }

    protected override void Start()
    {
        base.Start();

        //if (this.bindingMap == null)
        //{
        //    this.bindingMap = SettingsLoader.Instance().GetMapByTemplateName(this.AttachedVessel.name);
        //}
        //if (this.bindingMap == null)
        //    Debug.Log(string.Format("Vessel={0} can't find relatived map\n. ", this.name));
    }
    
    public override SlotBase CreateSlotFlag(int bay, int column, int tier)
    {
        return new SlotVessel(this, this.AttachedVessel.name, bay, column, tier);
    }

    public static void ParseVesselCode(string vesselCode, out int bay, out int column, out int tier)
    {
        bay = ParseHelper.TryAsInteger(vesselCode.Substring(0, 2));
        column = ParseHelper.TryAsInteger(vesselCode.Substring(2, 2));
        tier = ParseHelper.TryAsInteger(vesselCode.Substring(4, 2));
    }

    
}
