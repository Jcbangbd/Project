using UnityEngine;
using System.Collections;

[AddComponentMenu("Buffer/LiftBuffer")]
public class LiftBuffer : EngagedBuffer
{
    public override DefinedType UserType
    {
        get
        {
            return DefinedType.LiftBuffer;
        }
    }

    public override void AddContainer(int bay, Container container)
    {
        base.AddContainer(bay, container);

        container.transform.Translate(0, -container.HeightMetre, 0, Space.World); //根据集装箱箱高下移
    }
}
