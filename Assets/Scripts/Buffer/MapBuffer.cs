using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ZPMC.VR.Unity;

public abstract class MapBuffer : BufferBase, IEnumerable
{
    protected MapTable bindingMap;
    public MapTable BindingMap
    {
        set
        {
            this.bindingMap = value;
        }
    }
    protected SortedList<int, BayHeld> bayMarshal = new SortedList<int, BayHeld>();

    public abstract MapBufferType MapType { get; }
    public abstract SlotBase CreateSlotFlag(int bay, int column, int tier);

    protected override void Start()
    {
        base.Start();
    }
  
    protected BayHeld FindOrCreateBay(int bayNO)
    {
        if (this.bayMarshal.ContainsKey(bayNO))
        {
            return this.bayMarshal[bayNO];
        }
        else
        {
            BayHeld newBay = new BayHeld(this, bayNO);
            this.bayMarshal.Add(bayNO, newBay);
            return newBay;
        }
    }

    public void InitialContainer(int bayNO, int columnNO, int tierNO, Container container)
    {
        container.transform.Rotate(transform.rotation.eulerAngles,Space.World);
        this.AddContainer(bayNO, columnNO, tierNO, container);
    }

    /// <summary>
    /// 在指定的贝、列、层添加集装箱
    /// </summary>
    /// <param name="bayNO">贝</param>
    /// <param name="columnNO">列</param>
    /// <param name="tierNO">层</param>
    /// <param name="container">集装箱</param>
    public void AddContainer(int bayNO, int columnNO, int tierNO, Container container)
    {
        BayHeld bayHeld = this.FindOrCreateBay(bayNO);
        CellHeld cell = bayHeld.FindOrCreateCell(columnNO);
        Vector3 pos = this.bindingMap.GetMapPostion(bayNO, columnNO); //在Map中位置
        pos = this.transform.position + this.transform.rotation * pos;//在世界坐标中的位置
        pos.y += cell.GetTierPosition(tierNO);
        container.transform.position = pos;
        //container.transform.SetParent(transform, true);
        container.transform.parent = this.transform;
#if DEBUG
        if (container.SlotFlag != null)
        {
            Debug.Log("Container's buffer flag is not empty when add to Block");
        }
#endif
        container.SlotFlag = this.CreateSlotFlag(bayNO, columnNO, tierNO); //new EBufferBlock(this, name, bay, column, tier);
        cell.AddContainer(tierNO, container);
    }

    /// <summary>
    /// 在指定的贝、列、层移除集装箱
    /// </summary>
    /// <param name="bayNO">贝</param>
    /// <param name="columnNO">列</param>
    /// <param name="tierNO">层</param>
    /// <returns>集装箱</returns>
    public Container RemoveContainer(int bayNO, int columnNO, int tierNO)
    {
        try
        {
            BayHeld bay = this.bayMarshal[bayNO];
            Container unit = bay.RemoveContainer(columnNO, tierNO);
            if (bay.Count < 1)
            {
                this.bayMarshal.Remove(bayNO);
            }
            unit.SlotFlag = null;
            return unit;
        }
        catch (System.Exception ex)
        {
            throw new System.Exception(string.Format("Can't remove a container in {0}.\n Bay={1},Column={2},Tier={3}.\n {4}", name, bayNO, columnNO, tierNO, ex.Message));
        }
    }

    public override void DestroyAllContainer()
    {
        foreach (var item in this.bayMarshal.Values)
        {
            item.DestroyAllContainer();
        }
    }

    /// <summary>
    /// 在指定的贝、列、层查找集装箱
    /// </summary>
    /// <param name="bay">贝</param>
    /// <param name="column">列</param>
    /// <param name="tier">层</param>
    /// <returns>集装箱</returns>
    public Container FindContainer(int bay, int column, int tier)
    {
        if (this.bayMarshal.ContainsKey(bay))
        {
            BayHeld findBay = this.bayMarshal[bay];
            CellHeld findCell = findBay.FindCell(column);
            if (findCell != null)
                return findCell.FindContainer(tier);
        }
        return null;
    }

    public float GetWorldBayGauge(int bay)
    {
        Vector3 pos = transform.rotation * new Vector3(this.bindingMap.GetWorldOffsetBay(bay), 0, 0);
        return pos.x + transform.position.x; //还有问题，只表示了缓冲区走向为X方向的情况
    }

    public float GetWorldColumnGauge(int column)
    {
        Vector3 pos = transform.rotation * new Vector3(0, 0, this.bindingMap.GetWorldOffsetColumn(column));
        return pos.z + transform.position.z; //还有问题，只表示了缓冲区走向为X方向的情况
    }

    public Vector3 GetWorldPosition(int bayNo, int columnNo)
    {
        return transform.position + this.transform.rotation * this.bindingMap.GetMapPostion(bayNo, columnNo);//this.OffsetRotation
    }

    public override Container FindContainer(string containerName)
    {
        throw new System.NotImplementedException();
    }

    public IEnumerator GetEnumerator()
    {
        return this.bayMarshal.Values.GetEnumerator();
    }
}

public enum MapBufferType
{
    Unknown,
    Block,
    Vessel,
}