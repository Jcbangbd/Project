using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class VesselManager :MonoBehaviour, IEnumerable
{
    private Dictionary<string, VesselBuffer> bufferMarshal;
    private Dictionary<string, Vessel> vesselMarshal;
    public AliasPrefab[] PrefabIns;

    #region Creation
    private static VesselManager localObject;
    private VesselManager()
    {
        this.bufferMarshal = new Dictionary<string, VesselBuffer>();
        this.vesselMarshal = new Dictionary<string, Vessel>();
        localObject = this;
    }
    public static VesselManager GlobalInstance()
    {
        return localObject;
    }
    #endregion

    void Awake()
    {
        if (this.PrefabIns.Length == 0)
            throw new UnityException("Can't find the vessel prefab in VesselManager");
    }

    public Vessel CreateVessel(string prefabName)
    {
        GameObject ins = null;
        foreach (var item in this.PrefabIns)
        {
            if (item.AliasName == prefabName)
            {
                ins = item.PrefabIns;
                break;
            }
        }
        if (ins == null)
            throw new UnityException("Can't find the prefab with a specified name creating Vessel");
        GameObject obj = SpawnController.Instance.SpawnGO(ins);
        Vessel vessel = obj.GetComponent<Vessel>();
        return vessel;
    }

    public GameObject GetPrefab(string prefabName)
    {
        foreach (var item in this.PrefabIns)
        {
            if (item.AliasName == prefabName)
            {
                return item.PrefabIns;
            }
        }
        return null;
    }

    public void AddVessel(Vessel vessel,VesselBuffer buffer)
    {
        this.vesselMarshal.Add(vessel.name, vessel);
        this.bufferMarshal.Add(vessel.name, buffer);
    }

    public void AddContainer(string vesselName, string vesselCode, Container container)
    {
        int bay, column, tier;
        VesselBuffer.ParseVesselCode(vesselCode, out bay, out  column, out  tier);
        this.bufferMarshal[vesselName].AddContainer(bay, column, tier, container);
    }

    public Vessel FindVessel(string vesselName)
    {
        if (this.vesselMarshal.ContainsKey(vesselName))
            return this.vesselMarshal[vesselName];
        return null;
    }

    public VesselBuffer FindVesselBuffer(string vesselName)
    {
        if (this.bufferMarshal.ContainsKey(vesselName))
            return this.bufferMarshal[vesselName];
        return null;
    }

    public Container FindContainer(string vesselName,int bay,int column,int tier)
    {
        VesselBuffer buffer = this.FindVesselBuffer(vesselName);
        return buffer.FindContainer(bay,column,tier);
    }

    public Container FindContainer(string vesselName, string vesselCode)
    {
        VesselBuffer buffer = this.FindVesselBuffer(vesselName);

        if (buffer != null)
        {
            int bay, column, tier;
            VesselBuffer.ParseVesselCode(vesselCode, out bay, out  column, out  tier);
            return buffer.FindContainer(bay, column, tier);
        }
        else
            return null;
    }

    public Container RemoveContainer(string vesselName, string vesselCode)
    {
        int bay, column, tier;
        VesselBuffer.ParseVesselCode(vesselCode, out bay, out  column, out  tier);
        if (this.bufferMarshal.ContainsKey(vesselName))
            return this.bufferMarshal[vesselName].RemoveContainer(bay, column, tier);
        else
            return null;
    }

    public void DestroyAllContainer()
    {
        foreach (var item in this.bufferMarshal.Values)
        {
            item.DestroyAllContainer();
        }
    }

    public IEnumerator GetEnumerator()
    {
        return this.bufferMarshal.Values.GetEnumerator();
    }
}
