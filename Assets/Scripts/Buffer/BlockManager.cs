using UnityEngine;

using System.Collections;
using System.Collections.Generic;
using ZPMC.VR.Unity;

public class BlockManager : MonoBehaviour, IEnumerable
{  
    public GameObject BlockIns;  
    private Dictionary<string, BlockBuffer> blockMarshal;

    #region Creation
    private static BlockManager localObject;
    private BlockManager()
    {
        this.blockMarshal = new Dictionary<string, BlockBuffer>();
        localObject = this;
    }
    public static BlockManager Instance
    {
        get
        {
            return localObject;
        }
    }
    #endregion

    void Awake()
    {
        if (!this.BlockIns)
            throw new UnityException("Can't find the block prefab in BlockManager");
        if (!this.BlockIns.GetComponent<BlockBuffer>())
            throw new UnityException("Can't find the buffer script in block prefab");
    }

    public BlockBuffer FindOrAddBuffer(string blockName)
    {
        if (this.blockMarshal.ContainsKey(blockName))
            return this.blockMarshal[blockName];

        GameObject block = SpawnController.Instance.SpawnGO(BlockIns);
        BlockBuffer buffer = block.GetComponent<BlockBuffer>();
        buffer.name = blockName;
        this.blockMarshal.Add(blockName, buffer);
        return buffer;
    }

    public void AddBlockBuffer(BlockBuffer block)
    {
        this.blockMarshal.Add(block.name, block);
    }

    public void AddContainer(string yardCode,Container container)
    {
        string blockName;
        int bay, column, tier;
        BlockBuffer.ParseYardCode(yardCode, out blockName, out bay, out  column, out  tier);
        this.blockMarshal[blockName].AddContainer(bay, column, tier, container);
    }

    public BlockBuffer FindBlockBuffer(string blockName)
    {
        if (this.blockMarshal.ContainsKey(blockName))
            return this.blockMarshal[blockName];
        return null;
    }

    public Container FindContainer(string yardCode)
    {
        string blockName;
        int bay,column,tier;
        BlockBuffer.ParseYardCode(yardCode, out blockName, out bay, out  column, out  tier);
        if (this.blockMarshal.ContainsKey(blockName))
            return this.FindBlockBuffer(blockName).FindContainer(bay, column, tier);
        else
            return null;
    }

    public Container RemoveContainer(string yardCode)
    {
        string blockName;
        int bay, column, tier;
        BlockBuffer.ParseYardCode(yardCode, out blockName, out bay, out  column, out  tier);
        if (this.blockMarshal.ContainsKey(blockName))
            return this.blockMarshal[blockName].RemoveContainer(bay, column, tier);
        else
            return null;
    }

    public void DestroyAllContainer()
    {
        foreach (var item in this.blockMarshal.Values)
        {
            item.DestroyAllContainer();
        }
    }

    public IEnumerator GetEnumerator()
    {
        return this.blockMarshal.Values.GetEnumerator();
    }
}
