using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DubControl : MonoBehaviour
{
    private AudioSource[] audioList;
    private int indx;
    private int count;

    private static DubControl uniqueInstance;
    public static DubControl UniqueInstance
    {
        get { return uniqueInstance; }
        set
        {
            uniqueInstance = value;
            if (uniqueInstance)
            {
                uniqueInstance.UserReset();
            }
        }
    }

    public void UserReset()
    {

        if (this.count == 1)
        {
            this.audioList[0].Play();
            this.indx = 0;
        }
        else
            this.indx = -1;
    }

    private void Awake()
    {
        audioList = GetComponentsInChildren<AudioSource>();
        this.count = audioList.Length;
    }


    // Use this for initialization
    public void AdvancePlay()
    {
        if (this.indx < this.count)
            this.audioList[++indx].Play();
    }

    public void Stop()
    {
        if (this.indx < this.count)
            this.audioList[indx].Stop();
    }

    public void UnPause()
    {
        if (this.indx < this.count)
            this.audioList[indx].UnPause();
    }
}
