using System.Collections.Generic;
using UnityEngine;

namespace ZPMC.VR.Unity
{
	public class ScriptTruck
	{
        private int rmgID;
        public ScriptTruck(int indxASC)
        {
            this.rmgID = indxASC;
        }

        private static int indx = 1;
        private static SequenceList<TaskInfo> taskInfoList = new SequenceList<TaskInfo>();
        public static void ImportTask()
        {
            if (taskInfoList.Count > 0)
            {
                TaskManager.Instance.ImportTask(taskInfoList.ConvertToArray());
            }
            taskInfoList.Clear();
            indx = 1;
        }

        public void WriteDipatchScript(List<CellCode> list, bool isSubtitle = false)
        {
            //Sequence 1
            int count = UMP_Manager.Instance.RouteIn.childCount + UMP_Manager.Instance.RouteSide.childCount + UMP_Manager.Instance.RouteSide.childCount;
            List<float> posX = new List<float>(count), posZ = new List<float>(count), angleY = new List<float>(count);
            for (int i = 0; i < UMP_Manager.Instance.RouteIn.childCount; i++) //沿集港车道行驶
            {
                Transform t = UMP_Manager.Instance.RouteIn.GetChild(i);
                posX.Add(t.position.x); posZ.Add(t.position.z); angleY.Add(t.eulerAngles.y);
            }
            for (int i = 0; i < UMP_Manager.Instance.Swerve1.childCount; i++) //拐弯
            {
                Transform t = UMP_Manager.Instance.Swerve1.GetChild(i);
                posX.Add(t.position.x); posZ.Add(t.position.z); angleY.Add(t.eulerAngles.y);
            }
            for (int i = 0; i < UMP_Manager.Instance.RouteSide.childCount; i++) //拐弯进入倒车道
            {
                Transform t = UMP_Manager.Instance.RouteSide.GetChild(i);
                posX.Add(t.position.x + 942); posZ.Add(t.position.z + 51.75f); angleY.Add(t.eulerAngles.y);
            }
            float[] x1 = posX.ToArray(), z1 = posZ.ToArray(), y1 = angleY.ToArray();
            //Sequence 2
            count = UMP_Manager.Instance.RouteOut.childCount;
            posX = new List<float>(count); posZ = new List<float>(count); angleY = new List<float>(count);
            for (int i = 0; i < UMP_Manager.Instance.RouteOut.childCount; i++)
            {
                Transform t = UMP_Manager.Instance.RouteOut.GetChild(i);
                posX.Add(t.position.x); posZ.Add(t.position.z); angleY.Add(t.eulerAngles.y);
            }
            float[] x2 = posX.ToArray(), z2 = posZ.ToArray(), y2 = angleY.ToArray();

            int truckID = 9011, stoper = 8002; float fTime = 0, loopTime;
            EventScripter scripter = EventScripter.GetGlobalInstance();
            scripter.Initialize();
            bool flag = isSubtitle;
            foreach (var item in list)
            {
                if (flag)
                {
                    scripter.recordMarshal.Enqueue(new SimRecord { StartTime = fTime, EndTime = fTime, StartPosition = string.Format("{0}|{1}|{2}", y1[y1.Length-1], x1[x1.Length-1], z1[z1.Length - 1]), EventType = SimRecord.VisibleType, EndPosition = 1.ToString(), EquipID = truckID });
                    scripter.cars[truckID].x = x1[x1.Length - 1]; scripter.cars[truckID].z = z1[z1.Length-1]; scripter.cars[truckID].yRot = y1[y1.Length - 1];//?????
                    loopTime = scripter.cars[truckID].advanceTime;
                    scripter.Truck_InYard(fTime, scripter.cars[truckID], 1220.05f, -466.32f);//倒车进入陆测交互区fTime = 
                }
                else
                {
                    scripter.recordMarshal.Enqueue(new SimRecord { StartTime = fTime, EndTime = fTime, StartPosition = string.Format("{0}|{1}|{2}", y1[0], x1[0], z1[0]), EventType = SimRecord.VisibleType, EndPosition = 1.ToString(), EquipID = truckID });
                    scripter.cars[truckID].x = x1[0]; scripter.cars[truckID].z = z1[0]; scripter.cars[truckID].yRot = y1[0];//?????
                    loopTime = scripter.cars[truckID].advanceTime;
                    fTime = scripter.Truck_Seg(scripter.cars[truckID].advanceTime, scripter.cars[truckID], 4, x1, z1, y1);
                    fTime = scripter.Truck_InYard(fTime, scripter.cars[truckID], 1220.05f, -466.32f);//倒车进入陆测交互区
                    fTime = fTime - 45f;
                }
                //ASC搬箱
                Container container = BlockManager.Instance.FindBlockBuffer(item.NestName).FindContainer(item.Bay, item.Column, item.Tier);
                fTime = scripter.ASC_Out(fTime, scripter.ascs[rmgID], scripter.cars[truckID], container, flag);//item.NestName, item.Bay, item.Column, item.Tier
                fTime = scripter.Truck_OutYard(scripter.cars[truckID].advanceTime, scripter.cars[truckID], -522f);

                fTime = scripter.Truck_Seg(fTime, scripter.cars[truckID], 4, x2, z2, y2);//出港
                if (flag)
                    scripter.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = scripter.ascs[rmgID].advanceTime, EndTime = fTime, Tag = "外集卡接箱后，经出港车道驶离堆场，通过闸口出港" });
                if (!isSubtitle)  //记录作业任务信息
                {
                    TaskInfo info = new TaskInfo() { StartTime = loopTime, EndTime = fTime, BindContainer = container };
                    info.FromCode = string.Format("{0:D3}{1:D3}{2:D2}{3:D2}", item.NestName, item.Bay, item.Column, item.Tier);
                    info.ToCode = string.Format("港外");
                    taskInfoList.Enqueue(info);
                }
                scripter.recordMarshal.Enqueue(new SimRecord { StartTime = fTime, EndTime = fTime + 3, EventType = SimRecord.KinematicType, StartPosition = 90.ToString(), EndPosition = 0.ToString(), EquipID = stoper });
                fTime = fTime + 4;
                scripter.recordMarshal.Enqueue(new SimRecord
                {
                    StartTime = fTime,
                    EndTime = fTime + 4,
                    EquipID = truckID,
                    EquipSub = 1,
                    EventType = SimRecord.RunningType,
                    StartPosition = scripter.cars[truckID].x.ToString(),
                    EndPosition = (scripter.cars[truckID].x - 20).ToString(),
                    Tag = "0|6"
                });
                scripter.recordMarshal.Enqueue(new SimRecord
                {
                    StartTime = fTime,
                    EndTime = fTime + 4,
                    EquipID = truckID,
                    EquipSub = 2,
                    EventType = SimRecord.RunningType,
                    StartPosition = scripter.cars[truckID].z.ToString(),
                    EndPosition = (scripter.cars[truckID].z + 2f).ToString(),
                    Tag = "0|6"
                });
                scripter.recordMarshal.Enqueue(new SimRecord { StartTime = fTime + 4, EndTime = fTime + 7, EventType = SimRecord.KinematicType, StartPosition = 0.ToString(), EndPosition = 90.ToString(), EquipID = stoper });
                fTime = fTime + 4; scripter.cars[truckID].advanceTime = fTime;
                scripter.recordMarshal.Enqueue(new SimRecord { StartTime = scripter.cars[truckID].advanceTime, EndTime = scripter.cars[truckID].advanceTime, EventType = SimRecord.VisibleType, EndPosition = 0.ToString(), EquipID = truckID });
                if (flag)
                {
                    scripter.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = fTime, EndTime = float.MaxValue, Tag = "疏港作业持续进行……！" });
                    flag = false;
                }
            }
        }

        public void WriteCollectScript(List<CellCode> list, bool isSubtitle = false)
        {
            //Sequence 1
            int count = UMP_Manager.Instance.RouteIn.childCount + UMP_Manager.Instance.Swerve1.childCount + UMP_Manager.Instance.RouteSide.childCount;
            List<float> posX = new List<float>(count), posZ = new List<float>(count), angleY = new List<float>(count);
            for (int i = 0; i < UMP_Manager.Instance.RouteIn.childCount; i++) //沿集港车道行驶
            {
                Transform t = UMP_Manager.Instance.RouteIn.GetChild(i);
                posX.Add(t.position.x); posZ.Add(t.position.z); angleY.Add(t.eulerAngles.y);
            }
            for (int i = 0; i < UMP_Manager.Instance.Swerve1.childCount; i++)
            {
                Transform t = UMP_Manager.Instance.Swerve1.GetChild(i);
                posX.Add(t.position.x); posZ.Add(t.position.z); angleY.Add(t.eulerAngles.y);
            }
            for (int i = 0; i < UMP_Manager.Instance.RouteSide.childCount; i++) //拐弯进入倒车道
            {
                Transform t = UMP_Manager.Instance.RouteSide.GetChild(i);
                posX.Add(t.position.x + 942); posZ.Add(t.position.z + 51.75f); angleY.Add(t.eulerAngles.y);
            }
            float[] x1 = posX.ToArray(), z1 = posZ.ToArray(), y1 = angleY.ToArray();
            // sequence 2
            count = UMP_Manager.Instance.RouteOut.childCount;
            posX = new List<float>(count); posZ = new List<float>(count); angleY = new List<float>(count);
            for (int i = 0; i < UMP_Manager.Instance.RouteOut.childCount; i++)
            {
                Transform t = UMP_Manager.Instance.RouteOut.GetChild(i);
                posX.Add(t.position.x); posZ.Add(t.position.z); angleY.Add(t.eulerAngles.y);
            }
            float[] x2 = posX.ToArray(); float[] z2 = posZ.ToArray(); float[] y2 = angleY.ToArray();

            EventScripter scripter = EventScripter.GetGlobalInstance();
            scripter.Initialize();
            float fTime = 0, tempTime = 0,loopTime;
            int truckID = 9002, stoper = 8001;
            bool flag = isSubtitle;
            foreach (var item in list)
            {
                //if (i == 2)
                //    fTime = Mathf.Max(scripter.ascs[rmgID].advanceTime, scripter.cars[truckID].advanceTime);
                //else
                fTime = scripter.cars[truckID].advanceTime;
                //出现
                Container container = ContainerSpawner.Instance.SpawnContainer(ContainerSpawner.Instance.GetRandomOwnerIndex(), Container.FortyFeet, Container.EightHalfFeet);
                container.gameObject.SetActive(false);
                UserDriver.Instance.CollectingContainer.Add(indx.ToString(), container);
                scripter.recordMarshal.Enqueue(new SimRecord { StartTime = fTime, EndTime = fTime, EventType = SimRecord.VisibleType, StartPosition = string.Format("{0}|{1}|{2}", y1[0], x1[0], z1[0]), Tag = "0|" + indx.ToString(), EndPosition = 1.ToString(), EquipID = truckID });//sdlfjla|12.192|2.591
                indx++;
                scripter.cars[truckID].x = x1[0]; scripter.cars[truckID].z = z1[0]; scripter.cars[truckID].yRot = y1[0];
                //车挡
                loopTime = fTime;
                scripter.recordMarshal.Enqueue(new SimRecord { StartTime = fTime, EndTime = fTime + 3, EventType = SimRecord.KinematicType, StartPosition = (-90).ToString(), EndPosition = 0.ToString(), EquipID = stoper });
                fTime = fTime + 4;
                scripter.recordMarshal.Enqueue(new SimRecord { StartTime = fTime + 2, EndTime = fTime + 5, EventType = SimRecord.KinematicType, StartPosition = 0.ToString(), EndPosition = (-90).ToString(), EquipID = stoper });
                //入港到倒车道
                tempTime = fTime;
                fTime = scripter.Truck_Seg(fTime, scripter.cars[truckID], 4, x1, z1, y1);//1
                if (flag)
                {
                    scripter.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = 0, EndTime = fTime, Tag = "外集卡通过闸口，沿进港车道进入作业堆场后方，斜行进入倒车道" });
                    tempTime = fTime;
                }
                //倒车进入陆测交互区
                fTime = scripter.Truck_InYard(fTime, scripter.cars[truckID], 1220.05f, -466.32f);//倒车进入陆测交互区
                if (flag)
                    scripter.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = tempTime, EndTime = fTime, Tag = "外集卡倒车进入陆侧交换区" }); tempTime = fTime;
                //轨道吊抓箱
                if (!flag)
                    fTime = fTime - 45f;
                
                fTime = scripter.ASC_Collected(fTime, scripter.ascs[rmgID], scripter.cars[truckID], item.NestName, item.Bay, item.Column, item.Tier, flag);//
                if (!isSubtitle)
                {
                    TaskInfo info = new TaskInfo() { StartTime = loopTime, EndTime = fTime, BindContainer = container };
                    info.FromCode = string.Format("港外");
                    info.ToCode = string.Format("{0:D3}{1:D3}{2:D2}{3:D2}", item.NestName, item.Bay, item.Column, item.Tier);
                    taskInfoList.Enqueue(info);
                }
                if (flag)
                {
                    scripter.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = fTime, EndTime = float.MaxValue, Tag = "集港作业持续进行……！" });
                    flag = false;
                }
                // 集卡出港
                scripter.Truck_OutYard(scripter.cars[truckID].advanceTime, scripter.cars[truckID], -522f);
                scripter.Truck_Seg(scripter.cars[truckID].advanceTime, scripter.cars[truckID], 4, x2, z2, y2);

                //集卡消失
                scripter.recordMarshal.Enqueue(new SimRecord { StartTime = scripter.cars[truckID].advanceTime, EndTime = scripter.cars[truckID].advanceTime, EventType = SimRecord.VisibleType, EndPosition = 0.ToString(), EquipID = truckID });
            }
        }

        public void WriteReorderScript(List<CellCode> list, bool isSubtitle = false)
        {
            EventScripter scripter = EventScripter.GetGlobalInstance();
            scripter.Initialize();
            bool flag = isSubtitle;
            float fTime = 0, loopTime;
            this.column = 0; this.tier = 1;
            foreach (var item in list)
            {
                this.LoopCellAscend();
                loopTime = fTime;
                fTime = scripter.ASC_ReOrder(fTime, scripter.ascs[rmgID], item.NestName, item.Bay, item.Column, item.Tier, 22, this.column, this.tier, flag);
                if (!isSubtitle)//记录作业任务信息
                {
                    Container container = BlockManager.Instance.FindBlockBuffer(item.NestName).FindContainer(item.Bay, item.Column, item.Tier);
                    TaskInfo info = new TaskInfo() { StartTime = loopTime, EndTime = fTime, BindContainer = container };
                    info.FromCode = string.Format("{0:D3}{1:D3}{2:D2}{3:D2}", item.NestName, item.Bay, item.Column, item.Tier);
                    info.ToCode = string.Format("{0:D3}{1:D3}{2:D2}{3:D2}", item.NestName, 22, this.column, this.tier);
                    taskInfoList.Enqueue(info);
                }
                if (flag)
                {
                    scripter.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = fTime, EndTime = float.MaxValue, Tag = "ASC翻箱任务持续进行……" });
                    flag = false;
                }
            }
        }

        public List<CellCode> BuildCellDescend(string bName, int bay, int count)
        {
            List<CellCode> list = new List<CellCode>(count);
            this.tier = 5; this.column = 0;
            for (int i = 0; i < count; i++)
            {
                this.LoopCelDescend();
                list.Add(new CellCode() { NestName = bName, Bay = bay, Column = this.column, Tier = this.tier });
            }
            return list;
        }

        public List<CellCode> BuildCellAscend(string bName, int bay, int count)
        {
            List<CellCode> list = new List<CellCode>(count);
            this.tier = 1; this.column = 0;
            for (int i = 0; i < count; i++)
            {
                this.LoopCellAscend();
                list.Add(new CellCode() { NestName = bName, Bay = bay, Column = this.column, Tier = this.tier });
            }
            return list;
        }

        int column = 0;
        int tier = 1;
        private void LoopCellAscend()
        {
            column++;
            if (column > 10)
            {
                tier++;
                column = 1;
            }
        }

        private void LoopCelDescend()
        {
            column++;
            if (column > 10)
            {
                tier--;
                column = 1;
            }
        }
	}
}
