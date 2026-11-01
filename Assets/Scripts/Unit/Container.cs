using UnityEngine;
using System.Collections;

[AddComponentMenu("DefinedObject/Container")]
public class Container : PickableItem 
{
    public override DefinedType UserType
    {
        get { return DefinedType.Container; }
    }

    /// <summary>
    ///  船公司名称
    /// </summary>
    public string ShipOwnerName;
    
    public float LengthMetre = FortyFeet;
    public float HeightMetre = EightHalfFeet;

    public const float TwentyFeet = 6.096f;
    public const float FortyFeet = 12.192f;
    public const float EightHalfFeet = 2.591f;
    public const float EightFeet = 2.438f;
    public const float Metre2Foot = 3.2808399f;
    public const float Foot2Metre = 0.3048f;

    public SlotBase SlotFlag { get; set; }

    /// <summary>
    /// 集装箱顶部位置
    /// </summary>
    public float GetCurrentTop
    {
        get { return this.transform.position.y + this.HeightMetre; }
    }
    

    //public void OffsetDownSelfHeight()
    //{
    //    transform.Translate(0, -this.HeightMetre, 0, Space.World);
    //}


}
