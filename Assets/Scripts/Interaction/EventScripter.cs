using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace ZPMC.VR.Unity
{
    public class EventScripter
    {
        public SequenceList<SimRecord> recordMarshal;

        public Dictionary<int, Vehicle> agvs;
        public Dictionary<int, RMG> ascs;
        public Dictionary<int, QC> stss;
        public Dictionary<int, TP> mates;
        public Dictionary<int, Vehicle> cars;

        #region Creation
        private static EventScripter localObject;
        private EventScripter()
        {
            recordMarshal = new SequenceList<SimRecord>();

            agvs = new Dictionary<int, Vehicle>();
            ascs = new Dictionary<int, RMG>();
            stss = new Dictionary<int, QC>();
            mates = new Dictionary<int, TP>();
            cars = new Dictionary<int, Vehicle>();
        }
        public static EventScripter GetGlobalInstance()
        {
            if (localObject == null)
            {
                localObject = new EventScripter();
            }

            return localObject;
        }
        #endregion

        private const float Default_RMG_Hoist = 17.85f;
        private const float Default_QC_Hoist_Fore = 30f;
        private const float Default_QC_Hoist_Back = 15f;
        private const float Default_QC_Trolley_Back = -45f;
        private const float Default_Container_Height = 2.591f;
        private const float Default_TC_Hoist = 11.85f;
        private const float Default_AntiSway = 0.75f;
        private const float Default_AntiSwag_Speed = 0.10f;
        private const float Default_Mate_Up = 30f;
        private const float Default_Mate_Down = 18f;
        private const float Default_AGV_SwerveTime = 18.103178f; //9.051589f
        private const float Default_AGV_SwerveSpeed = 1.5f;
        private const float Default_AGV_SwerveRadius = 16f;
        private const float Default_AGV_Sidelong = 6f;
        private const float Default_Truck_SwerveTime = 10f;
        private const float Default_Truck_SwerveSpeed = 3;

        private const float Default_TC_SwerveTime = 15f;

        public void Initialize()
        {
            if (!Simulator.Instance)
                return;

            this.stss.Clear();
            foreach (EquipBase item in EquipManager.Instance.GetGroup(EquipBase.STS_Code))
            {
                QC qc = new QC();
                qc.equip = item;
                Entity t;
                qc.gantry = item.transform.position.x;
                qc.xK = (SimRun)Simulator.Instance.FindSimulator(item.EquipID, 1);// (KinematicTranslate)item.FindKinematic(1);
                t = item.FindElementWithTagName("Trolley");
                qc.trolley1 = t.transform.position.z;
                qc.zK1 = (SimRun)Simulator.Instance.FindSimulator(item.EquipID, 2);// (KinematicTranslate)item.FindKinematic(2);
                t = item.FindElementWithTagName("Hoist");
                qc.hoist1 = t.transform.position.y;
                qc.yK1 = (SimRun)Simulator.Instance.FindSimulator(item.EquipID, 3); //(KinematicTranslate)item.FindKinematic(3);
                t = item.FindElementWithTagName("AuxiliaryTrolley");
                qc.trolley2 = t.transform.position.z;
                qc.zK2 = (SimRun)Simulator.Instance.FindSimulator(item.EquipID, 12);  //(KinematicTranslate)item.FindKinematic(12);
                t = item.FindElementWithTagName("AuxiliaryHoist");
                qc.hoist2 = t.transform.position.y;
                qc.yK2 = (SimRun)Simulator.Instance.FindSimulator(item.EquipID, 13);  //(KinematicTranslate)item.FindKinematic(13);

                this.stss.Add(item.EquipID, qc);
            }

            this.agvs.Clear();
            foreach (EquipBase item in EquipManager.Instance.GetGroup(EquipBase.AGV_Code))
            {
                Vehicle agv = new Vehicle();
                agv.equip = item;
                //
                agv.xK = (SimRun)Simulator.Instance.FindSimulator(item.EquipID, 1); // (KinematicRunning)item.FindKinematic(1);
                agv.zK = (SimRun)Simulator.Instance.FindSimulator(item.EquipID, 2); // (KinematicRunning)item.FindKinematic(2);
                agv.yK = (SimRun)Simulator.Instance.FindSimulator(item.EquipID, 3); 
                agv.yRotK = (SimRotate)Simulator.Instance.FindSimulator(item.EquipID, 0); //(KinematicRotate)item.FindKinematic(0);
                //
                agv.x = item.transform.position.x;//item.FindKinematic(1).transform.position.x;
                agv.z = item.transform.position.z;// item.FindKinematic(2).transform.position.z;
                agv.yRot = item.transform.rotation.eulerAngles.y;// item.FindKinematic(0).transform.rotation.eulerAngles.y;
                
                this.agvs.Add(item.EquipID, agv);
            }

            this.cars.Clear();
            foreach (EquipBase item in EquipManager.Instance.GetGroup(EquipBase.Truck_Code))
            {
                Vehicle car = new Vehicle();
                car.equip = item;

                car.x = item.transform.position.x;// item.FindKinematic(1).transform.position.x;//
                car.z = item.transform.position.z;// item.FindKinematic(2).transform.position.z;//
                car.yRot = item.transform.rotation.eulerAngles.y;// item.FindKinematic(0).transform.rotation.eulerAngles.y;//

                car.xK = (SimRun)Simulator.Instance.FindSimulator(item.EquipID, 1); //(KinematicRunning)item.FindKinematic(1);
                car.zK = (SimRun)Simulator.Instance.FindSimulator(item.EquipID, 2); // (KinematicRunning)item.FindKinematic(2);
                car.yRotK = (SimRotate)Simulator.Instance.FindSimulator(item.EquipID, 0); ;// (KinematicRotate)item.FindKinematic(0);

                this.cars.Add(item.EquipID, car);
            }

            this.ascs.Clear();
            foreach (EquipBase item in EquipManager.Instance.GetGroup(EquipBase.ASC_Code))
            {
                RMG rmg = new RMG();
                rmg.equip = item;
                Entity t;
                rmg.gantryX = item.transform.position.x;// item.FindKinematic(1).transform.position.x;
                rmg.gantryZ = item.transform.position.z;// item.FindKinematic(1).transform.position.z;
                rmg.gantryK = (SimRun)Simulator.Instance.FindSimulator(item.EquipID, 1); // (KinematicTranslate)item.FindKinematic(1);
                t = item.FindElementWithTagName("Trolley");
                rmg.trolleyZ = t.transform.position.z;// item.FindKinematic(2).transform.position.z;
                rmg.trolleyX = t.transform.position.x;// item.FindKinematic(2).transform.position.x;
                rmg.trolleyK = (SimRun)Simulator.Instance.FindSimulator(item.EquipID, 2); // (KinematicTranslate)item.FindKinematic(2);
                t = item.FindElementWithTagName("Hoist");
                rmg.hoist = t.transform.position.y;// item.FindKinematic(3).transform.position.y;
                rmg.hoistK = (SimRun)Simulator.Instance.FindSimulator(item.EquipID, 3); // (KinematicTranslate)item.FindKinematic(3);
                this.ascs.Add(item.EquipID, rmg);
            }

            this.mates.Clear();
            foreach (EquipBase item in EquipManager.Instance.GetGroup(EquipBase.Mate_Code))
            {
                TP mate = new TP();
                mate.equip = item;
                //mate.a = (SimAnimation)Simulator.Instance.FindSimulator(item.EquipID, 0); //(KinematicAnimation)item.FindKinematic(0);
                //mate.currentValue = 0;// ((KinematicAnimation)item.FindKinematic(0)).NormalizedTime;

                this.mates.Add(item.EquipID, mate);
            }
        }

        #region STS
        public float STS_Discharging(float advanceTime, QC qc, string vessel, int bay, int column, int tier, int midBuffer, Vehicle agv, bool isSubtitle = false)
        {
            Container container = VesselManager.GlobalInstance().FindContainer(vessel, bay, column, tier);

            return this.STS_Discharging(advanceTime, qc, container, midBuffer, agv, isSubtitle);
        }

        public float STS_Discharging(float advanceTime, QC qc, Container container, int midBuffer, Vehicle agv, bool isSubtitle = false)
        {
            SimRecord record = new SimRecord(); float tempTime = advanceTime;

            record.EquipCode = (int)DefinedType.STS;//大车到装卸位置
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 1;
            record.StartPosition = qc.gantry.ToString();
            qc.gantry = container.transform.position.x;
            record.EndPosition = qc.gantry.ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.xK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//主小车到装卸位置
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 2;
            record.StartPosition = qc.trolley1.ToString();
            qc.trolley1 = container.transform.position.z;
            record.EndPosition = qc.trolley1.ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.zK1.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
            {
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = tempTime, EndTime = advanceTime, Tag = "岸桥大车就位，海侧小车运行至卸船箱位；同时，AGV运行至岸桥作业区" });
                tempTime = advanceTime;
            }

            record = new SimRecord();//主吊具下降到装卸位置上方（防摇定位）
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;
            record.StartPosition = qc.hoist1.ToString();
            qc.hoist1 = container.transform.position.y + container.HeightMetre + Default_AntiSway;
            record.EndPosition = qc.hoist1.ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.yK1.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//主吊具下降到装卸位置上方
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;
            record.StartPosition = qc.hoist1.ToString();
            qc.hoist1 = container.transform.position.y + container.HeightMetre;
            record.EndPosition = qc.hoist1.ToString();
            record.StartTime = advanceTime;
            advanceTime += Default_AntiSway / Default_AntiSwag_Speed;//qc.yK1.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//取箱
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.LoadingType;
            record.EquipSub = 4;
            record.StartPosition = container.SlotFlag.ToString();
            record.EndPosition = 0.ToString(); //2 20160707
            record.StartTime = advanceTime;
            advanceTime += 5f;
            record.EndTime = advanceTime;
            record.Tag = DefinedType.VesselBuffer.ToString();
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//主吊具抬升
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;
            record.StartPosition = qc.hoist1.ToString();
            qc.hoist1 = Default_QC_Hoist_Fore;
            record.EndPosition = qc.hoist1.ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.yK1.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
            {
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = tempTime, EndTime = advanceTime, Tag = "海侧小车吊具下降并抓取集装箱，起升至安全高度" });
                tempTime = advanceTime;
            }

            record = new SimRecord();//主小车运行到装卸位（平台上方）
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 2;
            record.StartPosition = qc.trolley1.ToString();
            qc.trolley1 = qc.equip.FindEngagedBuffer(midBuffer).GetWorldPosition(2).z;
            record.EndPosition = qc.trolley1.ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.zK1.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
            {
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = tempTime, EndTime = advanceTime, Tag = "海侧小车带箱运行至中转平台上方" });
                tempTime = advanceTime;
            }

            record = new SimRecord();//主吊具下降到防摇定位位置
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;
            record.StartPosition = qc.hoist1.ToString();
            qc.hoist1 = qc.equip.FindEngagedBuffer(midBuffer).GetWorldPosition(0).y + container.HeightMetre + Default_AntiSway;
            record.EndPosition = qc.hoist1.ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.yK1.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//主吊具下降到卸箱位
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;
            record.StartPosition = qc.hoist1.ToString();
            qc.hoist1 = qc.equip.FindEngagedBuffer(midBuffer).GetWorldPosition(0).y + container.HeightMetre;
            record.EndPosition = qc.hoist1.ToString();
            record.StartTime = advanceTime;
            advanceTime += Default_AntiSway / Default_AntiSwag_Speed;//qc.yK1.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord(); //卸箱
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.DischargingType;
            record.EquipSub = 4;
            record.StartPosition = 0.ToString();//2 20160707
            record.EndPosition = (new SlotEngaged(qc.equip.FindEngagedBuffer(midBuffer), 0)).ToString();//2 20160707
            record.StartTime = advanceTime;
            advanceTime += 5f;
            record.EndTime = advanceTime;
            record.Tag = "STS";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//主吊具上升
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;
            record.StartPosition = qc.hoist1.ToString();
            qc.hoist1 = Default_QC_Hoist_Fore;
            record.EndPosition = (qc.hoist1).ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.yK1.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord(); //副小车平移到平台上
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 12;
            record.StartPosition = qc.trolley2.ToString();
            qc.trolley2 = qc.equip.FindEngagedBuffer(midBuffer).GetWorldPosition(0).z;//2 20160707
            record.EndPosition = qc.trolley2.ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.zK2.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
            {
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = tempTime, EndTime = advanceTime, Tag = "海侧小车卸箱后运行至安全位置，陆侧小车行驶至中转平台上方" });
                tempTime = advanceTime;
            }

            record = new SimRecord();//副吊具下降至集装箱（防摇定位）
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 13;
            record.StartPosition = qc.hoist2.ToString();
            qc.hoist2 = qc.equip.FindEngagedBuffer(midBuffer).GetWorldPosition(0).y + container.HeightMetre + Default_AntiSway; //2 20160707
            record.EndPosition = (qc.hoist2).ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.yK2.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//副吊具下降至集装箱
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 13;
            record.StartPosition = qc.hoist2.ToString();
            qc.hoist2 = qc.equip.FindEngagedBuffer(midBuffer).GetWorldPosition(0).y + container.HeightMetre; //
            record.EndPosition = (qc.hoist2).ToString();
            record.StartTime = advanceTime;
            advanceTime += Default_AntiSway / Default_AntiSwag_Speed;// qc.yK2.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//抓箱
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.LoadingType;
            record.EquipSub = 14;
            record.StartPosition = (new SlotEngaged(qc.equip.FindEngagedBuffer(midBuffer), 0)).ToString();//
            record.EndPosition = 0.ToString();//
            record.StartTime = advanceTime;
            advanceTime += 3f;
            record.EndTime = advanceTime;
            record.Tag = "STS";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//副吊具起升
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 13;
            record.StartPosition = qc.hoist2.ToString();
            qc.hoist2 = Default_QC_Hoist_Back;
            record.EndPosition = (qc.hoist2).ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.yK2.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//副小车平移
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 12;
            record.StartPosition = qc.trolley2.ToString();
            qc.trolley2 = agv.z;
            record.EndPosition = qc.trolley2.ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.zK2.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
            {
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = tempTime, EndTime = advanceTime, Tag = "陆侧小车抓箱提升至安全高度，自动运行至AGV上方" });
                tempTime = advanceTime;
            }

            record = new SimRecord();//副吊具下降到AGV（防摇定位）
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 13;
            record.StartPosition = qc.hoist2.ToString();
            qc.hoist2 = agv.equip.FindEngagedBuffer(3).GetWorldPosition(0).y + Default_Container_Height + Default_AntiSway; ////2 20160707
            record.EndPosition = qc.hoist2.ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.zK2.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//副吊具下降到AGV
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 13;
            record.StartPosition = qc.hoist2.ToString();
            qc.hoist2 = agv.equip.FindEngagedBuffer(3).GetWorldPosition(0).y + Default_Container_Height; //
            record.EndPosition = qc.hoist2.ToString();
            record.StartTime = advanceTime;
            advanceTime += Default_AntiSway / Default_AntiSwag_Speed;//qc.zK2.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//落箱
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.DischargingType;
            record.EquipSub = 14;
            record.StartPosition = 0.ToString();//2 20160707
            record.EndPosition = (new SlotEngaged(agv.equip.FindEngagedBuffer(3), 0)).ToString();//2 20160707
            record.StartTime = advanceTime;
            advanceTime += 3f;
            record.EndTime = advanceTime;
            record.Tag = "STS";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//副吊具起升
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 13;
            record.StartPosition = qc.hoist2.ToString();
            qc.hoist2 = Default_QC_Hoist_Back;
            record.EndPosition = qc.hoist2.ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.zK2.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
            {
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = tempTime, EndTime = advanceTime, Tag = "陆侧小车卸箱至AGV后，运行至安全位置" });
                //this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = advanceTime, EndTime = 100000, Tag = "卸船任务完成！" });
            }

            qc.advanceTime = advanceTime;

            return advanceTime;
        }

        public float STS_Loading(float advanceTime, QC qc, string vessel, int bay, int column, int tier, int midBuffer, Vehicle agv, bool isSubtitle = false)
        {
            SimRecord record;
            float tempTime = advanceTime;
            VesselBuffer buffer = VesselManager.GlobalInstance().FindVesselBuffer(vessel);

            record = new SimRecord(); //大车移动
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 1;//
            record.StartPosition = qc.gantry.ToString();
            qc.gantry = buffer.GetWorldBayGauge(bay);
            record.EndPosition = qc.gantry.ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.xK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//副小车就位
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 12;//
            record.StartPosition = qc.trolley2.ToString();
            qc.trolley2 = agv.z;
            record.EndPosition = qc.trolley2.ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.zK2.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
            {
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = tempTime, EndTime = advanceTime, Tag = "岸桥大车就位，陆侧小车运行至AGV上方" });
                tempTime = advanceTime;
            }

            record = new SimRecord();//副吊具下降到目标位（防摇定位）
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 13;//
            record.StartPosition = qc.hoist2.ToString();
            qc.hoist2 = agv.equip.FindEngagedBuffer(3).GetWorldPosition(0).y + Default_Container_Height + Default_AntiSway;//2 20160707
            record.EndPosition = qc.hoist2.ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.yK2.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//副吊具下降到目标位
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 13;//
            record.StartPosition = qc.hoist2.ToString();
            qc.hoist2 = qc.hoist2 - Default_AntiSway;//agv.equip.FindEngagedBuffer(3).GetWorldPosition(0).y + Default_Container_Height;//
            record.EndPosition = qc.hoist2.ToString();
            record.StartTime = advanceTime;
            advanceTime += Default_AntiSway / Default_AntiSwag_Speed;// qc.yK2.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);
            
            record = new SimRecord();//抓箱
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.LoadingType;
            record.EquipSub = 14;
            record.StartPosition = (new SlotEngaged(agv.equip.FindEngagedBuffer(3), 0)).ToString(); //2 20160707
            record.EndPosition = 0.ToString();//2 20160707
            record.StartTime = advanceTime;
            advanceTime += 3f;
            record.EndTime = advanceTime;
            record.Tag = "STS";
            this.recordMarshal.Enqueue(record);

            agv.advanceTime = advanceTime + 3;

            record = new SimRecord();//副吊具上升
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 13;//
            record.StartPosition = qc.hoist2.ToString();
            qc.hoist2 = Default_QC_Hoist_Back;
            record.EndPosition = qc.hoist2.ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.yK2.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);
                        
            if (isSubtitle)
            {
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = tempTime, EndTime = advanceTime, Tag = "陆侧小车吊具自动下降并抓取集装箱，起升至安全高度" });
                tempTime = advanceTime;
            }

            record = new SimRecord();//副小车平移到中转平台上方
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 12;
            record.StartPosition = qc.trolley2.ToString();
            qc.trolley2 = qc.equip.FindEngagedBuffer(midBuffer).GetWorldPosition(0).z;//2 20160707
            record.EndPosition = qc.trolley2.ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.zK2.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);
                        
            if (isSubtitle)
            {
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = tempTime, EndTime = advanceTime, Tag = "陆侧小车自动运行至中转平台， AGV离开岸桥作业区，进入缓冲区" });
                tempTime = advanceTime;
            }

            record = new SimRecord();//副吊具下降到防摇定位位置
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 13;
            record.StartPosition = qc.hoist2.ToString();
            qc.hoist2 = qc.equip.FindEngagedBuffer(midBuffer).GetWorldPosition(0).y + Default_Container_Height + Default_AntiSway;//2 20160707
            record.EndPosition = (qc.hoist2).ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.yK2.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//副吊具下降到卸箱位
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 13;
            record.StartPosition = qc.hoist2.ToString();
            qc.hoist2 = qc.equip.FindEngagedBuffer(midBuffer).GetWorldPosition(0).y + Default_Container_Height;//2 20160707
            record.EndPosition = (qc.hoist2).ToString();
            record.StartTime = advanceTime;
            advanceTime += Default_AntiSway / Default_AntiSwag_Speed;//qc.yK2.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            //if (isSubtitle)
            //{
            //    this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = tempTime, EndTime = advanceTime, Tag = "陆测吊具下降到平台上方进行卸箱" });
            //    tempTime = advanceTime;
            //}

            record = new SimRecord();//卸箱
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.DischargingType;
            record.EquipSub = 14;
            record.StartPosition = 0.ToString();//2 20160707
            record.EndPosition = (new SlotEngaged(qc.equip.FindEngagedBuffer(midBuffer), 0)).ToString();//2 20160707
            record.StartTime = advanceTime;
            advanceTime += 3f;
            record.EndTime = advanceTime;
            record.Tag = "STS";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//副吊具上升
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 13;
            record.StartPosition = qc.hoist2.ToString();
            qc.hoist2 = Default_QC_Hoist_Back;
            record.EndPosition = (qc.hoist2).ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.yK2.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord(); //副小车移开
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 12;
            record.StartPosition = qc.trolley2.ToString();
            qc.trolley2 = Default_QC_Trolley_Back;
            record.EndPosition = qc.trolley2.ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.zK2.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
            {
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = tempTime, EndTime = advanceTime, Tag = "陆侧小车自动卸箱至中转平台，返回到安全位置" });
                tempTime = advanceTime;
            }

            record = new SimRecord(); //主小车到中转平台上方
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 2;
            record.StartPosition = qc.trolley1.ToString();
            qc.trolley1 = qc.equip.FindEngagedBuffer(midBuffer).GetWorldPosition(0).z;//2 20160707
            record.EndPosition = qc.trolley1.ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.zK1.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//主吊具下降到集装箱（防摇定位）
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;
            record.StartPosition = qc.hoist1.ToString();
            qc.hoist1 = qc.equip.FindEngagedBuffer(midBuffer).GetWorldPosition(0).y + Default_Container_Height + Default_AntiSway;//2 20160707
            record.EndPosition = qc.hoist1.ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.yK1.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//主吊具下降到集装箱
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;
            record.StartPosition = qc.hoist1.ToString();
            qc.hoist1 = qc.equip.FindEngagedBuffer(midBuffer).GetWorldPosition(0).y + Default_Container_Height;//2 20160707
            record.EndPosition = qc.hoist1.ToString();
            record.StartTime = advanceTime;
            advanceTime += Default_AntiSway / Default_AntiSwag_Speed;//qc.yK1.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//取箱
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.LoadingType;
            record.EquipSub = 4;
            record.StartPosition = (new SlotEngaged(qc.equip.FindEngagedBuffer(midBuffer), 0)).ToString();//2 20160707
            record.EndPosition = 0.ToString(); //2 20160707
            record.StartTime = advanceTime;
            advanceTime += 3f;
            record.EndTime = advanceTime;
            record.Tag = "STS";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//主吊具上升
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;
            record.StartPosition = qc.hoist1.ToString();
            qc.hoist1 = Default_QC_Hoist_Fore;
            record.EndPosition = (qc.hoist1).ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.yK1.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
            {
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = tempTime, EndTime = advanceTime, Tag = "海侧小车运行至中转平台，吊具下降抓取集装箱，并起升到一定高度" });
                tempTime = advanceTime;
            }

            record = new SimRecord();//主小车平移
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 2;
            record.StartPosition = qc.trolley1.ToString();
            qc.trolley1 = buffer.GetWorldColumnGauge(column);
            record.EndPosition = qc.trolley1.ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.zK1.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime; 
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//主吊具下降到船舶箱位（防摇定位）
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;
            record.StartPosition = qc.hoist1.ToString();
            qc.hoist1 = buffer.GetWorldPosition(bay, column).y + (tier - 80) / 2 * Default_Container_Height + Default_AntiSway;
            record.EndPosition = qc.hoist1.ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.yK1.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//主吊具下降到船舶箱位
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;
            record.StartPosition = qc.hoist1.ToString();
            qc.hoist1 = buffer.GetWorldPosition(bay, column).y + (tier - 80) / 2 * Default_Container_Height;
            record.EndPosition = qc.hoist1.ToString();
            record.StartTime = advanceTime;
            advanceTime += Default_AntiSway / Default_AntiSwag_Speed;//qc.yK1.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
            {
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = tempTime, EndTime = advanceTime, Tag = "海侧小车运行至船箱位上方，吊具下降落箱" });
                tempTime = advanceTime;
            }

            record = new SimRecord();//卸箱
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.DischargingType;
            record.EquipSub = 4;
            record.StartPosition = 0.ToString();//2 20160707
            record.EndPosition = (new SlotVessel(buffer, vessel, bay, column, tier)).ToString();
            record.StartTime = advanceTime;
            advanceTime += 3f;
            record.EndTime = advanceTime;
            record.Tag = DefinedType.VesselBuffer.ToString();
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//主吊具上升
            record.EquipCode = (int)DefinedType.STS;
            record.EquipID = qc.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;
            record.StartPosition = qc.hoist1.ToString();
            qc.hoist1 = Default_QC_Hoist_Fore;
            record.EndPosition = qc.hoist1.ToString();
            record.StartTime = advanceTime;
            advanceTime += qc.yK1.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
            {
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = tempTime, EndTime = advanceTime, Tag = "海侧小车吊具卸箱后，起升到一定高度" });
                //this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = advanceTime, EndTime = 100000, Tag = "装船任务完成！" });
            }

            qc.advanceTime = advanceTime;

            return advanceTime;
        }
        #endregion

        #region AGV
        public float AGV_STS2Buffer(float advanceTime, Vehicle agv, float x1, float z2, float endSpeed, bool isSubtitle = false)
        {
            SimRecord record;
            agv.advanceTime = advanceTime;

            record = new SimRecord();//沿岸边直走
            record.EquipCode = (int)DefinedType.AGV;
            record.EquipID = agv.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 1;//
            record.StartPosition = agv.x.ToString();
            agv.x = agv.x > x1 ? x1 + Default_AGV_SwerveRadius : x1 - Default_AGV_SwerveRadius;
            record.EndPosition = agv.x.ToString();
            record.StartTime = advanceTime;
            advanceTime += agv.xK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition), 0, Default_AGV_SwerveSpeed);
            record.EndTime = advanceTime;
            record.Tag = "0|" + Default_AGV_SwerveSpeed.ToString();
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//转弯进入缓冲区
            record.EquipCode = (int)DefinedType.AGV;
            record.EquipID = agv.equip.EquipID;
            record.EventType = SimRecord.SwerveType;
            record.EquipSub = 10;//
            record.StartPosition = "0|" + agv.x.ToString() + "|" + agv.z.ToString();
            agv.z = agv.z - Default_AGV_SwerveRadius;
            record.EndPosition = agv.x > x1 ? "-90|" + x1.ToString() + "|" + agv.z.ToString() : "90|" + x1.ToString() + "|" + agv.z.ToString();
            agv.x = x1;
            record.StartTime = advanceTime;
            advanceTime += Default_AGV_SwerveTime;
            record.EndTime = advanceTime;
            record.Tag = 1.ToString();
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//在缓冲区直行
            record.EquipCode = (int)DefinedType.AGV;
            record.EquipID = agv.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 2;//
            record.StartPosition = agv.z.ToString();
            agv.z = z2 ;
            record.EndPosition = agv.z.ToString();
            record.StartTime = advanceTime;
            advanceTime += Mathf.Abs(ParseHelper.TryAsSingle(record.StartPosition) - ParseHelper.TryAsSingle(record.EndPosition)) / Default_AGV_SwerveSpeed;
            record.EndTime = advanceTime;
            record.Tag = Default_AGV_SwerveSpeed.ToString() + "|" + endSpeed.ToString();
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = agv.advanceTime, EndTime = advanceTime, Tag = "AGV接箱后，经岸桥下车道转弯进入缓冲区" });

            agv.advanceTime = advanceTime;

            return advanceTime;
        }

        public float AGV_Buffer2STS(float advanceTime, Vehicle agv, float beginSpeed, float z1, float x1, bool isSubtitle = false)
        {
            SimRecord record;
            agv.advanceTime = advanceTime;

            record = new SimRecord();//出缓冲区，到转弯处
            record.EquipCode = (int)DefinedType.AGV;
            record.EquipID = agv.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 2;//
            record.StartPosition = agv.z.ToString();
            agv.z = z1 - Default_AGV_SwerveRadius; ;
            record.EndPosition = agv.z.ToString();
            record.StartTime = advanceTime;
            advanceTime += Mathf.Abs(ParseHelper.TryAsSingle(record.StartPosition) - ParseHelper.TryAsSingle(record.EndPosition)) / Default_AGV_SwerveSpeed;
            record.EndTime = advanceTime;
            record.Tag = beginSpeed.ToString() + "|" + Default_AGV_SwerveSpeed.ToString();
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//转弯进入岸桥下车道
            record.EquipCode = (int)DefinedType.AGV;
            record.EquipID = agv.equip.EquipID;
            record.EventType = SimRecord.SwerveType;
            record.EquipSub = 10;//

            if (agv.x + Default_AGV_SwerveRadius < x1)
            {
                record.StartPosition = "90|" + agv.x.ToString() + "|" + agv.z.ToString();
                agv.x = agv.x + Default_AGV_SwerveRadius;
                agv.z = z1;
                record.EndPosition = "180|" + agv.x.ToString() + "|" + agv.z.ToString();
            }
            else
            {
                record.StartPosition = "90|" + agv.x.ToString() + "|" + agv.z.ToString();
                agv.x = agv.x - Default_AGV_SwerveRadius;
                agv.z = z1;
                record.EndPosition = "0|" + agv.x.ToString() + "|" + agv.z.ToString();
            }
            record.StartTime = advanceTime;
            advanceTime = advanceTime + Default_AGV_SwerveTime;
            record.EndTime = advanceTime;
            record.Tag = 2.ToString();
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();
            record.EquipCode = (int)DefinedType.AGV;
            record.EquipID = agv.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 1;//
            record.StartPosition = agv.x.ToString();
            agv.x = x1;
            record.EndPosition = agv.x.ToString();
            record.StartTime = advanceTime;
            advanceTime += agv.xK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition), Default_AGV_SwerveSpeed, 0);
            record.EndTime = advanceTime;
            record.Tag = Default_AGV_SwerveSpeed.ToString() + "|0";
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = agv.advanceTime, EndTime = advanceTime, Tag = "AGV从缓冲区转弯进入岸桥下车道的作业位置" });

            agv.advanceTime = advanceTime;

            return advanceTime;
        }
        //负载AGV从交互区进入支架
        public float AGV_Mate_Laden(float advanceTime, Vehicle agv, TP mate, bool isSubtitle = false)//float z1, 
        {
            SimRecord record;
            float titleTime = advanceTime;
            float z1 = agv.z;

            record = new SimRecord();
            record.EquipCode = (int)DefinedType.AGV;//AGV之Lift顶升
            record.EquipID = agv.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;//
            record.StartPosition = 0.ToString();
            agv.y += 0.6f;
            record.EndPosition = agv.y.ToString();
            record.StartTime = advanceTime;
            advanceTime += 10f;
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            advanceTime += 2f;

            record = new SimRecord();
            record.EquipCode = (int)DefinedType.AGV;//AGV从堆场交互区进入装置下
            record.EquipID = agv.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 2;//
            record.StartPosition = agv.z.ToString();
            agv.z = mate.equip.transform.position.z;
            record.EndPosition = agv.z.ToString();
            record.StartTime = advanceTime;
            advanceTime += Mathf.Abs(ParseHelper.TryAsSingle(record.StartPosition) - ParseHelper.TryAsSingle(record.EndPosition)) / 1.5f;
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
            {
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "AGV顶升机构抬升集装箱后，进入支架下方" });
                titleTime = advanceTime;
            }

            record = new SimRecord();
            record.EquipCode = (int)DefinedType.AGV;//AGV之Lift下降
            record.EquipID = agv.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;//
            record.StartPosition = agv.y.ToString();
            agv.y = 0f;
            record.EndPosition = agv.y.ToString();
            record.StartTime = advanceTime;
            advanceTime += 10f;// Mathf.Abs(ParseHelper.TryAsSingle(record.StartPosition) - ParseHelper.TryAsSingle(record.EndPosition)) / 1.5f;
            record.EndTime = advanceTime;
            //record.Tag = beginSpeed.ToString() + "|0";
            this.recordMarshal.Enqueue(record);
                        
            record = new SimRecord();//LiftAGV落箱
            record.EquipCode = (int)DefinedType.Mate;//
            record.EquipID = mate.equip.EquipID;//
            record.EventType = SimRecord.LoadingType;
            record.EquipSub = 0;//
            record.StartPosition = new SlotEngaged(agv.equip.FindEngagedBuffer(3), 0).ToString();//2 20160707
            record.Tag = DefinedType.AGV.ToString();
            record.EndPosition = "0";//2 20160707
            record.StartTime = advanceTime - 5.5f;
            record.EndTime = advanceTime - 5.5f;
            this.recordMarshal.Enqueue(record);

            mate.advanceTime = advanceTime;

            record = new SimRecord();
            record.EquipCode = (int)DefinedType.AGV;//AGV从装置下出来
            record.EquipID = agv.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 2;//
            record.StartPosition = agv.z.ToString();
            agv.z = z1;
            record.EndPosition = agv.z.ToString();
            record.StartTime = advanceTime;
            advanceTime += Mathf.Abs(ParseHelper.TryAsSingle(record.StartPosition) - ParseHelper.TryAsSingle(record.EndPosition)) / 1.5f;
            record.EndTime = advanceTime;
            record.Tag = "0|1.5";
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "AGV顶升机构下降，将集装箱落在支架上，离开支架" });
            
            agv.advanceTime = advanceTime;

            return advanceTime;
        }
        //空载AGV从交互区进入支架
        public float AGV_Mate_Empty(float advanceTime, Vehicle agv, TP mate, float beginSpeed, float z1, bool isSubtitle = false)
        {
            SimRecord record; float titleTime = advanceTime;
            //AGV从堆场侧缓冲区进入装置下
            record = new SimRecord { EquipCode = (int)DefinedType.AGV, EquipID = agv.equip.EquipID, EventType = SimRecord.RunningType, EquipSub = 2, StartPosition = z1.ToString(), StartTime = advanceTime, Tag = beginSpeed.ToString() + "|0" };
            agv.z = mate.equip.transform.position.z;
            record.EndPosition = agv.z.ToString();
            advanceTime += Mathf.Abs(ParseHelper.TryAsSingle(record.StartPosition) - ParseHelper.TryAsSingle(record.EndPosition)) / 1.5f;
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            //if (isSubtitle)
            //{
            //    this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "AGV行走到AGV支架集装箱下方" });
            //    titleTime = advanceTime;
            //}
            //AGV顶升集装箱
            record = new SimRecord { EquipCode = (int)DefinedType.AGV, EquipID = agv.equip.EquipID, EventType = SimRecord.RunningType, EquipSub = 3, StartPosition = agv.y.ToString(), StartTime = advanceTime, Tag = "0|0" };
            agv.y += 0.6f;
            record.EndPosition = agv.y.ToString();
            advanceTime += 10f;//
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//LiftAGV取箱
            record.EquipCode = (int)DefinedType.AGV;//
            record.EquipID = agv.equip.EquipID;//
            record.EventType = SimRecord.LoadingType;
            record.EquipSub = 3;//
            record.StartPosition = new SlotEngaged(mate.equip.FindEngagedBuffer(0), 0).ToString();//2 20160707
            record.Tag = DefinedType.AGV.ToString();
            record.EndPosition = "0";//2 20160707
            record.StartTime = advanceTime - 4.5f;
            record.EndTime = advanceTime - 4.5f;
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
            {
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "AGV进入支架，顶升机构上升，完成接箱作业" });
                titleTime = advanceTime;
            }

            advanceTime += 2;

            record = new SimRecord();
            record.EquipCode = (int)DefinedType.AGV;//AGV从装置下出来
            record.EquipID = agv.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 2;//
            record.StartPosition = agv.z.ToString();
            agv.z = z1;
            record.EndPosition = agv.z.ToString();
            record.StartTime = advanceTime;
            advanceTime += Mathf.Abs(ParseHelper.TryAsSingle(record.StartPosition) - ParseHelper.TryAsSingle(record.EndPosition)) / 1.5f;
            record.EndTime = advanceTime;
            record.Tag = "0|0";
            this.recordMarshal.Enqueue(record);

            //if (isSubtitle)
            //{
            //    this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "AGV行走出支架" });
            //    titleTime = advanceTime;
            //}
            advanceTime += 2f;

            record = new SimRecord();
            record.EquipCode = (int)DefinedType.AGV;//AGV之Lift下降
            record.EquipID = agv.equip.EquipID;//
            record.EventType = SimRecord.KinematicType;
            record.EquipSub = 3;//
            record.StartPosition = agv.y.ToString();
            agv.y = 0f;
            record.EndPosition = agv.y.ToString();
            record.StartTime = advanceTime;
            advanceTime += 8f;// Mathf.Abs(ParseHelper.TryAsSingle(record.StartPosition) - ParseHelper.TryAsSingle(record.EndPosition)) / 1.5f;
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "AGV离开支架，顶升机构下降，恢复运行姿态" });
            advanceTime += 2;
            agv.advanceTime = advanceTime;

            return advanceTime;
        }

        public float AGV_Mate_LineOut(float advanceTime, Vehicle agv, float x1, float endSpeed)
        {
            SimRecord record;

            record = new SimRecord();//AGV出堆场，到堆场侧缓冲区
            record.EquipCode = (int)DefinedType.AGV;//
            record.EquipID = agv.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 1;//
            record.StartPosition = agv.x.ToString();
            agv.x = x1;
            record.EndPosition = agv.x.ToString();
            record.StartTime = advanceTime;
            advanceTime += Mathf.Abs(ParseHelper.TryAsSingle(record.StartPosition) - ParseHelper.TryAsSingle(record.EndPosition)) / 1.5f;
            record.EndTime = advanceTime;
            record.Tag = "0|" + endSpeed.ToString();
            this.recordMarshal.Enqueue(record);

            agv.advanceTime = advanceTime;

            return advanceTime;
        }

        public float AGV_Yard2Buffer(float advanceTime, Vehicle agv, float z1, float x2, float z3, float endSpeed, bool isSubtitle = false)
        {
            SimRecord record; float titleTime = advanceTime;
            bool isUp = x2 < agv.x ? true : false;

            record = new SimRecord();//直行出堆场
            record.EquipCode = (int)DefinedType.AGV;
            record.EquipID = agv.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 2;//
            record.StartTime = advanceTime;
            record.StartPosition = agv.z.ToString();
            agv.z = z1 - Default_AGV_SwerveRadius;
            record.EndPosition = agv.z.ToString();
            advanceTime += Mathf.Abs(ParseHelper.TryAsSingle(record.StartPosition) - ParseHelper.TryAsSingle(record.EndPosition)) / Default_AGV_SwerveSpeed;
            record.EndTime = advanceTime;
            record.Tag = "0|" + Default_AGV_SwerveSpeed.ToString();
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//转弯出堆场，进入高速车道
            record.EquipCode = (int)DefinedType.AGV;
            record.EquipID = agv.equip.EquipID;
            record.EventType = SimRecord.SwerveType;
            record.EquipSub = 10;//
            record.StartPosition = "90|" + agv.x.ToString() + "|" + agv.z.ToString();
            agv.z = z1;
            if (isUp)
            {
                agv.x = agv.x - Default_AGV_SwerveRadius;
                record.EndPosition = "0|" + agv.x.ToString() + "|" + agv.z.ToString();
            }
            else
            {
                agv.x = agv.x + Default_AGV_SwerveRadius;
                record.EndPosition = "180|" + agv.x.ToString() + "|" + agv.z.ToString();
            }
            record.StartTime = advanceTime;
            advanceTime += Default_AGV_SwerveTime;
            record.EndTime = advanceTime;
            record.Tag = 2.ToString();//X-Z
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//高速车道直行
            record.EquipCode = (int)DefinedType.AGV;
            record.EquipID = agv.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 1;//
            record.StartPosition = agv.x.ToString();
            if (isUp)
                agv.x = x2 + Default_AGV_SwerveRadius;
            else
                agv.x = x2 - Default_AGV_SwerveRadius;
            record.EndPosition = agv.x.ToString();
            record.StartTime = advanceTime;
            advanceTime += agv.xK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition), Default_AGV_SwerveSpeed, Default_AGV_SwerveSpeed);
            record.EndTime = advanceTime;
            record.Tag = Default_AGV_SwerveSpeed.ToString() + "|" + Default_AGV_SwerveSpeed.ToString();
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//转弯，进入缓冲区
            record.EquipCode = (int)DefinedType.AGV;
            record.EquipID = agv.equip.EquipID;
            record.EventType = SimRecord.SwerveType;
            record.EquipSub = 10;//
            if (isUp)
                record.StartPosition = "0|" + agv.x.ToString() + "|" + agv.z.ToString();
            else
                record.StartPosition = "180|" + agv.x.ToString() + "|" + agv.z.ToString();
            agv.x = x2;//agv.x - Default_AGV_SwerveRadius;            
            agv.z = agv.z + Default_AGV_SwerveRadius;
            record.EndPosition = "90|" + agv.x.ToString() + "|" + agv.z.ToString();
            record.StartTime = advanceTime;
            advanceTime += Default_AGV_SwerveTime;
            record.EndTime = advanceTime;
            record.Tag = 1.ToString();//Z-X
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//在缓冲区车道直行
            record.EquipCode = (int)DefinedType.AGV;
            record.EquipID = agv.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 2;//
            record.StartPosition = agv.z.ToString();
            agv.z = z3;
            record.EndPosition = agv.z.ToString();
            record.StartTime = advanceTime;
            advanceTime += Mathf.Abs(ParseHelper.TryAsSingle(record.StartPosition) - ParseHelper.TryAsSingle(record.EndPosition)) / Default_AGV_SwerveSpeed;
            record.EndTime = advanceTime;
            record.Tag = Default_AGV_SwerveSpeed.ToString() + "|" + endSpeed.ToString();
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "AGV经高速车道进入缓冲区" });

            agv.advanceTime = advanceTime;

            return advanceTime;
        }

        public float AGV_Buffer2Yard(float advanceTime, Vehicle agv, float beginSpeed, float z1, float x2, float z3, bool isSubtitle = false)
        {
            SimRecord record;
            agv.advanceTime = advanceTime;
            
            if (agv.z > z1 + Default_AGV_SwerveRadius)
            {
                record = new SimRecord();//在缓冲区直行
                record.EquipCode = (int)DefinedType.AGV;
                record.EquipID = agv.equip.EquipID;
                record.EventType = SimRecord.RunningType;
                record.EquipSub = 2;//
                record.StartTime = advanceTime;
                record.StartPosition = agv.z.ToString();
                agv.z = z1 + Default_AGV_SwerveRadius;
                record.EndPosition = agv.z.ToString();
                advanceTime += Mathf.Abs(ParseHelper.TryAsSingle(record.StartPosition) - ParseHelper.TryAsSingle(record.EndPosition)) / Default_AGV_SwerveSpeed;
                record.EndTime = advanceTime;
                record.Tag = beginSpeed.ToString() + "|" + Default_AGV_SwerveSpeed.ToString();
                this.recordMarshal.Enqueue(record);
            }

            record = new SimRecord();//转弯进入堆场侧高速车道
            record.EquipCode = (int)DefinedType.AGV;
            record.EquipID = agv.equip.EquipID;
            record.EventType = SimRecord.SwerveType;
            record.EquipSub = 10;//
            record.StartPosition = "90|" + agv.x.ToString() + "|" + agv.z.ToString();
            agv.z = z1;
            if (agv.x < x2 )
            {
                agv.x = agv.x + Default_AGV_SwerveRadius;
                record.EndPosition = "0|" + agv.x.ToString() + "|" + agv.z.ToString();
            }
            else
            {
                agv.x = agv.x - Default_AGV_SwerveRadius; // agv.x ;
                record.EndPosition = "180|" + agv.x.ToString() + "|" + agv.z.ToString();
            }
            record.StartTime = advanceTime;
            advanceTime += Default_AGV_SwerveTime;
            record.EndTime = advanceTime;
            record.Tag = 2.ToString();//X-Z
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//高速车道直行
            record.EquipCode = (int)DefinedType.AGV;
            record.EquipID = agv.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 1;//
            record.StartTime = advanceTime;
            record.StartPosition = agv.x.ToString();
            agv.x = agv.x < x2 ? x2 - Default_AGV_SwerveRadius : x2 + Default_AGV_SwerveRadius;
            record.EndPosition = agv.x.ToString();
            advanceTime += agv.xK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition), Default_AGV_SwerveSpeed, Default_AGV_SwerveSpeed);//, 0, 
            record.EndTime = advanceTime;
            record.Tag = beginSpeed.ToString() + "|" + Default_AGV_SwerveSpeed.ToString();
            this.recordMarshal.Enqueue(record);
            
            record = new SimRecord();//转弯进入堆场交互区
            record.EquipCode = (int)DefinedType.AGV;
            record.EquipID = agv.equip.EquipID;
            record.EventType = SimRecord.SwerveType;
            record.EquipSub = 10;//
            if (agv.x < x2)
            {
                record.StartPosition = "0|" + agv.x.ToString() + "|" + agv.z.ToString();
                agv.x = agv.x + Default_AGV_SwerveRadius;//           
            }
            else
            {
                record.StartPosition = "180|" + agv.x.ToString() + "|" + agv.z.ToString();
                agv.x = agv.x - Default_AGV_SwerveRadius;//          
            }
            agv.z = agv.z - Default_AGV_SwerveRadius;
            record.EndPosition = "90|" + agv.x.ToString() + "|" + agv.z.ToString();
            record.StartTime = advanceTime;
            advanceTime += Default_AGV_SwerveTime;
            record.EndTime = advanceTime;
            record.Tag = 1.ToString();//Z-X
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//堆场交互区直行
            record.EquipCode = (int)DefinedType.AGV;
            record.EquipID = agv.equip.EquipID;
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 2;//
            record.StartTime = advanceTime;
            record.StartPosition = agv.z.ToString();
            agv.z = z3;
            record.EndPosition = agv.z.ToString();
            advanceTime += Mathf.Abs(ParseHelper.TryAsSingle(record.StartPosition) - ParseHelper.TryAsSingle(record.EndPosition)) / Default_AGV_SwerveSpeed;
            record.EndTime = advanceTime;
            record.Tag = Default_AGV_SwerveSpeed.ToString() + "|" + Default_AGV_SwerveSpeed.ToString();
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = agv.advanceTime, EndTime = advanceTime, Tag = "AGV从缓冲区经高速车道进入海侧交换区" });

            agv.advanceTime = advanceTime;

            return advanceTime;
        }
        #endregion

        #region ASC
        public float ASC_Collected(float advanceTime, RMG rmg, ScriptBase equip, string blockName, int bay, int column, int tier, bool isSubtitle = false)
        {
            //新加保护判断，防止传入非法箱位
            BlockBuffer checkBlock = BlockManager.Instance.FindBlockBuffer(blockName);

            if (checkBlock == null)
            {
                Debug.LogWarning("ASC_Collected失败：未找到箱区 " + blockName);
                return advanceTime;
            }

            if (checkBlock.FindContainer(bay, column, tier) != null)
            {
                if (tier == 5)
                {
                    Debug.Log("无法放置：目标箱位第5层已存在集装箱，"
                        + string.Format("{0}{1:D3}{2:D2}{3:D2}", blockName, bay, column, tier));
                }
                else
                {
                    Debug.LogWarning("ASC_Collected目标箱位已占用，"
                        + string.Format("{0}{1:D3}{2:D2}{3:D2}", blockName, bay, column, tier));
                }

                return advanceTime;
            }

            SimRecord record;
            BlockBuffer block;
            float tempTime, spanTime, titleTime = advanceTime; float gravityOffset = -7f;
            block = BlockManager.Instance.FindBlockBuffer(blockName);
            Vehicle v = null;
            if (equip.GetType() == typeof(Vehicle))
                v = (Vehicle)equip;

            tempTime = advanceTime;

            record = new SimRecord();//大车运行到交互区
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 1;//
            record.StartPosition = rmg.gantryZ.ToString();
            if (v != null)
                rmg.gantryZ = v.z;
            else
                rmg.gantryZ = equip.equip.transform.position.z;
            record.EndPosition = rmg.gantryZ.ToString();
            record.StartTime = advanceTime;
            spanTime = rmg.gantryK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            advanceTime += spanTime;
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);
            //小车运行到重心位置
            record = new SimRecord { StartTime = tempTime, StartPosition = rmg.trolleyX.ToString(), EquipSub = 2, EventType = SimRecord.RunningType, EquipID = rmg.equip.EquipID, EquipCode = (int)DefinedType.ASC };
            rmg.trolleyX = rmg.equip.transform.position.x + gravityOffset;
            record.EndPosition = rmg.trolleyX.ToString();
            spanTime = rmg.trolleyK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            tempTime += spanTime;
            record.EndTime = tempTime;
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//小车运行到设备上方
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 2;//
            record.StartPosition = rmg.trolleyX.ToString();
            if (v != null)
                rmg.trolleyX = v.x;
            else
                rmg.trolleyX = equip.equip.transform.position.x;
            record.EndPosition = rmg.trolleyX.ToString();
            spanTime = rmg.trolleyK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.StartTime = Mathf.Max(advanceTime - spanTime, tempTime);
            advanceTime = record.StartTime + spanTime;
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
            {
                if (v == null)
                    this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "ASC大车、小车运行至支架上方" });
                else
                    this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "ASC大车、小车运行至外集卡上方" });
                titleTime = advanceTime;
            }

            record = new SimRecord();//吊具下降到集装箱上（防摇定位）
            record.EquipCode = (int)DefinedType.ASC;
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;//
            record.StartPosition = rmg.hoist.ToString();
            rmg.hoist = equip.equip.FindEngagedBuffer(0).GetWorldPosition(0).y + Default_Container_Height + Default_AntiSway;//GetLocalPosition //2 20160707
            record.EndPosition = rmg.hoist.ToString();
            record.StartTime = advanceTime;
            advanceTime += rmg.hoistK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//吊具下降到集装箱上
            record.EquipCode = (int)DefinedType.ASC;
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;//
            record.StartPosition = rmg.hoist.ToString();
            rmg.hoist = rmg.hoist - Default_AntiSway;//GetLocalPosition //2 20160707
            record.EndPosition = rmg.hoist.ToString();
            record.StartTime = advanceTime;
            advanceTime += Default_AntiSway / Default_AntiSwag_Speed;//rmg.yK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            //if (isSubtitle)
            //{
            //    this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "吊具下降到集装箱上方一定高度进行防摇定位，然后缓慢下降进行取箱" });
            //    titleTime = advanceTime;
            //}

            record = new SimRecord();//提箱
            record.EquipCode = (int)DefinedType.ASC;
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.LoadingType;
            record.EquipSub = 4;//
            record.StartPosition = new SlotEngaged(equip.equip.FindEngagedBuffer(0), 0).ToString();//2 20160707
            //record.Tag = DefinedType.ASC.ToString();
            record.EndPosition = "0";//2 20160707
            record.StartTime = advanceTime;
            advanceTime += 4;
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            equip.advanceTime = advanceTime + 3;
            
            record = new SimRecord();//吊具上升
            record.EquipCode = (int)DefinedType.ASC;
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;//
            record.StartPosition = rmg.hoist.ToString();
            rmg.hoist = Default_RMG_Hoist;
            record.EndPosition = rmg.hoist.ToString();
            record.StartTime = advanceTime;
            advanceTime += rmg.hoistK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
            {
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "ASC吊具下降并抓取集装箱，起升至一定高度" });
                titleTime = advanceTime;
            }

            tempTime = advanceTime;

            record = new SimRecord();//小车移动到中心位
            record.EquipCode = (int)DefinedType.ASC;
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 2;//
            record.StartPosition = rmg.trolleyX.ToString();
            rmg.trolleyX = rmg.equip.transform.position.x + gravityOffset;
            record.EndPosition = rmg.trolleyX.ToString();
            record.StartTime = tempTime;
            tempTime += rmg.trolleyK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = tempTime;
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//大车运行到箱位
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 1;//
            record.StartPosition = rmg.gantryZ.ToString();
            rmg.gantryZ = block.GetWorldPosition(bay, column).z;// block.GetWorldBayGauge(bay);
            record.EndPosition = rmg.gantryZ.ToString();
            record.StartTime = advanceTime;
            advanceTime += rmg.gantryK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//小车运行到箱位上方
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 2;//
            record.StartPosition = rmg.trolleyX.ToString();
            rmg.trolleyX = block.GetWorldPosition(bay, column).x;// 
            record.EndPosition = rmg.trolleyX.ToString();
            spanTime = rmg.trolleyK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = Mathf.Max(advanceTime, tempTime + spanTime);
            record.StartTime = record.EndTime - spanTime;
            this.recordMarshal.Enqueue(record);
            advanceTime = record.EndTime + 3f;

            if (isSubtitle)
            {
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "ASC大车、小车运行至堆场作业箱位上方" });
                titleTime = advanceTime;
            }

            record = new SimRecord();//吊具下降到箱位上方（防摇定位）
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;//
            record.StartPosition = rmg.hoist.ToString();
            if (tier > 1)
                rmg.hoist = Default_Container_Height * tier + Default_AntiSway;//+block.FindContainer(bay, column, tier - 1).GetCurrentTop
            else
                rmg.hoist = Default_Container_Height + Default_AntiSway;
            record.EndPosition = rmg.hoist.ToString();
            record.StartTime = advanceTime;
            advanceTime += rmg.hoistK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//吊具下降到箱位上方
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;//
            record.StartPosition = rmg.hoist.ToString();
            rmg.hoist = rmg.hoist - Default_AntiSway;
            record.EndPosition = rmg.hoist.ToString();
            record.StartTime = advanceTime;
            advanceTime += Default_AntiSway / Default_AntiSwag_Speed;//rmg.yK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            //if (isSubtitle)
            //{
            //    this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "吊具下降到作业箱位上方进行卸箱" });
            //    titleTime = advanceTime;
            //}

            record = new SimRecord();//卸箱
            record.EquipCode = (int)DefinedType.ASC;
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.DischargingType;
            record.EquipSub = 4;//
            record.StartPosition = 0.ToString();// new EBufferEngaged(rmg.FindEngagedBuffer(4), 2).ToString();//2 20160707
            record.Tag = DefinedType.ASC.ToString();
            record.EndPosition = BlockBuffer.BuildYardCode(blockName, bay, column, tier);
            record.StartTime = advanceTime;
            advanceTime += 4;
            record.EndTime = advanceTime;
            record.Tag = DefinedType.BlockBuffer.ToString();
            this.recordMarshal.Enqueue(record);

            tempTime = advanceTime;

            record = new SimRecord();//吊具上升
            record.EquipCode = (int)DefinedType.ASC;
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;//
            record.StartPosition = rmg.hoist.ToString();
            rmg.hoist = Default_RMG_Hoist;
            record.EndPosition = rmg.hoist.ToString();
            record.StartTime = advanceTime;
            advanceTime += rmg.hoistK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//小车运行到中心位
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 2;//
            record.StartPosition = rmg.trolleyX.ToString();//
            rmg.trolleyX = rmg.equip.transform.position.x + gravityOffset;
            record.EndPosition = rmg.trolleyX.ToString();
            spanTime = rmg.trolleyK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            advanceTime = Mathf.Max(advanceTime, tempTime + spanTime) + 5;
            record.StartTime = advanceTime - spanTime;
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "ASC吊具下降并落箱至堆场，小车、吊具回到初始位置" });

            rmg.advanceTime = advanceTime;

            return advanceTime;
        }

        public float ASC_Out(float advanceTime, RMG rmg, ScriptBase equip, string blockName, int bay, int column, int tier, bool isSubtitle = false)
        {
            BlockBuffer block;
            Container container;
            block = BlockManager.Instance.FindBlockBuffer(blockName);
            container = block.FindContainer(bay, column, tier);

            return this.ASC_Out(advanceTime, rmg, equip, container, isSubtitle);
        }

        public float ASC_Out(float advanceTime, RMG rmg, ScriptBase equip, Container container, bool isSubtitle = false)
        {
            SimRecord record;
            float tempTime, spanTime, titleTime = advanceTime; float gravityOffset = -7;
            Vehicle v = null;
            if (equip.GetType() == typeof(Vehicle))
                v = (Vehicle)equip;

            tempTime = advanceTime;

            record = new SimRecord();//大车到箱位
            record.EquipCode = (int)DefinedType.ASC;
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 1;//
            record.StartPosition = rmg.gantryZ.ToString();
            rmg.gantryZ = container.transform.position.z;
            record.EndPosition = rmg.gantryZ.ToString();
            record.StartTime = advanceTime;
            advanceTime += rmg.gantryK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            //小车运行到重心位置
            record = new SimRecord { StartTime = tempTime, StartPosition = rmg.trolleyX.ToString(), EquipSub = 2, EventType = SimRecord.RunningType, EquipID = rmg.equip.EquipID, EquipCode = (int)DefinedType.ASC };
            rmg.trolleyX = rmg.equip.transform.position.x + gravityOffset;
            record.EndPosition = rmg.trolleyX.ToString();
            spanTime = rmg.trolleyK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            tempTime += spanTime;
            record.EndTime = tempTime;
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//小车到箱位
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.KinematicType;
            record.EquipSub = 2;//
            record.StartPosition = rmg.trolleyX.ToString();//
            rmg.trolleyX = container.transform.position.x;//
            record.EndPosition = rmg.trolleyX.ToString();
            spanTime = rmg.trolleyK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            advanceTime = Mathf.Max(advanceTime, tempTime + spanTime);
            record.StartTime = advanceTime - spanTime;
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
            {
                if (v == null)
                    this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "ASC大车、小车运行至堆场集装箱上方，同时AGV行驶至海侧交换区" });
                else
                    this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "ASC大车、小车运行至堆场集装箱上方" });
                titleTime = advanceTime;
            }

            advanceTime += 5;

            record = new SimRecord();//吊具到箱位（防摇定位）
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.KinematicType;
            record.EquipSub = 3;//
            record.StartPosition = rmg.hoist.ToString();//
            rmg.hoist = container.GetCurrentTop + Default_AntiSway;//
            record.EndPosition = rmg.hoist.ToString();
            record.StartTime = advanceTime;
            advanceTime += rmg.hoistK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//吊具到箱位
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.KinematicType;
            record.EquipSub = 3;//
            record.StartPosition = rmg.hoist.ToString();//
            rmg.hoist = container.GetCurrentTop;//
            record.EndPosition = rmg.hoist.ToString();
            record.StartTime = advanceTime;
            advanceTime += Default_AntiSway / Default_AntiSwag_Speed; //rmg.yK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//取箱
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.LoadingType;
            record.EquipSub = 4;//
            record.StartPosition = container.SlotFlag.ToString();
            record.Tag = DefinedType.BlockBuffer.ToString();
            record.EndPosition = "0";//2 20160707
            record.StartTime = advanceTime;
            advanceTime += 5;
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//吊具上升
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.KinematicType;
            record.EquipSub = 3;//
            record.StartPosition = rmg.hoist.ToString();
            rmg.hoist = Default_RMG_Hoist;
            record.EndPosition = rmg.hoist.ToString();
            record.StartTime = advanceTime;
            advanceTime += rmg.hoistK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
            {
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "ASC吊具下降并抓取集装箱，并起升到安全高度" });
                titleTime = advanceTime;
            }

            tempTime = advanceTime;

            record = new SimRecord();//大车移动
            record.EquipCode = (int)DefinedType.ASC;
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.KinematicType;
            record.EquipSub = 1;//
            record.StartPosition = rmg.gantryZ.ToString();
            if (v != null)
                rmg.gantryZ = v.z;
            else
                rmg.gantryZ = equip.equip.transform.position.z;//
            record.EndPosition = rmg.gantryZ.ToString();
            record.StartTime = advanceTime;
            advanceTime += rmg.gantryK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//小车到中心位
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.KinematicType;
            record.EquipSub = 2;//
            record.StartPosition = rmg.trolleyX.ToString();//
            rmg.trolleyX = rmg.equip.transform.position.x + gravityOffset;
            record.EndPosition = rmg.trolleyX.ToString();
            spanTime = rmg.trolleyK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.StartTime = tempTime;
            tempTime += spanTime;
            record.EndTime = tempTime;
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//小车到目标位
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.KinematicType;
            record.EquipSub = 2;//
            record.StartPosition = rmg.trolleyX.ToString();//
            if (v != null)
                rmg.trolleyX = v.x;
            else
                rmg.trolleyX = equip.equip.transform.position.x;
            record.EndPosition = rmg.trolleyX.ToString();
            spanTime = rmg.trolleyK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            advanceTime = Mathf.Max(advanceTime, tempTime + spanTime);
            record.StartTime = advanceTime - spanTime;
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
            {
                if (v == null)
                    this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "ASC大车、小车运行至海侧交换区支架上方" });
                else
                    this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "ASC大车、小车运行至陆侧交换区外集卡上方" });
                titleTime = advanceTime;
            }

            advanceTime += 6;

            record = new SimRecord();//吊具下降（防摇定位）
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.KinematicType;
            record.EquipSub = 3;//
            record.StartPosition = rmg.hoist.ToString();
            rmg.hoist = equip.equip.FindEngagedBuffer(0).GetWorldPosition(2).y + container.HeightMetre + Default_AntiSway;//
            record.EndPosition = rmg.hoist.ToString(); //block.FindContainer(bay, column, tier).GetCurrentTop;
            record.StartTime = advanceTime;
            advanceTime += rmg.hoistK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//吊具下降
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.KinematicType;
            record.EquipSub = 3;//
            record.StartPosition = rmg.hoist.ToString();
            rmg.hoist = equip.equip.FindEngagedBuffer(0).GetWorldPosition(2).y + container.HeightMetre;//
            record.EndPosition = rmg.hoist.ToString(); //block.FindContainer(bay, column, tier).GetCurrentTop;
            record.StartTime = advanceTime;
            advanceTime += Default_AntiSway / Default_AntiSwag_Speed; //rmg.yK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//卸箱
            record.EquipCode = (int)equip.equip.EquipmentType;//
            record.EquipID = equip.equip.EquipID;//
            record.EventType = SimRecord.LoadingType;
            record.EquipSub = 0;//
            record.StartPosition = new SlotEngaged(rmg.equip.FindEngagedBuffer(4), 0).ToString();//2 20160707
            record.EndPosition = "0";//2 20160707
            record.StartTime = advanceTime;
            advanceTime += 6;
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            tempTime = advanceTime;
            equip.advanceTime = advanceTime + 5;

            record = new SimRecord();//吊具上升
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.KinematicType;
            record.EquipSub = 3;//
            record.StartPosition = rmg.hoist.ToString();
            rmg.hoist = Default_RMG_Hoist;
            record.EndPosition = rmg.hoist.ToString(); //block.FindContainer(bay, column, tier).GetCurrentTop;
            record.StartTime = advanceTime;
            advanceTime += rmg.hoistK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//小车到重心位
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.KinematicType;
            record.EquipSub = 2;//
            record.StartPosition = rmg.trolleyX.ToString();//
            rmg.trolleyX = rmg.equip.transform.position.x + gravityOffset;
            record.EndPosition = rmg.trolleyX.ToString();
            spanTime = rmg.trolleyK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            tempTime += 6;
            record.StartTime = tempTime; 
            tempTime += spanTime;
            record.EndTime = tempTime;//record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            advanceTime = Mathf.Max(advanceTime, tempTime); 

            if (isSubtitle)
            {
                if (v == null)
                    this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "ASC吊具下降，卸箱至支架上" });
                else
                    this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "ASC吊具下降，卸箱至外集卡上" });
                titleTime = advanceTime;
            }

            //if (isSubtitle)
            //{
            //    this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "吊具下降进行卸箱,之后提升到一定的安全高度" });
            //    //this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = advanceTime, EndTime = 100000, Tag = "ASC发箱任务完成！" });
            //}

            rmg.advanceTime = advanceTime;

            return advanceTime;
        }

        public float ASC_ReOrder(float advanceTime, RMG rmg, string blockName, int bay1, int column1, int tier1, int bay2, int column2, int tier2, bool isSubtitle = false)
        {
            SimRecord record;
            BlockBuffer block = BlockManager.Instance.FindBlockBuffer(blockName); float gravityOffset = -7;
            float tempTime, spanTime, titleTime = advanceTime;
            Container container;
            container = block.FindContainer(bay1, column1, tier1);

            tempTime = advanceTime;//大车移动到起始位
            record = new SimRecord();
            record.EquipCode = (int)DefinedType.ASC;
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 1;//
            record.StartPosition = rmg.gantryZ.ToString();
            rmg.gantryZ = block.GetWorldPosition(bay1, column1).z;// block.GetWorldBayGauge(bay1);//
            record.EndPosition = rmg.gantryZ.ToString();
            record.StartTime = advanceTime;
            advanceTime += rmg.gantryK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);
            //小车运行到重心位置
            record = new SimRecord { StartTime = tempTime, StartPosition = rmg.trolleyX.ToString(), EquipSub = 2, EventType = SimRecord.RunningType, EquipID = rmg.equip.EquipID, EquipCode = (int)DefinedType.ASC };
            rmg.trolleyX = rmg.equip.transform.position.x + gravityOffset;
            record.EndPosition = rmg.trolleyX.ToString();
            spanTime = rmg.trolleyK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            tempTime += spanTime;
            record.EndTime = tempTime;
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//小车移动到起始位
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 2;//
            record.StartPosition = rmg.trolleyX.ToString();//
            rmg.trolleyX = block.GetWorldPosition(bay1, column1).x;// block.GetWorldColumnGauge(column1);//
            record.EndPosition = rmg.trolleyX.ToString();
            spanTime = rmg.trolleyK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            advanceTime = Mathf.Max(advanceTime, tempTime + spanTime);
            record.StartTime = advanceTime - spanTime;
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            advanceTime += 2;

            if (isSubtitle)
            {
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "ASC大车、小车运行至起始作业位置上方" });
                titleTime = advanceTime;
            }

            record = new SimRecord();//吊具下降到起始位（防摇定位）
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;//
            record.StartPosition = rmg.hoist.ToString();//
            rmg.hoist = container.GetCurrentTop + Default_AntiSway;//
            record.EndPosition = rmg.hoist.ToString();
            record.StartTime = advanceTime;
            advanceTime += rmg.trolleyK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//吊具下降到集装箱上（抓箱）
            record.EquipCode = (int)DefinedType.ASC;
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;//
            record.StartPosition = rmg.hoist.ToString();
            rmg.hoist = rmg.hoist - Default_AntiSway;//2 20160707
            record.EndPosition = rmg.hoist.ToString();
            record.StartTime = advanceTime;
            advanceTime += Default_AntiSway / Default_AntiSwag_Speed;//rmg.yK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//取箱
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.LoadingType;
            record.EquipSub = 4;//
            record.StartPosition = container.SlotFlag.ToString();
            record.Tag = DefinedType.BlockBuffer.ToString();
            record.EndPosition = "0";//2 20160707
            record.StartTime = advanceTime;
            advanceTime += 2;
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//吊具起升
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;//
            record.StartPosition = rmg.hoist.ToString();
            rmg.hoist = Default_RMG_Hoist;
            record.EndPosition = rmg.hoist.ToString();
            record.StartTime = advanceTime;
            advanceTime += rmg.hoistK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            if (isSubtitle)
            {
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "ASC吊具下降并抓取集装箱，并起升到一定高度" });
                titleTime = advanceTime;
            }

            tempTime = advanceTime;
            record = new SimRecord();//大车平移到目标位
            record.EquipCode = (int)DefinedType.ASC;
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 1;//
            record.StartPosition = rmg.gantryZ.ToString();
            rmg.gantryZ = block.GetWorldPosition(bay2, column2).z;// block.GetWorldBayGauge(bay2);
            record.EndPosition = rmg.gantryZ.ToString();
            record.StartTime = advanceTime;
            advanceTime += rmg.gantryK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//小车平移到重心位
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 2;//
            record.StartPosition = rmg.trolleyX.ToString();//
            rmg.trolleyX = rmg.equip.transform.position.x + gravityOffset;//重心位
            record.EndPosition = rmg.trolleyX.ToString();
            spanTime = rmg.trolleyK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.StartTime = tempTime;
            tempTime += spanTime;
            record.EndTime = tempTime;
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//小车平移到目标位
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 2;//
            record.StartPosition = rmg.trolleyX.ToString();//
            rmg.trolleyX = block.GetWorldPosition(bay2, column2).x;// block.GetWorldColumnGauge(column2);
            record.EndPosition = rmg.trolleyX.ToString();
            spanTime = rmg.trolleyK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            advanceTime = Mathf.Max(advanceTime, tempTime + spanTime);
            record.StartTime = advanceTime - spanTime;
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            advanceTime += 2;

            if (isSubtitle)
            {
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "ASC大车、小车带箱运行至目标作业位置上方" });
                titleTime = advanceTime;
            }

            record = new SimRecord();//吊具下降到目标位
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;//
            record.StartPosition = rmg.hoist.ToString();
            if (tier2 > 1)
                rmg.hoist = tier2 * Default_Container_Height + Default_AntiSway;// block.FindContainer(bay2, column2, tier2 - 1).GetCurrentTop + container.HeightMetre + Default_AntiSway;
            else
                rmg.hoist = container.HeightMetre + Default_AntiSway;
            record.EndPosition = rmg.hoist.ToString(); //block.FindContainer(bay, column, tier).GetCurrentTop;
            record.StartTime = advanceTime;
            advanceTime += rmg.hoistK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//吊具下降到集装箱上（落箱）
            record.EquipCode = (int)DefinedType.ASC;
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;//
            record.StartPosition = rmg.hoist.ToString();
            rmg.hoist = rmg.hoist - Default_AntiSway;//2 20160707
            record.EndPosition = rmg.hoist.ToString();
            record.StartTime = advanceTime;
            advanceTime += Default_AntiSway / Default_AntiSwag_Speed;//rmg.yK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//卸箱
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.DischargingType;
            record.EquipSub = 4;//
            record.StartPosition = 0.ToString();// new EBufferEngaged(rmg.FindEngagedBuffer(4), 2).ToString();//2 20160707
            record.Tag = DefinedType.ASC.ToString();
            record.EndPosition = BlockBuffer.BuildYardCode(blockName, bay2, column2, tier2);
            record.StartTime = advanceTime;
            advanceTime += 4;
            record.EndTime = advanceTime;
            record.Tag = DefinedType.BlockBuffer.ToString();
            this.recordMarshal.Enqueue(record);
            
            tempTime = advanceTime;

            record = new SimRecord();//吊具上升
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 3;//
            record.StartPosition = rmg.hoist.ToString();
            rmg.hoist = Default_RMG_Hoist;
            record.EndPosition = rmg.hoist.ToString(); //block.FindContainer(bay, column, tier).GetCurrentTop;
            record.StartTime = advanceTime;
            advanceTime += rmg.hoistK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            record = new SimRecord();//小车到重心位
            record.EquipCode = (int)DefinedType.ASC;//
            record.EquipID = rmg.equip.EquipID;//
            record.EventType = SimRecord.RunningType;
            record.EquipSub = 2;//
            record.StartPosition = rmg.trolleyX.ToString();//
            rmg.trolleyX = rmg.equip.transform.position.x + gravityOffset;//重心位
            record.EndPosition = rmg.trolleyX.ToString();
            //spanTime = rmg.trolleyK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            //advanceTime = Mathf.Max(advanceTime, tempTime + spanTime);
            //record.StartTime = advanceTime - spanTime;
            record.StartTime = advanceTime - 5;
            advanceTime += rmg.trolleyK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            this.recordMarshal.Enqueue(record);

            rmg.advanceTime = advanceTime;

            if (isSubtitle)
            {
                this.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = titleTime, EndTime = advanceTime, Tag = "ASC吊具下降并落箱至堆场，小车、吊具回到初始位置" });
            }

            return advanceTime;
        }
        #endregion

        #region Truck
        static float TruckSpeed = 6;
        public float Truck_OutYard(float advanceTime, Vehicle car, float z1)//, float z2, float x3
        {
            SimRecord record;

            record = new SimRecord { StartPosition = car.z.ToString(), EquipSub = 2, StartTime = advanceTime, EquipID = car.equip.EquipID, EventType = SimRecord.RunningType };
            car.z = z1 + Default_AGV_SwerveRadius;record.EndPosition = car.z.ToString();
            advanceTime += Mathf.Abs(ParseHelper.TryAsSingle(record.StartPosition) - ParseHelper.TryAsSingle(record.EndPosition)) / 2f; record.EndTime = advanceTime;            
            record.Tag = "0|" + Default_Truck_SwerveSpeed.ToString();
            this.recordMarshal.Enqueue(record);

            record = new SimRecord { EquipSub = 10, EventType = SimRecord.SwerveType, StartTime = advanceTime, EquipID = car.equip.EquipID };
            record.StartPosition = string.Format("{0}|{1}|{2}", 90, car.x, car.z);
            car.x = car.x + Default_AGV_SwerveRadius; car.z = z1; car.yRot = 0;
            record.EndPosition = string.Format("{0}|{1}|{2}", 0, car.x, car.z);
            advanceTime += Default_AGV_SwerveTime; record.EndTime = advanceTime;
            record.Tag = "2";
            this.recordMarshal.Enqueue(record);

            car.advanceTime = advanceTime;

            return advanceTime;
        }

        public float Truck_InYard(float advanceTime, Vehicle car, float x1, float z2)
        {
            SimRecord record;

            record = new SimRecord { StartPosition = car.x.ToString(), EquipSub = 1, EventType = SimRecord.RunningType,StartTime = advanceTime, EquipID = car.equip.EquipID };
            car.x = x1 + Default_AGV_SwerveRadius; record.EndPosition = car.x.ToString();
            advanceTime += car.xK.CalculateTime(ParseHelper.TryAsSingle(record.StartPosition), ParseHelper.TryAsSingle(record.EndPosition));
            record.EndTime = advanceTime;
            record.Tag = "0|" + Default_Truck_SwerveSpeed.ToString();
            this.recordMarshal.Enqueue(record);

            record = new SimRecord {  EquipSub = 10, EventType = SimRecord.SwerveType, StartTime = advanceTime, EquipID = car.equip.EquipID };
            record.StartPosition = string.Format("{0}|{1}|{2}", 0, car.x, car.z);
            car.x = x1; car.z += Default_AGV_SwerveRadius; car.yRot = 90f;
            record.EndPosition = string.Format("{0}|{1}|{2}", 90, car.x, car.z);
            advanceTime += Default_AGV_SwerveTime;record.EndTime = advanceTime;            
            record.Tag = "1";
            this.recordMarshal.Enqueue(record);

            record = new SimRecord { StartPosition = car.z.ToString(), EquipSub = 2, EventType = SimRecord.RunningType,StartTime = advanceTime, EquipID = car.equip.EquipID };
            car.z = z2; record.EndPosition = car.z.ToString();
            advanceTime += Mathf.Abs(ParseHelper.TryAsSingle(record.StartPosition) - ParseHelper.TryAsSingle(record.EndPosition)) / 1.5f;
            record.EndTime = advanceTime;
            record.Tag = Default_Truck_SwerveSpeed.ToString() + "|0";
            this.recordMarshal.Enqueue(record);

            car.advanceTime = advanceTime;

            return advanceTime;
        }

        public float Truck_Seg(float advanceTime, Vehicle car, float speedRatio, float[] x, float[] z, float[] y)
        {
            SimRecord record; float spanTime = 0;

            for (int i = 0; i < x.Length; i++)
            {
                //if (car.yRot != y[i])
                //    spanTime = advanceTime + 1.2f *  Mathf.Sqrt(Mathf.Pow(x[i] - car.x, 2) + Mathf.Pow(z[i] - car.z, 2)) / TruckSpeed / speedRatio;
                //else
                    spanTime = advanceTime + Mathf.Sqrt(Mathf.Pow(x[i] - car.x, 2) + Mathf.Pow(z[i] - car.z, 2)) / TruckSpeed / speedRatio;
                if (car.yRot != y[i])
                {
                    record = new SimRecord { EquipSub = 0, StartPosition = car.yRot.ToString(), EndPosition = y[i].ToString(), Tag = "6|6", EndTime = spanTime, StartTime = advanceTime, EventType = SimRecord.KinematicType, EquipID = car.equip.EquipID };
                    this.recordMarshal.Enqueue(record); car.yRot = y[i];                    
                }
                if (car.x != x[i])
                {
                    record = new SimRecord { EquipSub = 1, StartPosition = car.x.ToString(), EndPosition = x[i].ToString(), Tag = "6|6", EndTime = spanTime, StartTime = advanceTime, EventType = SimRecord.RunningType, EquipID = car.equip.EquipID };
                    this.recordMarshal.Enqueue(record); car.x = x[i];                   
                }
                if (car.z != z[i])
                {
                    record = new SimRecord { EquipSub = 2, StartPosition = car.z.ToString(), EndPosition = z[i].ToString(), Tag = "6|6", EndTime = spanTime, StartTime = advanceTime, EventType = SimRecord.RunningType, EquipID = car.equip.EquipID };
                    this.recordMarshal.Enqueue(record); car.z = z[i]; 
                }
                advanceTime = spanTime;
            }
            car.advanceTime = advanceTime;
            return advanceTime;
        }
        #endregion

    }

    public class ScriptBase
    {
        public EquipBase equip;

        public float advanceTime;
    }

    public class Vehicle : ScriptBase
    {
        public float x;
        public float yRot;
        public float z;
        public float y;

        public SimRun xK;//KinematicRunning
        public SimRun zK;//KinematicRunning
        public SimRun yK;//
        public SimRotate yRotK;//KinematicRotate
    }

    public class RMG : ScriptBase
    {
        public float gantryX;
        public float gantryZ;
        public float trolleyX;
        public float trolleyZ;
        public float hoist;
        public float spreader;

        public SimRun gantryK;//KinematicTranslate
        public SimRun hoistK;//KinematicTranslate
        public SimRun trolleyK;//KinematicTranslate
    }

    public class TP : ScriptBase
    {
        //public KinematicAnimation a;
        //public float currentValue;
    }

    public class QC: ScriptBase
    {
        public float gantry;
        public float trolley1;
        public float hoist1;
        //public float spreader1;
        public float trolley2;
        public float hoist2;
        //public float spreader2;
        public SimRun xK;//KinematicTranslate
        public SimRun zK1;//KinematicTranslate
        public SimRun yK1;//KinematicTranslate
        public SimRun zK2;//KinematicTranslate
        public SimRun yK2;//KinematicTranslate
    }

    //public class TRMG : ScriptBase
    //{
    //    public float gantry;
    //    public float trolley;
    //    public float trolleyRotation;
    //    public float hoist;
    //    public float spreader;

    //    public KinematicTranslate xK;
    //    public KinematicTranslate yK;
    //    public KinematicTranslate zK;
    //    public KinematicRotate yR;
    //}
}