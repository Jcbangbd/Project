using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;


public class LoadingScene : MonoBehaviour
{

    [SerializeField]
    Image[] picObject = null;
    private int currentIndx = 0;
    private int lastIndx;
    Color orginColor = new Color(0.156f, 0.156f, 0.156f);
    Color hightColor = new Color(1, 0.85f, 0);

    void Start()
    {
        StartCoroutine(this.ExecuteCountDown());
        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(1);
    }

    IEnumerator ExecuteCountDown()
    {

        this.lastIndx = this.picObject.Length - 1;
        while (true)
        {
            this.picObject[this.lastIndx].color = this.orginColor;
            this.picObject[this.currentIndx].color = this.hightColor;
            this.currentIndx++;
            this.lastIndx++;
            if (this.currentIndx == this.picObject.Length)
                this.currentIndx = 0;
            if (this.lastIndx == this.picObject.Length)
                this.lastIndx = 0;
            yield return new WaitForSecondsRealtime(1f);
        }
    }
}

