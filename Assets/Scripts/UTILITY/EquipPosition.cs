using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZPMC.VR.Unity
{
    [Serializable]
    public class EquipPosition
    {
        public int EquipID { get; set; } //所有设备
        public string EquipName { get; set; }//所有设备
        public string PrefabAlias { get; set; }//所有设备
        public float GantryX { get; set; }//所有设备
        public float GantryZ { get; set; }//所有设备
        public float GantryRotationEulerY { get; set; }//所有设备
        public float TrolleyX { get; set; } //桥吊、轨道吊
        public float TrolleyZ { get; set; } //桥吊、轨道吊
        public float RotatingTrolleyEulerY { get; set; } //带旋转小车的轨道吊
        public float HoistY { get; set; } //桥吊、轨道吊
        public float SpreaderScale { get; set; }//桥吊、轨道吊
        public float AuxiliaryTrolleyX { get; set; }//桥吊
        public float AuxiliaryTrolleyZ { get; set; }//桥吊
        public float AuxiliaryHoistY { get; set; }//桥吊
        public float AuxiliarySpreaderScale { get; set; }//桥吊

        public EquipPosition()
        {
        }

        public EquipPosition(EquipBase equip)
        {
            this.EquipID = equip.EquipID;
            this.EquipName = equip.name;
            this.GantryX = equip.transform.position.x;
            this.GantryZ = equip.transform.position.z;
            this.GantryRotationEulerY = equip.transform.rotation.eulerAngles.y;
        }

        protected EquipBase FindEquip(int equipID)
        {
            EquipBase equip = EquipManager.Instance.FindEquip(equipID);
            if (equip == null)
            {
                if (!string.IsNullOrEmpty(this.PrefabAlias))
                {
                    GameObject go = EquipSpawner.Instance.SpawnGameObject(this.PrefabAlias);
                    go.name = this.EquipName;
                    equip = go.GetComponent<EquipBase>();//
                    equip.EquipID = this.EquipID;
                    equip.AddToManager();
                }
                else
                {
                    return null;
                }
            }
            else
            {
                //归位旋转，用于重新赋值
                GameObject cloneInstance = EquipSpawner.Instance.GetPrefab(this.PrefabAlias);
                if (!cloneInstance)
                    Debug.LogError("Can't Find the prefab name is " + this.PrefabAlias);
                if (!equip.gameObject.activeInHierarchy)
                    equip.gameObject.SetActive(true);

                equip.transform.rotation = cloneInstance.transform.rotation;
                Entity rTrolley = equip.FindElementWithTagName("RotatingTrolley");
                if (rTrolley) //有旋转小车
                {
                    EquipBase cloneEquip=cloneInstance.GetComponent<EquipBase>();
                    Transform cloneRotating = cloneEquip.FindChildWithTagName("RotatingTrolley");
                    rTrolley.transform.rotation = cloneRotating.rotation;
                }
            }

            return equip;
        }

        public virtual void InitialPosition()
        {
            EquipBase equip = this.FindEquip(this.EquipID);
            //Gantry
            equip.transform.position = new Vector3(this.GantryX, 0, this.GantryZ);
            equip.transform.Rotate(0, this.GantryRotationEulerY, 0, Space.World);
            //KinematicAnimation k = equip.GetComponent<KinematicAnimation>();
            //if (k)
            //{
            //    k.SetNormalizedTime(0f);
            //    //Debug.Log(string.Format("Initial {0}", k.NormalizedTime));
            //}
            //(main) trolley
            Entity trolley = equip.FindElementWithTagName("Trolley");
            if (trolley)
                trolley.transform.position = new Vector3(this.TrolleyX, trolley.transform.position.y, this.TrolleyZ);
            //(main) Rotating Trolley
            Entity rTrolley = equip.FindElementWithTagName("RotatingTrolley");
            if (rTrolley)
                rTrolley.transform.Rotate(0, this.RotatingTrolleyEulerY, 0, Space.World);
            //(main) Hoist
            Entity hoist = equip.FindElementWithTagName("Hoist");
            if (hoist)
                hoist.transform.position = new Vector3(hoist.transform.position.x, HoistY, hoist.transform.position.z);
            //(main) Spreader
            Entity spreader = equip.FindElementWithTagName("Spreader");
            if (spreader)
                spreader.transform.localScale = new Vector3(SpreaderScale, 1, 1);
            //Auxiliary Trolley
            Entity trolleyAuxiliary = equip.FindElementWithTagName("AuxiliaryTrolley");
            if (trolleyAuxiliary)
                trolleyAuxiliary.transform.position = new Vector3(this.AuxiliaryTrolleyX, trolleyAuxiliary.transform.position.y, AuxiliaryTrolleyZ);
            //Auxiliary Hoist
            Entity hoistAuxiliary = equip.FindElementWithTagName("AuxiliaryHoist");
            if (hoistAuxiliary)
                hoistAuxiliary.transform.position = new Vector3(hoistAuxiliary.transform.position.x, AuxiliaryHoistY, hoistAuxiliary.transform.position.z);
            //Auxiliary Spreader
            Entity spreaderAuxiliary = equip.FindElementWithTagName("AuxiliarySpreader");
            if (spreaderAuxiliary)
                spreaderAuxiliary.transform.localScale = new Vector3(AuxiliarySpreaderScale, 1, 1);
        }
    }


    [Serializable]
    public class PositionSetting
    {
        public List<EquipPosition> InitialPosition { get; set; }

        public PositionSetting()
        {
            this.InitialPosition = new List<EquipPosition>();
        }

        public void InitilizePosition()
        {
            foreach (var item in this.InitialPosition)
            {
                item.InitialPosition();
            }
        }

        public void Clear()
        {
            this.InitialPosition.Clear();
            this.InitialPosition = null;
        }
    }
}
