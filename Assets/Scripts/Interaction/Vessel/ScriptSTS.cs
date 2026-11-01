using System;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace ZPMC.VR.Unity
{
	public class ScriptSTS
	{
        private int qcID;
        private int[] agvs;
        private float[] PosBufferAGV;
        private int[] frames;
        private float posUnderSTS;
        private float posHW_B2Y;
        private float posHW_Y2B;
        private int rmgID;
        private string blockName;
        private int blockBay;
        private int shipBay;

        public ScriptSTS(int indx)
        {
            this.qcID = indx;
            switch (indx)
            {
                case 1010:
                    {
                        this.rmgID = 2067;
                        this.posUnderSTS = -41f;
                        this.posHW_Y2B = -89.5f;
                        this.posHW_B2Y = -94.5f;
                        this.agvs = new int[] { 3004, 3003, 3002, 3001 };
                        this.frames = new int[] { 6124, 6123, 6122, 6121 };//6125, 
                        this.PosBufferAGV = new float[] { 1192f, 1188f, 1184f, 1180f };
                        this.blockName = "A35";
                        this.blockBay = 14;
                        this.shipBay = 2;
                        break;
                    }
                case 1011:
                    {
                        this.rmgID = 2069;
                        this.posUnderSTS = -45f;
                        this.posHW_Y2B = -99.5f;
                        this.posHW_B2Y = -99.5f;
                        this.agvs = new int[] { 3008, 3007, 3006, 3005 };
                        this.frames = new int[] { 6129, 6128, 6127, 6126 };//6125, 
                        this.PosBufferAGV = new float[] { 1242f, 1238f, 1234f, 1230f };
                        this.blockName = "A36";
                        this.blockBay = 18;
                        this.shipBay = 18;
                        break;
                    }
                case 1012:
                    {
                        this.rmgID = 2071;
                        this.posUnderSTS = -49f;
                        this.posHW_Y2B = -104.5f;
                        this.posHW_B2Y = -104.5f;
                        this.agvs = new int[] { 3038, 3039, 3040, 3041 };
                        this.frames = new int[] { 6135, 6134, 6133, 6132 };//6125, 
                        this.PosBufferAGV = new float[] { 1300f, 1296f, 1292f, 1288f };
                        this.blockName = "A37";
                        this.blockBay = 6;
                        this.shipBay = 34;
                        break;
                    }
                case 1013:
                    {
                        this.rmgID = 2077;
                        this.posUnderSTS = -53f;
                        this.posHW_Y2B = -109.5f;
                        this.posHW_B2Y = -109.5f;
                        this.agvs = new int[] { 3042, 3043, 3044, 3045 };
                        this.frames = new int[] { 6139, 6138, 6137, 6136 };//6125, 
                        this.PosBufferAGV = new float[] { 1380f, 1376f, 1372f, 1368f };
                        this.blockName = "A40";
                        this.blockBay = 14;
                        this.shipBay = 50;
                        break;
                    }
                default:
                    break;
            }
        }

        private static SequenceList<TaskInfo> taskInfoList = new SequenceList<TaskInfo>();
        //public static void Reset()
        //{
        //    taskInfoList = new SequenceList<TaskInfo>();
        //}
        public static void ImportTask()
        {
            if (taskInfoList.Count > 0)
            {
                TaskManager.Instance.ImportTask(taskInfoList.ConvertToArray());
            }
            taskInfoList.Clear();
        }

        /*public void WriteDischargeScript(List<CellCode> list, bool isSubtitle = false)
        {
            EventScripter scripter = EventScripter.GetGlobalInstance();
            float fTime = 0;
            int agvID = 0, frameID = -1, midBuffer = 0;
            this.tier = 1; this.column = 0;
            bool flag = isSubtitle;

            foreach (var code in list)
            {
                agvID = this.LoopAGV(); frameID = this.LoopFrame(); midBuffer = LoopMidBuffer(); LoopCellAscend();
                Container container = VesselManager.GlobalInstance().FindContainer(code.NestName, code.Bay, code.Column, code.Tier);
                float xGantry = container.transform.position.x;
                float loopTime = 0;

                scripter.AGV_Buffer2STS(fTime, scripter.agvs[agvID], 0, posUnderSTS, xGantry, false); loopTime = fTime;
                fTime = scripter.STS_Discharging(fTime, scripter.stss[qcID], container, midBuffer, scripter.agvs[agvID], flag);
                fTime = scripter.AGV_STS2Buffer(fTime, scripter.agvs[agvID], xGantry + 16, -75f, 1.5f, flag);
                fTime = scripter.AGV_Buffer2Yard(fTime, scripter.agvs[agvID], 1.5f, this.posHW_B2Y, scripter.mates[frameID].equip.transform.position.x, -136.5f, flag);
                fTime = scripter.AGV_Mate_Laden(fTime, scripter.agvs[agvID], scripter.mates[frameID], flag);
                scripter.AGV_Yard2Buffer(fTime, scripter.agvs[agvID], this.posHW_Y2B, this.PosBufferAGV[indxAGV], -74f, 1.5f, false);
                if (flag)
                    fTime = scripter.mates[frameID].advanceTime;
                else
                    fTime = Mathf.Max(scripter.mates[frameID].advanceTime - 30f, scripter.ascs[rmgID].advanceTime);

                fTime = scripter.ASC_Collected(fTime, scripter.ascs[rmgID], scripter.mates[frameID], this.blockName, this.blockBay, this.column, this.tier, flag);
                fTime = scripter.ASC_Collected(fTime, scripter.ascs[rmgID], scripter.mates[frameID], target.BlockName, target.Bay, target.Column, target.Tier, flag);
                if (!isSubtitle)
                {
                    TaskInfo info = new TaskInfo() { StartTime = loopTime, EndTime = fTime, BindContainer = container };
                    info.FromCode = string.Format("Ship{0:D3}{1:D2}{2:D2}", code.Bay, code.Column, code.Tier);
                    info.ToCode = string.Format("{0:D3}{1:D3}{2:D2}{3:D2}", this.blockName, this.blockBay, this.column, this.tier);
                    taskInfoList.Enqueue(info);
                }
                if (flag)
                {
                    scripter.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = fTime, EndTime = float.MaxValue, Tag = "卸船作业持续进行……！" });
                    flag = false;
                    continue;
                }
                fTime = scripter.stss[this.qcID].advanceTime;
            }
        }*/

        public static bool SupportsDischargeTarget(string block)
        {
            return block == "A35" || block == "A36" || block == "A37" || block == "A40";
        }

        private void ConfigureDischargeTarget(string block)
        {
            // 恢复原场景的固定箱区与岸桥绑定，同时校验计划没有被外部调用交叉分配。
            int expectedCrane = Vtp.VR.Integration.AutomaticQuayCraneScheduler.GetFixedQuayCraneId(block);
            if (expectedCrane != qcID)
                throw new InvalidOperationException("箱区 " + block + " 必须由岸桥 " + expectedCrane + " 执行，不能使用岸桥 " + qcID + "。");
            blockName = block;
            switch (block)
            {
                case "A35": rmgID=2067; frames=new[]{6124,6123,6122,6121}; posHW_B2Y=-94.5f; posHW_Y2B=-89.5f; break;
                case "A36": rmgID=2069; frames=new[]{6129,6128,6127,6126}; posHW_B2Y=posHW_Y2B=-99.5f; break;
                case "A37": rmgID=2071; frames=new[]{6135,6134,6133,6132}; posHW_B2Y=posHW_Y2B=-104.5f; break;
                case "A40": rmgID=2077; frames=new[]{6139,6138,6137,6136}; posHW_B2Y=posHW_Y2B=-109.5f; break;
                default: throw new InvalidOperationException("不支持的卸船目标箱区：" + block);
            }
        }

        public void WriteDischargeScript(List<CellCode> list, bool isSubtitle = false, bool automaticAssignment = false,
            IList<DischargeTargetSlot> assignedTargets = null)
        {
            EventScripter scripter = EventScripter.GetGlobalInstance();
            float fTime = automaticAssignment ? scripter.stss[qcID].advanceTime : 0;
            if(automaticAssignment && (assignedTargets==null || assignedTargets.Count!=list.Count))
                throw new InvalidOperationException("自动卸船的源箱与目标任务数量不一致。");
            int automaticIndex=0;
            int agvID = 0, frameID = -1, midBuffer = 0;

            bool flag = isSubtitle;

            foreach (var code in list)
            {
                Container container = VesselManager.GlobalInstance().FindContainer(code.NestName, code.Bay, code.Column, code.Tier);
                if (container == null) { if (automaticAssignment) throw new InvalidOperationException("自动分配的船舶源箱已失效。"); Debug.LogWarning("未找到船上待卸集装箱，已跳过：" + string.Format("{0} Bay={1}, Column={2}, Tier={3}", code.NestName, code.Bay, code.Column, code.Tier)); continue; }

                DischargeTargetSlot targetSlot;

                if(automaticAssignment) targetSlot=assignedTargets[automaticIndex++];
                else if (!DischargeTargetSlotQueue.TryGetNextAvailableTarget(this.blockName, out targetSlot))
                { Debug.LogWarning("卸船目标箱位序列已用完，后续卸船任务不再生成。当前船箱位：" + string.Format("Ship{0:D3}{1:D2}{2:D2}", code.Bay, code.Column, code.Tier)); continue; }
                if (automaticAssignment) ConfigureDischargeTarget(targetSlot.BlockName);

                agvID = this.LoopAGV(); frameID = this.LoopFrame(); midBuffer = LoopMidBuffer();
                if(automaticAssignment)fTime=Mathf.Max(fTime,scripter.agvs[agvID].advanceTime);
                float xGantry = container.transform.position.x;
                float loopTime = 0;

                scripter.AGV_Buffer2STS(fTime, scripter.agvs[agvID], 0, posUnderSTS, xGantry, false); loopTime = fTime;
                fTime = scripter.STS_Discharging(fTime, scripter.stss[qcID], container, midBuffer, scripter.agvs[agvID], flag);
                fTime = scripter.AGV_STS2Buffer(fTime, scripter.agvs[agvID], xGantry + 16, -75f, 1.5f, flag);
                fTime = scripter.AGV_Buffer2Yard(fTime, scripter.agvs[agvID], 1.5f, this.posHW_B2Y, scripter.mates[frameID].equip.transform.position.x, -136.5f, flag);
                fTime = scripter.AGV_Mate_Laden(fTime, scripter.agvs[agvID], scripter.mates[frameID], flag);
                scripter.AGV_Yard2Buffer(fTime, scripter.agvs[agvID], this.posHW_Y2B, this.PosBufferAGV[indxAGV], -74f, 1.5f, false);
                if (flag)
                    fTime = scripter.mates[frameID].advanceTime;
                else
                    fTime = Mathf.Max(scripter.mates[frameID].advanceTime - 30f, scripter.ascs[rmgID].advanceTime);

                fTime = scripter.ASC_Collected(fTime, scripter.ascs[rmgID], scripter.mates[frameID], targetSlot.BlockName, targetSlot.Bay, targetSlot.Column, targetSlot.Tier, flag);

                if (!isSubtitle)
                {
                    TaskInfo info = new TaskInfo() { StartTime = loopTime, EndTime = fTime, BindContainer = container };
                    info.FromCode = string.Format("Ship{0:D3}{1:D2}{2:D2}", code.Bay, code.Column, code.Tier);
                    info.ToCode = string.Format("{0:D3}{1:D3}{2:D2}{3:D2}", targetSlot.BlockName, targetSlot.Bay, targetSlot.Column, targetSlot.Tier);
                    taskInfoList.Enqueue(info);
                }
                if (flag)
                {
                    scripter.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = fTime, EndTime = float.MaxValue, Tag = "卸船作业持续进行……！" });
                    flag = false;
                    continue;
                }
                fTime = scripter.stss[this.qcID].advanceTime;
            }
        }

        public void WriteLoadScript(List<CellCode> list, bool isSubtitle = false)
        {
            EventScripter scripter = EventScripter.GetGlobalInstance();
            float fTime = 0, tempTime,loopTime;
            int agvID = 0, frameID = -1;
            this.tier = 1; this.column = -1;
            bool flag = isSubtitle;
            float xGantry = VesselManager.GlobalInstance().FindVesselBuffer("Vessel004").GetWorldPosition(this.shipBay, 1).x;
            foreach (var code in list)
            {
                try
                {
                    agvID = this.LoopAGV(); frameID = this.LoopFrame(); LoopMidBuffer(); LoopShipCell();
                    fTime = Mathf.Max(scripter.ascs[rmgID].advanceTime, scripter.agvs[agvID].advanceTime); loopTime = fTime;
                    scripter.AGV_Buffer2Yard(fTime, scripter.agvs[agvID], 1.5f, this.posHW_B2Y, scripter.mates[frameID].equip.transform.position.x, -136.5f);
                    Container container = BlockManager.Instance.FindBlockBuffer(code.NestName).FindContainer(code.Bay, code.Column, code.Tier);
                    fTime = scripter.ASC_Out(fTime, scripter.ascs[rmgID], scripter.mates[frameID], container, flag);//code.NestName, code.Bay, code.Column, code.Tier
                    fTime = scripter.AGV_Mate_Empty(fTime, scripter.agvs[agvID], scripter.mates[frameID], 0, scripter.agvs[agvID].z, flag);
                    tempTime = fTime;
                    fTime = scripter.AGV_Yard2Buffer(fTime, scripter.agvs[agvID], this.posHW_Y2B, xGantry + 16f, -74f, 1.5f, false);
                    fTime = scripter.AGV_Buffer2STS(fTime, scripter.agvs[agvID], 1.5f, this.posUnderSTS, xGantry, false);
                    if (flag)
                        scripter.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = tempTime, EndTime = fTime, Tag = "AGV经高速车道进入缓冲区，再运行至岸桥作业区" });
                    fTime = Mathf.Max(scripter.stss[qcID].advanceTime, fTime);
                    scripter.STS_Loading(fTime - 6f, scripter.stss[qcID], "Vessel004", this.shipBay, ShipColumns[this.column], shipTiers[this.tier], this.indxMid, scripter.agvs[agvID], flag);
                    fTime = scripter.AGV_STS2Buffer(scripter.agvs[agvID].advanceTime, scripter.agvs[agvID], this.PosBufferAGV[indxAGV], -75f, 1.5f, false);
                    if (!isSubtitle)
                    {
                        TaskInfo info = new TaskInfo() { StartTime = loopTime, EndTime = scripter.stss[qcID].advanceTime, BindContainer = container };
                        info.FromCode = string.Format("{0:D3}{1:D3}{2:D2}{3:D2}", code.NestName, code.Bay, code.Column, code.Tier);
                        info.ToCode = string.Format("Ship{0:D3}{1:D2}{2:D2}", this.blockBay, this.column, this.tier);
                        taskInfoList.Enqueue(info);
                    }
                    if (flag)
                    {
                        scripter.recordMarshal.Enqueue(new SimRecord { EventType = SimRecord.TitleVisible, StartTime = scripter.stss[qcID].advanceTime, EndTime = float.MaxValue, Tag = "装船作业持续进行……" });
                        flag = false;
                        scripter.ascs[rmgID].advanceTime = scripter.stss[qcID].advanceTime;
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }
            }
        }
        
        public List<CellCode> BuildLoadList(string bName, int bay, int count)
        {
            List<CellCode> list = new List<CellCode>(count);
            this.tier = 4; this.column = 0;
            for (int i = 0; i < count; i++)
            {
                this.LoopCelDescend();
                list.Add(new CellCode() { NestName = bName, Bay = bay, Column = this.column, Tier = this.tier });
            }

            return list;
        }

        //public void WriteDischargeScript(Container[] containers)//SequenceList<SimRecord>  return recordMarshal;
        //{
        //    EventScripter scripter = EventScripter.GetGlobalInstance();
        //    float fTime = 0;
        //    int agvID = 0, frameID = -1, midBuffer = 0;
        //    this.tier = 1; 
        //    for (int i = 0; i < containers.Length; i++)
        //    {
        //        agvID = this.LoopAGV(); frameID = this.LoopFrame(); midBuffer = LoopMidBuffer(); LoopCell();
        //        float xGantry = containers[i].transform.position.x;
        //        fTime = Mathf.Max(scripter.stss[this.qcID].advanceTime, 0);
        //        scripter.AGV_Buffer2STS(fTime, scripter.agvs[agvID], 0, posUnderSTS, xGantry, false);
        //        fTime = scripter.STS_Discharging(fTime, scripter.stss[qcID], containers[i], midBuffer, scripter.agvs[agvID], false);
        //        fTime = scripter.AGV_STS2Buffer(fTime, scripter.agvs[agvID], xGantry + 16, -75f, 1.5f, false);
        //        fTime = scripter.AGV_Buffer2Yard(fTime, scripter.agvs[agvID], 1.5f, this.posHW_B2Y, scripter.mates[frameID].equip.transform.position.x, -136.5f, false);
        //        fTime = scripter.AGV_Mate_Laden(fTime, scripter.agvs[agvID], scripter.mates[frameID], false);
        //        scripter.AGV_Yard2Buffer(fTime, scripter.agvs[agvID], this.posHW_Y2B, this.PosBufferAGV[indxAGV], -74f, 1.5f, false);
        //        fTime = Mathf.Max(scripter.mates[frameID].advanceTime - 30f, scripter.ascs[rmgID].advanceTime);
        //        fTime = scripter.ASC_Collected(fTime, scripter.ascs[rmgID], scripter.mates[frameID], this.blockName, this.blockBay, this.column, this.tier, false);//1
        //    }
        //}
        
        //public void WriteLoadScript(Container[] containers)
        //{
        //    EventScripter scripter = EventScripter.GetGlobalInstance();
        //    float fTime = 0;
        //    int agvID = 0, frameID = -1, midBuffer = 0;
        //    this.tier = 1; this.column = -1;
        //    float xGantry = VesselManager.GlobalInstance().FindVesselBuffer("Vessel004").GetWorldPosition(this.shipBay, 1).x;
        //    for (int i = 0; i < containers.Length; i++)
        //    {
        //        try
        //        {
        //            agvID = this.LoopAGV(); frameID = this.LoopFrame(); midBuffer = LoopMidBuffer(); LoopShipCell();
        //            fTime = Mathf.Max(scripter.ascs[rmgID].advanceTime, scripter.agvs[agvID].advanceTime);
        //            scripter.AGV_Buffer2Yard(fTime, scripter.agvs[agvID], 1.5f, this.posHW_B2Y, scripter.mates[frameID].equip.transform.position.x, -136.5f);
        //            fTime = scripter.ASC_Out(fTime, scripter.ascs[rmgID], scripter.mates[frameID], containers[i], false);
        //            fTime = scripter.AGV_Mate_Empty(fTime, scripter.agvs[agvID], scripter.mates[frameID], 0, scripter.agvs[agvID].z, false);
        //            fTime = scripter.AGV_Yard2Buffer(fTime, scripter.agvs[agvID], this.posHW_Y2B, xGantry + 16f, -74f, 1.5f, false);
        //            fTime = scripter.AGV_Buffer2STS(fTime, scripter.agvs[agvID], 1.5f, this.posUnderSTS, xGantry, false);
        //            fTime = Mathf.Max(scripter.stss[qcID].advanceTime, fTime);
        //            scripter.STS_Loading(fTime - 6f, scripter.stss[qcID], "Vessel004", this.shipBay, ShipColumns[this.column], shipTiers[this.tier], this.indxMid, scripter.agvs[agvID], false);
        //            fTime = scripter.AGV_STS2Buffer(scripter.agvs[agvID].advanceTime, scripter.agvs[agvID], this.PosBufferAGV[indxAGV], -75f, 1.5f, false);
        //        }
        //        catch (Exception ex)
        //        {
        //            throw new Exception(ex.Message);
        //        }
        //    }
        //}
        
        private int column = 0;
        private int tier = 1;
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

        private static int[] ShipColumns = new int[] { 12, 10, 8, 6, 4, 2, 0, 1, 3, 5, 7, 9, 11 };
        private static int[] shipTiers = new int[] { 82, 84, 86, 88, 90 };
        private void LoopShipCell()
        {
            column++;
            if (column > 12)
            {
                tier++;
                column = 0;
            }
        }

        private int indxAGV = -1;
        private int LoopAGV()
        {
            indxAGV++;
            if (indxAGV >= this.agvs.Length)
                indxAGV = 0;
            return this.agvs[indxAGV];
        }

        private int indxFrame = -1;
        private int LoopFrame()
        {
            indxFrame++;
            if (indxFrame >= this.frames.Length)
                indxFrame = 0;
            return this.frames[indxFrame];
        }

        private int indxMid = 0;
        private int LoopMidBuffer()
        {
            indxMid++;
            if (indxMid > 2)
                indxMid = 1;
            return indxMid;
        }
	}
}
