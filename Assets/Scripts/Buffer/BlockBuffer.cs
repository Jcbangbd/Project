using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ZPMC.VR.Unity;

[AddComponentMenu("Buffer/BlocKBuffer")]
public class BlockBuffer : MapBuffer
{
    public override MapBufferType MapType
    {
        get { return MapBufferType.Block; }
    }

    public override DefinedType UserType
    {
        get { return DefinedType.BlockBuffer; }
    }

    protected override void Awake()
    {
        base.Awake();
    }

    protected override void Start()
    {
        base.Start();

        //if (!BlockManager.Instance.FindBlockBuffer(this.name))
        //{
        //    BlockManager.Instance.AddBlockBuffer(this);
        //    this.bindingMap = SettingsLoader.Instance().GetYardMapByStackName(name);
        //}

        //if (bindingMap == null)
        //{
        //    throw new UnityException("MapTemplate is NULL in " + this.name);
        //}
    }

    public override SlotBase CreateSlotFlag(int bay, int column, int tier)
    {
        return new SlotBlock(this, name, bay, column, tier);
    }

    public static void ParseYardCode(string yardCode,out string blockName,out int bay,out int column, out int tier)
    {
        blockName = yardCode.Substring(0, 3);
        bay = ParseHelper.TryAsInteger(yardCode.Substring(3, 3));
        column = ParseHelper.TryAsInteger(yardCode.Substring(6, 2));
        tier = ParseHelper.TryAsInteger(yardCode.Substring(8, 1));
    }

    public static string BuildYardCode(string blockName, int bay, int column, int tier)
    {
        return string.Format ("{0}{1:D3}{2:D2}{3}", blockName, bay, column, tier);
    }

    //public void SpawnContainer(int bay, int column, int tier, int shipOwnerIndex, float ctnLenght = ContainerUnit.FortyFeet, float ctnHeight = ContainerUnit.EightHalfFeet)
    //{
    //    //0. prepare
    //    CellHeld cell = this.FindOrCreateBay(bay).FindOrCreateCell(column);
    //    GameObject instance =ContainerSpawn.Instance.GetPrefab(shipOwnerIndex);
    //    Vector3 pos = transform.position + this.bindingMap.GetOffsetPostion(bay, column);
    //    pos.y = cell.GetCurrentTop();
    //    //1. spawn
    //    GameObject tempGO = SpawnController.Instance.SpawnGO(instance, pos,
    //        instance.transform.rotation * Quaternion.Euler(bindingMap.ContainerRotation));
    //    ContainerUnit container = tempGO.GetComponent<ContainerUnit>();
    //    tempGO.transform.localScale = new Vector3(1,ctnLenght / container.LengthMetre, ctnHeight / container.HeightMetre);
    //    tempGO.transform.SetParent(transform, true);
    //    //3. rectify container properties
    //    container.LengthMetre = ctnLenght;
    //    container.HeightMetre = ctnHeight;
    //    container.MetaBuffer = new EBufferBlock(this, name, bay, column, tier);
    //    //4.
    //    cell.AddContainer(tier, container);
    //}
    
    //public const float bayStat = 0.6f;
    //public const float colStat = 0.2f;
    //private void ProduceContainerRandom()
    //{
    //    int bay = 2;
    //    while (this.bindingMap.ContainsBay(bay))
    //    {
    //        if (Random.Range(0f, 1f) > bayStat)
    //            continue;

    //        if (Random.Range(1f, 3f) < 2)
    //        {
    //            for (int col = 1; col <= this.bindingMap.ColPosition.Count; col++)
    //            {
    //                float temp = Random.Range(0f, 1f);
    //                if (temp < colStat)
    //                {
    //                    int count = Random.Range(1, 4);
    //                    for (int tier = 1; tier <= count; tier++)
    //                    {
    //                        int owner = ContainerSpawn.Instance.GetRandomOwnerIndex();
    //                        ContainerUnit container = ContainerSpawn.Instance.SpawnContainer(owner);
    //                        this.AddContainer(bay, col, tier, container);
    //                        //SpawnContainer(bay, col, tier, owner);
    //                    }
    //                }
    //            }
    //        }
    //        else
    //        {
    //            for (int col = 1; col <= this.bindingMap.ColPosition.Count; col++)
    //            {
    //                float temp = Random.Range(0f, 1f);
    //                if (temp < colStat)
    //                {
    //                    int count = Random.Range(1, 4);
    //                    for (int tier = 1; tier <= count; tier++)
    //                    {
    //                        int owner = ContainerSpawn.Instance.GetRandomOwnerIndex();
    //                        ContainerUnit container = ContainerSpawn.Instance.SpawnContainer(owner, ContainerUnit.TwentyFeet);
    //                        this.AddContainer(bay - 1, col, tier, container);
    //                        //SpawnContainer(bay - 1, col, tier, owner, ContainerUnit.TwentyFeet);
    //                    }
    //                }
    //                temp = Random.Range(0f, 1f);
    //                if (temp < colStat)
    //                {
    //                    int count = Random.Range(1, 4);
    //                    for (int tier = 1; tier <= count; tier++)
    //                    {
    //                        int owner = ContainerSpawn.Instance.GetRandomOwnerIndex();
    //                        ContainerUnit container = ContainerSpawn.Instance.SpawnContainer(owner, ContainerUnit.TwentyFeet);
    //                        this.AddContainer(bay + 1, col, tier, container);
    //                        //SpawnContainer(bay + 1, col, tier, owner, ContainerUnit.TwentyFeet);
    //                    }
    //                }
    //            }
    //        }
    //        bay += 4;
    //    }

    //}
}
