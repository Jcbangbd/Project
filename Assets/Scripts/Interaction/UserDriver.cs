using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using ZPMC.VR.Unity;

public abstract class UserDriver : MonoBehaviour
{
    public static UserDriver Instance;
    public bool IsPause {protected get; set; }

    public SortedList<string, Container> CollectingContainer = new SortedList<string, Container>();

    protected virtual void Awake()
    {
        if (Instance != null)
            //return;
            throw new UnityException("Tried to generate more than one instance of singleton UserDriver.");
        this.BindInstance();
    }

    protected abstract void BindInstance();

    //public virtual void DoPause() { }

    //public virtual void UnPause() { }

    public abstract void ReStart();

    public abstract void TryStopKinematic(float currentTime);    
}
