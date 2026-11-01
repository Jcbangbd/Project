using UnityEngine;
using System.Collections;

//[ExecuteInEditMode]
public class Locomotive : MonoBehaviour //若完善，要继承EquipBase
{
    public Vector3 FisrtOffset;
    public Vector3 IntervalOffset;
    public int QuantityOfCar = 40;

    public GameObject RailCarPrefab;

    void Awake()
    {

    }

    //// Use this for initialization
    void Start()
    {
        if (!RailCarPrefab)
            return;
        for (int i = 1; i <= QuantityOfCar; i++)
        {
            GameObject go = SpawnController.Instance.SpawnGO(RailCarPrefab,
                transform.position + this.FisrtOffset + (i - 1) * this.IntervalOffset,
                RailCarPrefab.transform.rotation);
            go.transform.SetParent(transform, true);
            go.name = string.Format("RailCar{0:D2}", i);
            RailCar car = go.GetComponent<RailCar>();
            car.Attached = this;
            car.CarID = i;
        }
    }
	
    //// Update is called once per frame
    //void Update () {
	
    //}
}
