using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public abstract class EquipBase : MonoBehaviour 
{
    public const int ASC_Code = 200;
    public const int STS_Code = 100;
    public const int AGV_Code = 300;
    public const int Mate_Code = 600;
    public const int Truck_Code = 900;
    public const int TC_Code = 290;
    public const int TruckStoper = 800;

    public int EquipID;

    protected Dictionary<int, EngagedBuffer> NestedBuffer;//= new Dictionary<int, EngagedBuffer>()

    protected Dictionary<string, Entity> NestedElements;

    public abstract DefinedType EquipmentType { get; }

    public abstract void AddToManager();

    protected virtual void Awake()
    {
        this.NestedElements = new Dictionary<string, Entity>();

        Entity[] elements = GetComponentsInChildren<Entity>();
        foreach (Entity item in elements)
        {
            item.AttachedEquip = this;
            //if (item.IsKinematic)
                this.NestedElements.Add(item.tag, item);

            Transform t = item.transform;
            string s = t.tag;
            while (t != transform)
            {
                t = t.parent;
                s = t.tag + "/" + s;
            }
            s = name + ":/" + s;
            item.AliasName = s;
        }
    }

    protected virtual void Start()
    {
        TextMesh[] texts = GetComponentsInChildren<TextMesh>();
        foreach (var item in texts)
        {
            if (item.gameObject.tag == "AliasName")
            {
                item.text = this.EquipID.ToString();
            }
        }
    }

    //public void AddKinematic(KinematicBase kinematic)
    //{
    //    if (NestedKinematic == null)
    //        this.NestedKinematic = new Dictionary<int, KinematicBase>();
    //    this.NestedKinematic.Add(kinematic.IndexInEquip, kinematic);
    //}

    public void AddEngagedBuffer(EngagedBuffer buffer)
    {
        if (NestedBuffer == null)
            this.NestedBuffer = new Dictionary<int, EngagedBuffer>();
        this.NestedBuffer.Add(buffer.IndexInEquip, buffer);
    }

    public Entity FindElementWithTagName(string tagName)
    {
        foreach (Entity item in this.NestedElements.Values)
        {
            if (item.tag == tagName)
                return item;
        }
        return null;
    }

    public Transform FindChildWithTagName(string tagName)
    {
        Entity[] children = GetComponentsInChildren<Entity>();
        foreach (var item in children)
        {
            if (item.tag == tagName)
                return item.transform;
        }
        return null;
    }

    public EngagedBuffer FindEngagedBuffer(int bufferID)
    {
        if (this.NestedBuffer.ContainsKey(bufferID))
            return this.NestedBuffer[bufferID];
        return null;
    }

    public Container RemoveContainer(int bufferID, int bay)
    {
        EngagedBuffer buffer = this.NestedBuffer[bufferID];
        return buffer.RemoveContainer(bay);
    }

    public virtual void AddContainer(int bufferID, int bay, Container container)
    {
        container.SlotFlag = new SlotEngaged(this.NestedBuffer[bufferID], bay);
    }

    public void DestroyAllContainer()
    {
        foreach (var item in this.NestedBuffer.Values)
        {
            item.DestroyAllContainer();
        }
    }

    public override string ToString()
    {
        return string.Format("{0}|{1}", this.EquipmentType.ToString(), this.EquipID);
    }
}



