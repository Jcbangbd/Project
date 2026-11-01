using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

[AddComponentMenu("Buffer/EngagedBuffer")]
public class EngagedBuffer : BufferBase 
{
    /// <summary>
    /// 在设备内的编号
    /// </summary>
    public int IndexInEquip;
    /// <summary>
    /// 局部坐标偏移
    /// </summary>
    public Vector3[] Offsets = { new Vector3(0, 0, 0), new Vector3(-3.25f, 0, 0), new Vector3(3.25f, 0, 0) };

    [HideInInspector]
    public EquipBase AttachedEquip;

    protected Dictionary<int, Container> engagedContainer;
    
    public override DefinedType UserType
    {
        get { return DefinedType.EngagedBuffer; }
    }

    protected override void Awake()
    {
        base.Awake();

        this.engagedContainer = new Dictionary<int, Container>(2);

        AttachedEquip= GetComponentInParent<EquipBase>();
        if (AttachedEquip == null)
        {
            Debug.Log("Can't find the related equipment.");
        }
        else
        {
            this.AttachedEquip.AddEngagedBuffer(this);
        }
    }

    private Vector3 GetLocalPosition(int bay)
    {
        if (bay >= 0 && bay < this.Offsets.Length)
            return this.Offsets[bay];
        throw new UnityException(string.Format("Can't get the engaged position.\n Equip={0} Index={1}\n Specified bay={2}",
            AttachedEquip, this.IndexInEquip, bay));
    }

    public Vector3 GetWorldPosition(int bay)
    {
        if (bay >= 0 && bay < this.Offsets.Length)
            return transform.position + transform.rotation * this.Offsets[bay];
        throw new UnityException(string.Format("Can't get the engaged position.\n Equip={0} Index={1}\n Specified bay={2}",
            AttachedEquip, this.IndexInEquip, bay));
    }

    public virtual void AddContainer(int bay, Container container)
    {
        this.engagedContainer.Add(bay, container);
        //container.transform.SetParent(transform, true);
        container.transform.parent = this.transform;
        container.transform.localPosition = this.GetLocalPosition(bay);//进一步精确调整位置 Quaternion.Inverse(transform.rotation) * 
    }

    public Container FindContainer(int bay)
    {
        if (this.engagedContainer.ContainsKey(bay))
            return this.engagedContainer[bay];
        else
            return null;
    }

    public Container RemoveContainer(int bay)
    {
        Container container = this.engagedContainer[bay];
        if (container != null)
        {
            this.engagedContainer.Remove(bay);
            container.SlotFlag = null;
        }
        return container;
    }

    public void OffsetContainer(Vector3 offset)
    {
        foreach (KeyValuePair<int, Container> pair in this.engagedContainer)
        {
            pair.Value.transform.position = transform.position + this.Offsets[pair.Key] + offset;
        }
    }

    public override void DestroyAllContainer()
    {
        for (int i = 0; i < this.Offsets.Length; i++)
        {
            if (this.engagedContainer.ContainsKey(i))
            {
                Container container = this.engagedContainer[i];
                this.engagedContainer.Remove(i);
                GameObject.DestroyObject(container.gameObject,0);
            }
        }
    }

    public override Container FindContainer(string containerName)
    {
        throw new NotImplementedException();
    }
}