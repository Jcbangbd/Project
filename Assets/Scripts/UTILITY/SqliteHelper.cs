//using System;
//using System.Data;
//using Mono.Data.Sqlite;

//namespace ZPMC.VR.Unity
//{
//    public class SqliteHelper
//    {
//        private const string fileYardMapName = "/PortData.db";
//        private SqliteConnection dbConnection;//
//        private SqliteCommand dbCommand;
//        private SqliteDataReader reader;
//        private SqliteDataAdapter adapter;
//        private DataTable dataTable = null;

//        public static string PortDataFilePath
//        {
//            get { return FileHelper.GetDataPath() + fileYardMapName; }
//        }

//        public static SqliteHelper CreateDefaultAccess()
//        {
//            return new SqliteHelper(@"data source=" + SqliteHelper.PortDataFilePath);
//        }

//        public SqliteHelper(string connectionString)
//        {
//            OpenDB(connectionString);
//        }

//        /// <summary>
//        /// 打开数据源链接
//        /// </summary>
//        /// <returns>Orecle Connnetction</returns>
//        private void Open()
//        {
//            if (dbConnection.State == System.Data.ConnectionState.Closed)
//            {
//                try
//                {
//                    dbConnection.Open();//打开数据库连接
//                }
//                catch (Exception ex)
//                {
//                    throw new SqliteException(ex.Message);
//                }
//            }
//        }

//        private void OpenDB(string connectionString)
//        {
//            try
//            {
//                dbConnection = new SqliteConnection(connectionString);

//                dbConnection.Open();
//            }
//            catch (Exception ex)
//            {
//                throw new SqliteException(ex.Message);
//            }
//            //Debug.Log(connectionString);
//        }

//        public void CloseConnnection()
//        {
//            if (dbCommand != null)
//            {
//                dbCommand.Dispose();
//            }
//            dbCommand = null;

//            if (reader != null)
//            {
//                reader.Dispose();
//            }
//            reader = null;

//            if (dbConnection != null)
//            {
//                dbConnection.Close();

//            }
//            dbConnection = null;
//        }

//        public SqliteDataReader ExecuteQuery(string sqlQuery)
//        {
//            dbCommand = dbConnection.CreateCommand();
//            dbCommand.CommandText = sqlQuery;
//            reader = dbCommand.ExecuteReader();

//            return reader;
//        }

//        public SqliteDataReader CreateTable(string name, string[] col, string[] colType)
//        {
//            if (col.Length != colType.Length)
//            {
//                throw new SqliteException("columns.Length != colType.Length");
//            }

//            string query = "CREATE TABLE " + name + " (" + col[0] + " " + colType[0];
//            for (int i = 1; i < col.Length; ++i)
//            {
//                query += ", " + col[i] + " " + colType[i];
//            }

//            query += ")";
//            return ExecuteQuery(query);
//        }

//        public SqliteDataReader ReadFullTable(string tableName)
//        {
//            string query = "SELECT * FROM " + tableName;
//            return ExecuteQuery(query);
//        }

//        public DataTable ReadByDataTable(string tableName, string condition = "", string orderStr = "")
//        {
//            string query = "SELECT * FROM " + tableName;
//            if (condition != "")
//                query += condition;
//            if (orderStr != "")
//                query += orderStr;
//            dataTable = new DataTable();
//            Open();//打开数据连接
//            dbCommand = new SqliteCommand(query, dbConnection);
//            adapter = new SqliteDataAdapter();//query, dbConnection
//            adapter.SelectCommand = dbCommand;
//            adapter.Fill(dataTable);
//            //dataTable.TableName = tableName;

//            return dataTable;
//        }

//        public SqliteDataReader InsertInto(string tableName, string[] values)
//        {
//            string query = "INSERT INTO " + tableName + " VALUES (" + values[0];
//            for (int i = 1; i < values.Length; ++i)
//            {
//                query += ", " + values[i];
//            }
//            query += ")";
//            return ExecuteQuery(query);
//        }

//        public SqliteDataReader InsertIntoSpecific(string tableName, string[] cols, string[] values)
//        {
//            if (cols.Length != values.Length)
//            {
//                throw new SqliteException("columns.Length != values.Length");
//            }

//            string query = "INSERT INTO " + tableName + "(" + cols[0];
//            for (int i = 1; i < cols.Length; ++i)
//            {
//                query += ", " + cols[i];
//            }
//            query += ") VALUES (" + values[0];

//            for (int i = 1; i < values.Length; ++i)
//            {
//                query += ", " + values[i];
//            }

//            query += ")";
//            return ExecuteQuery(query);
//        }
        
//        public SqliteDataReader UpdateInto(string tableName, string[] cols, string[] colsvalues, string selectkey, string selectvalue)
//        {
//            string query = "UPDATE " + tableName + " SET " + cols[0] + " = " + colsvalues[0];
//            for (int i = 1; i < colsvalues.Length; ++i)
//            {
//                query += ", " + cols[i] + " =" + colsvalues[i];
//            }

//            query += " WHERE " + selectkey + " = " + selectvalue + " ";

//            return ExecuteQuery(query);
//        }

//        public void UpdateTable()
//        {
//            try
//            {
//                Open();//打开数据连接
//                SqliteCommandBuilder thisBuilder = new SqliteCommandBuilder(adapter);//这句代码必须要的，否则出错
//                thisBuilder.DataAdapter.Update(dataTable);
//                //this.adapter.Update(dataTable);
//            }
//            catch (Exception ex)
//            {
//                throw new Exception(ex.Message + "\n" + adapter.SelectCommand.ToString());
//            }
//        }

//        public SqliteDataReader Delete(string tableName, string[] cols, string[] colsvalues)
//        {
//            string query = "DELETE FROM " + tableName + " WHERE " + cols[0] + " = " + colsvalues[0];

//            for (int i = 1; i < colsvalues.Length; ++i)
//            {

//                query += " or " + cols[i] + " = " + colsvalues[i];
//            }
//            return ExecuteQuery(query);
//        }
        
//        public SqliteDataReader DeleteContents(string tableName)
//        {
//            string query = "DELETE FROM " + tableName;
//            return ExecuteQuery(query);
//        }

//        public SqliteDataReader SelectWhere(string tableName, string[] items, string[] col, string[] operation, string[] values)
//        {
//            if (col.Length != operation.Length || operation.Length != values.Length)
//            {
//                throw new SqliteException("col.Length != operation.Length != values.Length");
//            }

//            string query = "SELECT " + items[0];
//            for (int i = 1; i < items.Length; ++i)
//            {
//                query += ", " + items[i];
//            }

//            query += " FROM " + tableName + " WHERE " + col[0] + operation[0] + "'" + values[0] + "' ";
//            for (int i = 1; i < col.Length; ++i)
//            {
//                query += " AND " + col[i] + operation[i] + "'" + values[0] + "' ";
//            }

//            return ExecuteQuery(query);
//        }
//    }
//}
