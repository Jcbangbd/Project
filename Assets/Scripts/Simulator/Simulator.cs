using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ZPMC.VR.Unity;

public class Simulator : MonoBehaviour
{
    private Dictionary<int, Dictionary<int, SimBase>> simMarshal = new Dictionary<int, Dictionary<int, SimBase>>();//

    //[System.NonSerialized]
    public static Simulator Instance;
    public bool isPause { private get; set; }

    void Awake()
    {
        // this function will be called whenever an instance of the SpawnController class is made
        // first, we check that an instance does not already exist (this is a singleton, afterall!)
        if (Instance != null)
        {
            // drop out if instance exists, to avoid generating duplicates
            Debug.LogWarning("Tried to generate more than one instance of singleton Simulator.");
            return;
        }
        // as no instance already exists, we can safely set instance to this one
        Instance = this;
    }

    public void AddController(int equipID, SimBase metaSim)
    {
        try
        {
            if (this.simMarshal.ContainsKey(equipID))
            {
                this.simMarshal[equipID].Add(metaSim.IndexInEquip, metaSim);
            }
            else
            {
                Dictionary<int, SimBase> m = new Dictionary<int, SimBase>();
                this.simMarshal.Add(equipID, m);
                m.Add(metaSim.IndexInEquip, metaSim);
            }
        }
        catch (System.Exception ex)
        {
            throw new System.Exception("Simulator initialization error!" + ex.Message);
        }
    }

    public SimBase FindSimulator(int equipID, int simID)
    {
        if (this.simMarshal.ContainsKey(equipID))
        {
            if (this.simMarshal[equipID].ContainsKey(simID))
                return this.simMarshal[equipID][simID];
            return null;
        }
        return null;
    }

    // Update is called once per frame
    void Update()
    {
        if (isPause)
            return;
        float currentTime = TimeHelper.Instance.RectifiedTime;
        foreach (var item in this.simMarshal.Values)
        {
            foreach (var s in item.Values)
            {
                s.UserUpdate(ref currentTime);
            }
        }
    }

    public void TryStopKinematic(float currentTime)
    {
        foreach (var item in this.simMarshal.Values)
        {
            foreach (var s in item.Values)
            {
                s.TryCompleteKinematic(currentTime);
            }
        }
    }

    public void StopAllKinematic()
    {
        foreach (var item in this.simMarshal.Values)
        {
            foreach (var s in item.Values)
            {
                s.CompletedKinematic();
            }
        }
    }

}
