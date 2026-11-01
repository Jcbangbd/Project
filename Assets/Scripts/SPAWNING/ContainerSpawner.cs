using UnityEngine;

[AddComponentMenu("UserMiscellaneous/ContainerSpawner")]
public class ContainerSpawner : MonoBehaviour
{
    public AliasPrefab[] ContainerIns;

    [System.NonSerialized]
    public static ContainerSpawner Instance;

    private ContainerSpawner()
    {
        // this function will be called whenever an instance of the SpawnController class is made
        // first, we check that an instance does not already exist (this is a singleton, afterall!)
        if (Instance != null)
        {
            // drop out if instance exists, to avoid generating duplicates
            Debug.LogWarning("Tried to generate more than one instance of singleton ContainerSpawner.");
            return;
        }
        // as no instance already exists, we can safely set instance to this one
        Instance = this;
    }

    public string GetRandomOwnerName()
    {
        return ContainerIns[Random.Range(0, this.ContainerIns.Length)].AliasName;
    }

    public int GetRandomOwnerIndex()
    {
        return Random.Range(0, this.ContainerIns.Length);
    }

    /// <summary>
    /// 通过货主名获取Prefab
    /// </summary>
    /// <param name="ownerName">string</param>
    /// <returns>Prefab</returns>
    public GameObject GetPrefab(string ownerName)
    {
        foreach (var item in this.ContainerIns)
        {
            if (item.AliasName == ownerName)
                return item.PrefabIns;
        }
        int temp = Random.Range(0, this.ContainerIns.Length);
        return this.ContainerIns[temp].PrefabIns;
    }

    private GameObject GetPrefab(int ownerIndex)
    {
        if (ownerIndex >= 0 && ownerIndex < this.ContainerIns.Length)
            return this.ContainerIns[ownerIndex].PrefabIns;
        int temp = Random.Range(0, this.ContainerIns.Length);
        return this.ContainerIns[temp].PrefabIns;
    }

    public Container SpawnContainer(int shipOwnerIndex, float ctnLenght = Container.FortyFeet, float ctnHeight = Container.EightHalfFeet)
    {
        GameObject ins = GetPrefab(shipOwnerIndex);
        GameObject tempGO = SpawnController.Instance.SpawnGO(ins, ins.transform.position, ins.transform.rotation);
        Container container = tempGO.GetComponent<Container>();
        tempGO.transform.localScale = new Vector3(ctnLenght / container.LengthMetre, 1, ctnHeight / container.HeightMetre);
        container.LengthMetre = ctnLenght;
        container.HeightMetre = ctnHeight;
        return container;
    }
}

