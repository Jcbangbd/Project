using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class PickableShow : PickableItem  
{
    [SerializeField]
    private bool showDialog = false;
    [SerializeField]
    private int showID = 0;


    public override void OnPointerClick(PointerEventData eventData)
    {
        if (!showDialog)
        {
            base.OnPointerClick(eventData);
            return;
        }
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (gameObject.GetInstanceID() == pickedID)
            {
                if (pickedButton != PointerEventData.InputButton.Left)
                    EventSystem.current.SetSelectedGameObject(null);
            }
            pickedID = gameObject.GetInstanceID();
            pickedButton = PointerEventData.InputButton.Left;
            EventSystem.current.SetSelectedGameObject(gameObject, eventData);
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (gameObject.GetInstanceID() == pickedID)
            {
                if (pickedButton != PointerEventData.InputButton.Right)
                    EventSystem.current.SetSelectedGameObject(null);
            }
            pickedID = gameObject.GetInstanceID();
            pickedButton = PointerEventData.InputButton.Right;
            Vector2 screenPos = eventData.position;

            EventSystem.current.SetSelectedGameObject(gameObject, eventData);
            if (this.showID < UMP_Manager.Instance.Equip2Obj.Length)
            {
                RectTransform temp = UMP_Manager.Instance.Equip2Obj[this.showID];
                temp.gameObject.SetActive(true);
                temp.transform.position = new Vector3(screenPos.x, screenPos.y, 0);
            }
        }
    }

    public override void OnSelect(BaseEventData eventData)//
    {
        if (!showDialog)
        {
            base.OnSelect(eventData);
            return;
        }
        if (pickedButton == PointerEventData.InputButton.Left)
        {
            base.OnSelect(eventData);
        }
        else
        {

        }
    }

    public override void OnDeselect(BaseEventData eventData)//
    {
        if (!showDialog)
        {
            base.OnDeselect(eventData);
            return;
        }
        if (pickedButton == PointerEventData.InputButton.Right)
        {
            if (this.showID < UMP_Manager.Instance.Equip2Obj.Length)
                UMP_Manager.Instance.Equip2Obj[this.showID].gameObject.SetActive(false);
        }
        base.OnDeselect(eventData);
    }
}