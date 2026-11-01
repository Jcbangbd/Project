using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AnimationBuffer : EngagedBuffer
{
    [SerializeField]
    private float AnimationStart = 0;
    [SerializeField]
    private float AnimationEnd = 1;
    [SerializeField]
    private float PhysicalStart = 0;
    [SerializeField]
    private float PhysicalEnd = 1;

    //private KinematicAnimation bindingAnimation;

    private float ratio;
    private Vector3 directionVector;
    public Vector3 DirectionVector
    {
        set { this.directionVector = value; }
    }

    protected override void Awake()
    {
        base.Awake();

        this.ratio = (this.PhysicalEnd - this.PhysicalStart) / (this.AnimationEnd - this.AnimationStart);
    }

    //public void Initialize(KinematicAnimation setAnimation)
    //{
    //    //this.bindingAnimation = setAnimation;

    //    if (setAnimation.LocalAxis == KinematicAxis.X)
    //    {
    //        this.directionVector = new Vector3(1, 0, 0);
    //    }
    //    else if (setAnimation.LocalAxis == KinematicAxis.Y)
    //    {
    //        this.directionVector = new Vector3(0, 1, 0);
    //    }
    //    else if (setAnimation.LocalAxis == KinematicAxis.Z)
    //    {
    //        this.directionVector = new Vector3(0, 0, 1);
    //    }
    //}

    public override void AddContainer(int bay, Container container)
    {
        this.engagedContainer.Add(bay, container);
        container.transform.SetParent(transform, true);
        //container.transform.localPosition = this.GetLocalPosition(bay);//进一步精确调整位置 Quaternion.Inverse(transform.rotation) * 
    }

    public void UserUpdate(float speed, float currentValue)
    {
        float rectifyValue = currentValue;
        if (currentValue < this.AnimationStart)
        {
            if (speed < 0)
                rectifyValue = this.AnimationStart;
            else
                return;
        }
        else if (currentValue > this.AnimationEnd)
        {
            if (speed > 0)
                rectifyValue = this.AnimationEnd;
            else
                return;
        }

        if (this.engagedContainer.Count > 0)
        {
            Vector3 offset = directionVector * (this.PhysicalStart + (rectifyValue - AnimationStart) * this.ratio);
            foreach (KeyValuePair<int, Container> pair in this.engagedContainer)
            {
                pair.Value.transform.localPosition = this.Offsets[pair.Key] + offset;
            }
        }
    }

}
