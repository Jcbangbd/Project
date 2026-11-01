using UnityEngine;
using System.Collections;

public abstract class SlotBase  
{
    public abstract SlotType BufferType { get; }
    public int Bay { get; set; }
    public BufferBase ParentBuffer { get; set; }//先用Component表示，以后应该要用统一基础类BaseObject->buffer来表示

    public SlotBase(BufferBase parentBuffer, int bay)
    {
        this.ParentBuffer = parentBuffer;
        this.Bay = bay;
    }

    public abstract override string ToString();
}

public class SlotBlock : SlotBase
{
    public string BlockName { get; set; }
    public int Column { get; set; }
    public int Tier { get; set; }

    public override SlotType BufferType
    {
        get { return SlotType.Block; }
    }

    public SlotBlock(BufferBase parentBuffer, string blockName, int bay, int column, int tier)
        : base(parentBuffer, bay)
    {
        this.BlockName = blockName;
        this.Column = column;
        this.Tier = tier;
    }

    public override string ToString()
    {
        return string.Format("{0}{1:D3}{2:D2}{3:D1}", BlockName, Bay, Column, Tier);
    }
}

public class SlotVessel : SlotBase
{
    public string VesselName { get; set; }
    public int Column { get; set; }
    public int Tier { get; set; }

    public override SlotType BufferType
    {
        get { return SlotType.Vessel; }
    }

    public SlotVessel(BufferBase parentBuffer, string vesselName, int bay, int column, int tier)
        : base(parentBuffer, bay)
    {
        this.VesselName = vesselName;
        this.Column = column;
        this.Tier = tier;
    }

    public override string ToString()
    {
        return string.Format("{0}|{1:D2}{2:D2}{3:D2}", VesselName, Bay, Column, Tier);
    }

    //public void ParseSlot(out 
}

public class SlotEngaged : SlotBase
{
    public override SlotType BufferType
    {
        get { return SlotType.Engaged; }
    }

    public SlotEngaged(BufferBase parentBuffer, int bay)
        : base(parentBuffer, bay)
    {
    }

    public static int GetBayCode(string inputStr)
    {
        int pos = inputStr.LastIndexOf('|');
        string tempStr = inputStr.Substring(pos + 1);

        if (!string.IsNullOrEmpty(tempStr))
            return ZPMC.VR.Unity.ParseHelper.TryAsInteger(tempStr);
        else
            throw new UnityException(string.Format("Parse {0}:{1} Error!", typeof(SlotEngaged).ToString(), inputStr));
    }

    public override string ToString()
    {
        EngagedBuffer attached = (EngagedBuffer)ParentBuffer;
        return string.Format("{0}|{1}|{2}", attached.AttachedEquip.ToString(),  attached.IndexInEquip, Bay); 
    }
}

public enum SlotType
{
    Unknown = 0,
    Block = 1,
    Vessel = 2,
    Engaged = 3
}