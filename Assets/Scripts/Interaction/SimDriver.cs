using UnityEngine;
using System;
using System.Collections;
using ZPMC.VR.Unity;

public class SimDriver : UserDriver
{
    SequenceList<SimRecord> recordMarshal;
    Simulator simulator;
    
    protected override void Awake()
    {
        base.Awake();
        this.recordMarshal = EventScripter.GetGlobalInstance().recordMarshal;
        if (this.simulator == null)
        {
            this.simulator = GetComponent<Simulator>();
            if (!this.simulator)
                this.simulator = gameObject.AddComponent<Simulator>();

            //DriverSetting setting = (DriverSetting)FileHelper.DeserializeObject(FileHelper.GetDataPath() + "/" + "DriverSetting.xml", typeof(DriverSetting));
            //setting.BuildDriver(this.simulator);
        }
    }

    protected override void BindInstance()
    {
        UserDriver.Instance = this;
    }

    public override void TryStopKinematic(float currentTime)
    {
        Simulator.Instance.TryStopKinematic(currentTime);
    }

    public override void ReStart()
    {
        Simulator.Instance.StopAllKinematic();//EquipManager.Instance.StopAllKinematic();
        recordMarshal.Clear();
    }

    // Update is called once per frame
    void Update()
    {
        if (this.IsPause)
            return;
        while (this.recordMarshal.Count > 0 && this.recordMarshal.Peek().StartTime <= TimeHelper.Instance.RectifiedTime)
        {
            SimRecord record = this.recordMarshal.Dequeue();
            try
            {
                switch (record.EventType)
                {
                    case SimRecord.RunningType:
                    case SimRecord.KinematicType:
                    case SimRecord.SwerveType:
                        {
                            SimBase controller = this.simulator.FindSimulator(record.EquipID, record.EquipSub);
                            controller.InitialParse(record);
                            break;
                        }
                    case SimRecord.LoadingType:
                        {
                            EquipBase equip = EquipManager.Instance.FindEquip(record.EquipID);//record.EquipCode, 
                            Container container = this.LoadContainer(record);

                            equip.AddContainer(record.EquipSub, ParseHelper.TryAsInteger(record.EndPosition), container);
                            //IEnumerator corountine = StartCoroutine(corountine);
                            break;
                        }
                    case SimRecord.DischargingType:
                        {
                            EquipBase equip = EquipManager.Instance.FindEquip(record.EquipID);//record.EquipCode, 
                            Container container = equip.RemoveContainer(record.EquipSub, ParseHelper.TryAsInteger(record.StartPosition));
                            this.DischargeContainer(record, container);
                            break;
                        }
                    case SimRecord.VisibleType:
                        {
                            Truck car = EquipManager.Instance.FindEquipTruck(record.EquipID);
                            if (!car)
                            {
                                car = EquipManager.Instance.SpawnTruck(record.EquipID);
                            }
                            else
                            {
                                GameObject obj = EquipSpawner.Instance.GetPrefab("Truck");
                                car.transform.rotation = obj.transform.rotation;
                            }
                            bool isVisual = ParseHelper.TryAsBoolean(record.EndPosition);
                            car.SetVisible(isVisual);
                            if (isVisual && !string.IsNullOrEmpty(record.StartPosition))
                            {
                                string[] subStr = record.StartPosition.Split('|');
                                float x, y, z;
                                y = ParseHelper.TryAsSingle(subStr[0]);
                                x = ParseHelper.TryAsSingle(subStr[1]);
                                z = ParseHelper.TryAsSingle(subStr[2]);

                                car.transform.Rotate(0, y, 0);
                                car.transform.position = new Vector3(x, 0, z);
                                if (!string.IsNullOrEmpty(record.Tag))
                                {
                                    subStr = record.Tag.Split(';');
                                    for (int i = 0, k = subStr.Length; i < k; i++)
                                    {
                                        string[] tStr = subStr[i].Split('|');
                                        //int owner = ContainerSpawner.Instance.GetRandomOwnerIndex();
                                        int bay = ParseHelper.TryAsInteger(tStr[0]);
                                        //float length = ParseHelper.TryAsSingle(tStr[2]);
                                        //float hight = ParseHelper.TryAsSingle(tStr[3]);
                                        Container container = this.CollectingContainer[tStr[1]];//= ContainerSpawner.Instance.SpawnContainer(owner, length, hight);
                                        //container.name = tStr[1];
                                        container.gameObject.SetActive(true);
                                        container.transform.Rotate(0, y, 0, Space.World);
                                        car.AddContainer(record.EquipSub, bay, container);
                                    }
                                }
                            }
                            break;
                        }
                    case SimRecord.TitleVisible:
                        {
                            Subtitle.Instance.SetSubtilte(record.Tag, record.StartTime, record.EndTime);
                            break;
                        }
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Parse Record Error.\n" + record.ToString() + "\n" + ex.Message);
            }
        }
    }

    private Container LoadContainer(SimRecord record)
    {
        try
        {
            if (record.Tag == DefinedType.BlockBuffer.ToString()) //subStr.Length < 2堆场
            {
                return BlockManager.Instance.RemoveContainer(record.StartPosition);
            }
            string[] subStr = record.StartPosition.Split('|');
            if (record.Tag == DefinedType.VesselBuffer.ToString())
            {
                return VesselManager.GlobalInstance().RemoveContainer(subStr[0], subStr[1]);
            }
            else
            {
                int equipID = ParseHelper.TryAsInteger(subStr[1]);
                int bufferID = ParseHelper.TryAsInteger(subStr[2]);
                int bay = ParseHelper.TryAsInteger(subStr[3]);
                EquipBase equip = EquipManager.Instance.FindEquip(subStr[0], equipID);
                return equip.FindEngagedBuffer(bufferID).RemoveContainer(bay);
            }
        }
        catch (System.Exception ex)
        {
            throw new System.Exception("Loading container error! \n" + record.ToString() + ex.Message);
        }
    }

    private void DischargeContainer(SimRecord record, Container container)
    {
        try
        {
            if (record.Tag == DefinedType.BlockBuffer.ToString()) //subStr.Length < 2堆场
            {
                BlockManager.Instance.AddContainer(record.EndPosition, container);
                return;
            }

            string[] subStr = record.EndPosition.Split('|');
            if (record.Tag == DefinedType.VesselBuffer.ToString())
            {
                VesselManager.GlobalInstance().AddContainer(subStr[0], subStr[1], container);
            }
            else
            {
                int equipID = ZPMC.VR.Unity.ParseHelper.TryAsInteger(subStr[1]);
                int bufferID = ZPMC.VR.Unity.ParseHelper.TryAsInteger(subStr[2]);
                int bay = ZPMC.VR.Unity.ParseHelper.TryAsInteger(subStr[3]);
                EquipBase equip = EquipManager.Instance.FindEquip(subStr[0], equipID);

                equip.AddContainer(bufferID, bay, container);
                //IEnumerator corountine = StartCoroutine(corountine);
            }
        }
        catch (System.Exception ex)
        {
            throw new System.Exception("Discharging container error! \n" + record.ToString() + ex.Message);
        }
    }
}

[Serializable]
public class SimRecord : IComparable<SimRecord>
{
    public float StartTime { get; set; }
    public float EndTime { get; set; }
    public int EquipCode { get; set; }
    public int EquipID { get; set; }
    public int EquipSub { get; set; }
    public int EventType { get; set; }

    public string StartPosition { get; set; }
    public string EndPosition { get; set; }
    public string Tag { get; set; }

    public const int RunningType = 10;
    public const int KinematicType = 11;
    public const int SwerveType = 12;
    public const int LoadingType = 21;
    public const int DischargingType = 22;
    public const int VisibleType = 31;
    public const int TitleVisible = 41;

    public int CompareTo(SimRecord other)
    {
        return this.StartTime.CompareTo(other.StartTime);
    }

    public override string ToString()
    {
        return string.Format("Time={0}-{1},Equip={2}'{3},Event = {4} Start ={5},End={6},Tag={7} \n", this.StartTime, this.EndTime, this.EquipID, this.EquipSub, this.EventType, this.StartPosition, this.EndPosition, this.Tag);
    }
}


