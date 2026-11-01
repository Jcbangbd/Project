using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Duration : MonoBehaviour 
{
    [SerializeField]
    private float DurationTime = 1.5f;
    private Coroutine closeRoutine;
    [SerializeField]
    private DurationGroup attachGroup = null;
    private int indxInGroup;
    void Start()
    {
        if (this.attachGroup)
        {
            this.indxInGroup = this.attachGroup.AddDuration(this);
            this.attachGroup.OnChanged(this.indxInGroup);
        }
    }

    public void OnPointerEnter()
    {
        if (this.attachGroup)
            this.attachGroup.OnChanged(this.indxInGroup);

        if (this.closeRoutine != null)
            this.StopCoroutine(this.closeRoutine);
    }

    public void TryClose()
    {
        if (this.gameObject.activeInHierarchy)
            this.closeRoutine = StartCoroutine(this.WaitDuration());
    }

    IEnumerator WaitDuration()
    {
        float countTime = 0;
        while (countTime < this.DurationTime)
        {
            yield return new WaitForSecondsRealtime(0.1f);
            countTime += 0.5f;
        }
        this.closeRoutine = null;
        this.gameObject.SetActive(false);
    }

    void OnDisable()
    {
        if (this.closeRoutine != null)
            this.StopCoroutine(this.closeRoutine);
    }
}