using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class DurationGroup : MonoBehaviour 
{
    private SortedList<int,Duration> nestDuration;
    private int count;

    void Awake()
    {
        this.nestDuration = new SortedList<int, Duration>();
        this.count = -1;
    }

    public int AddDuration(Duration item)
    {
        this.count++;
        this.nestDuration.Add(this.count,item);
        return this.count;
    }

    public void OnChanged(int indx)
    {
        foreach(var item in this.nestDuration)
        {
            if (item.Key == indx)
                continue;
            if (item.Value.gameObject.activeInHierarchy)
                item.Value.gameObject.SetActive(false);
        }
    }

}
