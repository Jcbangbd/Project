using UnityEngine;
using System.Collections;
using ZPMC.VR.Unity;

public class Vessel : PickableItem
{
    public override DefinedType UserType
    {
        get
        {
            return DefinedType.Vessel;
        }
    }

    public VesselBuffer nestedBuffer { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        VesselBuffer[] buffers = GetComponentsInChildren<VesselBuffer>();
        if (buffers.Length == 0)
            throw new UnityException("The VesselBuffer quantity is equal to 0 in Vessel " + this.name);
        if (buffers.Length > 1)
            throw new UnityException("The VesselBuffer quantity is more than 1 in Vessel " + this.name);
        this.nestedBuffer = buffers[0];
        this.nestedBuffer.name = this.name;
        this.nestedBuffer.AttachedVessel = this;
    }

    //protected override void Start()
    //{
    //    base.Start();

    //    //if (!VesselManager.GlobalInstance().FindVessel(name))
    //    //{
    //    //    VesselManager.GlobalInstance().AddVessel(this, this.nestedBuffer);
    //    //}
    //}
}
