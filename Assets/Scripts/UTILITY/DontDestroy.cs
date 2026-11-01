using UnityEngine;
using System.Collections;

public class DontDestroy : MonoBehaviour 
{
    static DontDestroy Instance;
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            DestroyImmediate(gameObject);
            return;
        }
    }

}
