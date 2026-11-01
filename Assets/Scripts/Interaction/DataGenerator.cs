//using System;
//using System.Data;
//using System.Collections;
//using System.Collections.Generic;
//using ZPMC.VR.Unity;
//using Mono.Data.Sqlite;

//namespace ZPMC.VR.Unity
//{
//    public class DataGenerator
//    {
//        #region Creation
//        private static DataGenerator localObject;
//        private DataGenerator()
//        {
//        }
//        public static DataGenerator GetGlobalInstance()
//        {
//            if (localObject == null)
//            {
//                localObject = new DataGenerator();
//            }

//            return localObject;
//        }
//        #endregion

//        public void SaveYardContainers()
//        {
//            SqliteHelper db = SqliteHelper.CreateDefaultAccess();//new SqliteHelper(@"data source=" + SqliteHelper.PortDataFilePath);//
//            db.DeleteContents("YardInfo");
//            try
//            {
//                DataTable table = db.ReadByDataTable("YardInfo");

//                string[] values = new string[10];
//                foreach (BlockBuffer b in BlockManager.Instance)
//                {
//                    foreach (BayHeld bay in b)
//                    {
//                        foreach (CellHeld cell in bay)
//                        {
//                            foreach (Container unit in cell)
//                            {
//                                values[0] = MakeID().ToString();
//                                values[1] = MakeContainer();
//                                values[3] = "Export";
//                                if (unit.LengthMetre == Container.FortyFeet)
//                                {
//                                    values[2] = "40PF";
//                                    values[4] = string.Format("{0:F4}", Container.FortyFeet);
//                                }
//                                else
//                                {
//                                    values[2] = "20PF";
//                                    values[4] = Container.TwentyFeet.ToString();
//                                }
//                                values[5] = unit.HeightMetre.ToString();
//                                values[6] = UnityEngine.Random.Range(5, 40).ToString();//Weight
//                                values[7] = unit.SlotFlag.ToString();
//                                values[8] = "0";
//                                //values[9] = string.Format("'{0}'", unit.ShipOwnerName);

//                                //db.InsertInto("YardInfo", values);
//                                DataRow row = table.NewRow();
//                                row.ItemArray = values;
//                                table.Rows.Add(row);
//                            }
//                        }
//                    }
//                }
//                db.UpdateTable();
//            }
//            catch (Exception ex)
//            {
//                throw new Exception(ex.Message);
//            }
//            finally
//            {
//                db.CloseConnnection();
//            }
//        }

//        public void SaveVesselContainers()
//        {
//            SqliteHelper db = SqliteHelper.CreateDefaultAccess();//new SqliteHelper(@"data source=" + SqliteHelper.PortDataFilePath);//
//            db.DeleteContents("VesselInfo");
//            try
//            {
//                DataTable table = db.ReadByDataTable("VesselInfo");

//                string[] values = new string[12];
//                foreach (VesselBuffer b in VesselManager.GlobalInstance())
//                {
//                    foreach (BayHeld bay in b)
//                    {
//                        foreach (CellHeld cell in bay)
//                        {
//                            foreach (Container unit in cell)
//                            {
//                                values[0] = MakeID().ToString();
//                                values[1] = MakeContainer();
//                                values[3] = "Discharge";
//                                if (unit.LengthMetre == Container.FortyFeet)
//                                {
//                                    values[2] = "40PF";
//                                    values[4] = string.Format("{0:F4}", Container.FortyFeet);
//                                }
//                                else
//                                {
//                                    values[2] = "20PF";
//                                    values[4] = Container.TwentyFeet.ToString();
//                                }
//                                values[5] = unit.HeightMetre.ToString();
//                                values[6] = UnityEngine.Random.Range(5, 40).ToString();//Weight
//                                values[7] = "Vessel001";
//                                values[8] = unit.SlotFlag.ToString();
//                                values[10] = "0";

//                                DataRow row = table.NewRow();
//                                row.ItemArray = values;
//                                table.Rows.Add(row);
//                            }
//                        }
//                    }
//                }
//                db.UpdateTable();
//            }
//            catch (Exception ex)
//            {
//                throw new Exception(ex.Message);
//            }
//            finally
//            {
//                db.CloseConnnection();
//            }
//        }

//        private static int RecordID = 0;
//        private static int MakeID()
//        {
//            return ++RecordID;
//        }

//        private static string MakeContainer()
//        {
//            return string.Format("YMLU8{0:D6}", RecordID);//:F2
//        }

//        public void InitialPosition()
//        {
//            DataTable table = SqliteHelper.CreateDefaultAccess().ReadByDataTable("EquipmentPosition");
//            DataTableReader reader = new DataTableReader(table);
//            while (reader.Read())
//            {
//                int equipID = ParseHelper.TryAsInteger(reader["Equipment"].ToString());
//                int equipType = EquipManager.GetEquipTypeByID(ParseHelper.TryAsInteger(reader["Equipment"].ToString()));
//                switch (equipType)
//                {
//                    case EquipBase.ASC_Code:
//                        ASC temp = EquipManager.Instance.FindEquipASC(equipID);
//                        temp.transform.position = new UnityEngine.Vector3(ParseHelper.TryAsSingle(reader["PSNX"].ToString()), temp.transform.position.y, temp.transform.position.z);
//                        break;
//                    default:
//                        break;
//                }
//            }
//        }

//        public void WriteEvent(ICollection<SimRecord> recordMarshal)
//        {
//            SqliteHelper db = null;
//            try
//            {
//                db = SqliteHelper.CreateDefaultAccess();
//                db.DeleteContents("EventOutput");

//                DataTable table = db.ReadByDataTable("EventOutput");

//                string[] values = new string[12];
//                foreach (SimRecord record in recordMarshal)
//                {
//                    values[0] = MakeID().ToString();
//                    values[1] = record.StartTime.ToString();
//                    values[2] = record.EndTime.ToString();
//                    values[3] = record.EventType.ToString();
//                    values[4] = record.EquipCode.ToString();
//                    values[5] = record.EquipID.ToString();
//                    values[6] = record.EquipSub.ToString();
//                    values[9] = record.StartPosition;
//                    values[10] = record.EndPosition;
//                    values[11] = record.Tag;

//                    DataRow row = table.NewRow();
//                    row.ItemArray = values;
//                    table.Rows.Add(row);
//                }

//                db.UpdateTable();
//            }
//            catch (Exception ex)
//            {
//                throw new Exception(ex.Message);
//            }
//            finally
//            {
//                db.CloseConnnection();
//            }
//        }
//    }
//}
