using System;
using System.Collections;
using System.Collections.Generic;

namespace ZPMC.VR.Unity
{
    [Serializable]
    public class DriverSetting
    {
        public List<DriverEquip> Setting;

        public DriverSetting()
        {
            this.Setting = new List<DriverEquip>();
        }

        public void BuildDriver(Simulator simulator)
        {
            foreach (var item in this.Setting)
            {
                item.BuildDriver(simulator);
            }
        }
    }

    [Serializable]
    public class DriverEquip
    {
        public List<int> EquipList { get; set; } //相关设备
        public List<DriverEntity> EntityList { get; set; }

        public DriverEquip()
        {
            this.EquipList = new List<int>();
            this.EntityList = new List<DriverEntity>();
        }

        public void BuildDriver(Simulator simulator)
        {
            foreach (int i in this.EquipList)
            {
                EquipBase equip = EquipManager.Instance.FindEquip(i);
                if (equip == null)
                    continue;

                foreach (DriverEntity item in this.EntityList)
                {
                    item.BuildDriver(equip, simulator);
                }
            }
        }
    }

    [Serializable]
    public class DriverEntity
    {
        public string EntityName { get; set; }
        public List<DriverDof> DofList { get; set; }

        public DriverEntity()
        {
            this.DofList = new List<DriverDof>();
        }

        public void BuildDriver(EquipBase equip, Simulator simulator)
        {
            Entity e = equip.FindElementWithTagName(this.EntityName);
            if (e)
            {
                foreach (DriverDof item in this.DofList)
                {
                    item.BuildDriver(e, simulator);
                }
            }
            else
            {
                foreach (DriverDof item in this.DofList)
                {
                    item.BuildDriver(equip, simulator);
                }
            }
        }
    }

    [Serializable]
    public class DriverDof
    {
        public int DriverID { get; set; }
        public DOF dof { get; set; }
        public float MaxSpeed { get; set; }
        public float AccTime { get; set; }

        public DriverDof()
        {
        }

        //public DriverDof(int id, DOF dof)
        //    : this()
        //{
        //    this.DriverID = id;
        //    this.dof = dof;
        //}

        public void BuildDriver(Entity e, Simulator simulator)
        {
            switch (this.dof)
            {
                case DOF.TranslationX:
                case DOF.TranslationY:
                case DOF.TranslationZ:
                    {
                        SimRun controller = new SimRun(e.transform, this.dof);
                        controller.MaxSpeed = this.MaxSpeed;
                        controller.AccTime = this.AccTime;
                        controller.IndexInEquip = this.DriverID;
                        simulator.AddController(e.AttachedEquip.EquipID, controller);
                        break;
                    }
                case DOF.RotationX:
                case DOF.RotationY:
                case DOF.RotationZ:
                    {
                        SimRotate controller = new SimRotate(e.transform, this.dof);
                        controller.MaxSpeed = this.MaxSpeed;
                        controller.AccTime = this.AccTime;
                        controller.IndexInEquip = this.DriverID;
                        simulator.AddController(e.AttachedEquip.EquipID, controller);
                        break;
                    }
                case DOF.ScaleX:
                case DOF.ScaleY:
                case DOF.ScaleZ:
                    {
                        SimScale controller = new SimScale(e.transform, this.dof);
                        controller.IndexInEquip = this.DriverID;
                        simulator.AddController(e.AttachedEquip.EquipID, controller);
                        break;
                    }
                case DOF.LocalRotX:
                case DOF.LocalRotY:
                case DOF.LocalRotZ:
                    {
                        SimLocalRot controller = new SimLocalRot(e.transform, this.dof);
                        controller.IndexInEquip=this.DriverID;
                        simulator.AddController(e.AttachedEquip.EquipID,controller);
                        break;
                    }
                //case DOF.Animation:
                //    {
                //        SimAnimation controller = new SimAnimation(e.transform, this.dof);
                //        controller.IndexInEquip = this.DriverID;
                //        simulator.AddController(e.AttachedEquip.EquipID, controller);
                //        break;
                //    }
                case DOF.Swerve:
                    {
                        SimSwerve controller = new SimSwerve(e.transform, this.dof) { IndexInEquip = this.DriverID };
                        //controller.IndexInEquip = this.DriverID;
                        simulator.AddController(e.AttachedEquip.EquipID, controller);
                        break;
                    }
                default:
                    throw new Exception("Can't identify the Dof:" + this.dof.ToString());
            }
        }

        public void BuildDriver(EquipBase equip, Simulator simulator)
        {
            switch (this.dof)
            {
                case DOF.Animation:
                    {
                        SimAnimation controller = new SimAnimation(equip.transform, this.dof);
                        controller.IndexInEquip = this.DriverID;
                        simulator.AddController(equip.EquipID, controller);
                        break;
                    }
                default:
                    throw new Exception(string.Format("Can't identify the Dof:{0},EquipID={1}", this.dof.ToString(), equip.EquipID));
            }
        }
    }

}
