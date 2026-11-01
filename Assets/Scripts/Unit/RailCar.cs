using UnityEngine;
using System.Collections;


public class RailCar : MonoBehaviour//若完善，要继承EquipBase
{
    [HideInInspector]
    public int CarID;
    //private EngagedBuffer buffer;
    public Locomotive Attached { get; set; }
    
    //void Awake()
    //{
    //    //this.buffer = GetComponent<EngagedBuffer>();
    //}

    //public void AddContainer(int bay, Container container)
    //{
    //    container.SlotFlag = new SlotEngaged(buffer, bay);

    //    this.buffer.EngagingContainer(bay, container);
    //}


}
