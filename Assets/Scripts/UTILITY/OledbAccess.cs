//using UnityEngine;
//using System;
//using System.Collections;
//using System.Data.OleDb;
//using System.Data;

//namespace ZPMC.VR.Unity.Scripts
//{
//    public class OledbAccess
//    {
//        #region Creation
//        private OledbAccess()
//        {
//            this.BulidConnection();
//        }// Don't allow creation
//        private static OledbAccess localObject = null;
//        public static OledbAccess GetGlobalInstance()
//        {
//            if (localObject == null)
//            {
//                localObject = new OledbAccess();
//            }

//            return localObject;
//        }
//        #endregion

//        private const string fileYardMapName = "/PortData.db";
//        private OleDbConnection dbConnection;//
//        //private OleDbCommand dbCommand;
//        //private OleDbDataReader reader;

//        public static string DataBaseFilePath
//        {
//            get { return FileHelper.GetDataPath() + fileYardMapName; }
//        }

//        private void BulidConnection()
//        {
//            string connection = @"Provider=Microsoft.Jet.OLEDB.4.0;Data Source=" + DataBaseFilePath;
//            dbConnection = new OleDbConnection(connection);
//        }

//        /// <summary>
//        /// 打开数据源链接
//        /// </summary>
//        /// <returns>Orecle Connnetction</returns>
//        private void Open()
//        {
//            if (dbConnection.State == ConnectionState.Closed)
//            {
//                try
//                {
//                    dbConnection.Open();//打开数据库连接
//                }
//                catch (Exception ex)
//                {
//                    throw ex;
//                }
//            }
//        }

//        public void SaveYardContainers()
//        {
//            this.Open();

//            try
//            {
//                //foreach (BlocKBuffer b in BlockManager.GetGlobalInstance())
//                //{
//                //foreach (BayHeld bay in b)
//                //{
//                //    foreach (CellHeld cell in bay)
//                //    {
//                //        foreach (ContainerUnit unit in cell)
//                //        {
//                string cmdString = string.Format("INSERT INTO Test (Alias , QQ) VALUES ('asdf', 'sdflj')");//", 'asdf'", "'40'"
//                OleDbCommand cmd = new OleDbCommand(cmdString, this.dbConnection);

//                cmd.ExecuteNonQuery();
//                //            }
//                //        }
//                //    }
//                //}
//            }
//            catch (Exception ex)
//            {
//                throw new Exception(ex.Message);
//            }
//            finally
//            {
//                this.dbConnection.Close();
//            }

//        }
//    }
//}