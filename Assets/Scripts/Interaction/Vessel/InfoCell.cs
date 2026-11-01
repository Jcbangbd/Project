using System;
using UnityEngine;
using UnityEngine.UI;

namespace ZPMC.VR.Unity
{
    public class InfoCell : MonoBehaviour, IComparable<InfoCell>
    {
        public int tier = 0;

        private Toggle currentToggle;
        public bool isOn
        {
            get
            {
                if (!this.currentToggle)
                    return true;
                else
                    return this.currentToggle.isOn;
            }
        }

        public InfoColumn AttachedColumn { private get; set; }

        public int Column
        {
            get
            {
                return this.AttachedColumn.Column;
            }
        }

        void Start()
        {
            this.currentToggle = GetComponent<Toggle>();
        }

        public void OnCheckChanged(bool isOn)
        {
            if (isOn)
            {
                this.AttachedColumn.OnSelectOn(this.tier);
            }
            else
            {
                this.AttachedColumn.OnSelectOff(this.tier);
            }
        }

        public void ChangeManual(bool isOn)
        {
            Toggle t = this.GetComponent<Toggle>();
            t.isOn = isOn;
        }

        public void SetSelectable(bool isOn)
        {
            this.currentToggle.interactable = isOn;
        }

        public int CompareTo(InfoCell other)
        {
            return other.tier - this.tier;
        }
    }
}