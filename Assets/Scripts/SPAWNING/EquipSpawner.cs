using UnityEngine;
using System.Collections;

[AddComponentMenu("UserMiscellaneous/EquipSpawner")]
public class EquipSpawner : MonoBehaviour 
{
    public AliasPrefab[] EquipPrefabs;

    [System.NonSerialized]
    public static EquipSpawner Instance;

    private EquipSpawner()
    {
        // this function will be called whenever an instance of the SpawnController class is made
        // first, we check that an instance does not already exist (this is a singleton, afterall!)
        if (Instance != null)
        {
            // drop out if instance exists, to avoid generating duplicates
            Debug.LogWarning("Tried to generate more than one instance of singleton EquipSpawner.");
            return;
        }
        // as no instance already exists, we can safely set instance to this one
        Instance = this;
    }

    public GameObject GetPrefab(string alias)
    {
        foreach (var item in this.EquipPrefabs)
        {
            if (item.AliasName == alias)
                return item.PrefabIns;
        }
        return null;
    }

    public GameObject SpawnGameObject(string alias)
    {
        GameObject ins = GetPrefab(alias);
        if (!ins)
        {
            throw new UnityException(string.Format("Can't find the Prefab while spawning a Equipment. Alias Name is {0}", alias));
        }
        GameObject go = SpawnController.Instance.SpawnGO(ins);

        return go;
    }
}
