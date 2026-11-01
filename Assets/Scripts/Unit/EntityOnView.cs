using System;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("DefinedObject/EntityOnView")]
public class EntityOnView : Entity 
{
    [SerializeField]
    private Vector3 absDirection = Vector3.zero;
    [SerializeField]
    private Vector3 offset = Vector3.zero;

    public override void OnSelect(UnityEngine.EventSystems.BaseEventData eventData)
    {
        //base.OnSelect(eventData);

        CameraManager.Instance.FocusOn(transform, this.offset, this.absDirection);
    }

}
