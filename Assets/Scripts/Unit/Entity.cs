using UnityEngine;
using System.Collections;

[AddComponentMenu("DefinedObject/Entity")]
public class Entity : PickableShow 
{
    [HideInInspector]
    public EquipBase AttachedEquip;
    [HideInInspector]
    public string AliasName;

    public override DefinedType UserType
    {
        get { return DefinedType.DefinedElement; }
    }

    //public KinematicInfo[] kinematics;

    //public bool IsKinematic
    //{
    //    get
    //    {
    //        if (this.kinematics.Length > 0)
    //            return true;
    //        else
    //            return false;
    //    }
    //}

    //protected override void Awake()
    //{
    //    base.Awake();
    //}

    public override string ToString()
    {
        if (string.IsNullOrEmpty(this.AliasName))
            return this.name;//AttachedEquip.name + "/" + 
        return this.AliasName;
    }


}

public enum DOF
{
    None = 0,
    TranslationX = 1,
    TranslationY = 2,
    TranslationZ = 3,
    RotationX = 4,
    RotationY = 5,
    RotationZ = 6,
    ScaleX = 7,
    ScaleY = 8,
    ScaleZ = 9,
    Animation = 10,
    Swerve = 11,
    LocalRotX = 24,
    LocalRotY = 25,
    LocalRotZ = 26
}