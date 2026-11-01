using UnityEngine; 
using System;
using System.Xml.Serialization;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace ZPMC.VR.Unity
{
	public class FileHelper
	{
#if UNITY_EDITOR
        private static string DataPath = Application.dataPath + "/StreamingAssets";//+ "/my.xml";
#elif UNITY_IPHONE
 private static string DataPath = Application.dataPath +"/Raw";//+"/my.xml";
#elif UNITY_ANDROID
 private static string DataPath = "jar:file://" + Application.dataPath + "!/assets/";//+"/my.xml;
#elif UNITY_STANDALONE_WIN
      private static string DataPath = Application.dataPath + "/StreamingAssets";
#else
        private static string DataPath = Application.dataPath + "/StreamingAssets" ;
#endif

        public static string GetDataPath()
        {            
            return DataPath;//return "E:\\Temp\\T02\\XOCT_Data\\StreamingAssets";
        }

        /// <summary>
        /// Private constructor to prevent instantiation.
        /// </summary>
        private FileHelper()
        {
        }

        // Here we serialize our Reward object of reward   
        public static bool SerializeObject(string fileName, object pObject, Type pType)
        {
            if (String.IsNullOrEmpty(fileName))
            {
                Debug.Log("Serializing path is NULL");
                return false;
            }
            try
            {
                XmlSerializer xmlFormater = new XmlSerializer(pType);//
                using (Stream fStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    //XmlWriter writer = XmlWriter.Create(fStream);
                    //xmlFormater.Serialize(writer, pObject);
                    xmlFormater.Serialize(fStream, pObject);
                }
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Deserialize Error!\n FileName is {0} Type is {1} \n {2}", fileName, pType.ToString(), ex.Message));
            }
        }

        // Here we deserialize it back into its original form   
        public static object DeserializeObject(string fileName, Type pType)
        {
            if (string.IsNullOrEmpty(fileName) || !File.Exists(fileName))
            {
                throw new Exception("DeSerializing path is NULL");
                //return null;
            }
            try
            {
                if (Application.platform != RuntimePlatform.Android)
                {
                    XmlSerializer xmlFormater = new XmlSerializer(pType);
                    using (Stream fStream = File.OpenRead(fileName))// new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        //XmlReader reader = XmlReader.Create(fStream);
                        //return xmlFormater.Deserialize(reader);
                        return xmlFormater.Deserialize(fStream);
                    }
                }
                else
                {
                    WWW www = new WWW(fileName);
                    while (!www.isDone)
                    {
                    }
                    //yield return www;
                    return www.assetBundle.mainAsset; //还有问题
                }
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Deserialize Error!\n FileName is {0} Type is {1} \n {2}", fileName, pType.ToString(),ex.Message));
            }
        }

        /// <summary>
        /// 保存数据
        /// </summary>
        /// <param name="fileName">File Name</param>
        /// <param name="obj">Object</param>
        /// <returns>Bool</returns>
        public static bool Save(String fileName, System.Object obj)
        {
            if (String.IsNullOrEmpty(fileName))
                return false;

            try
            {
                BinaryFormatter binaryFormatter = new BinaryFormatter();
                FileStream fs = new FileStream(fileName, FileMode.OpenOrCreate);
                binaryFormatter.Serialize(fs, obj);

                fs.Close();
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);//return false;
            }
        }

        /// <summary>
        /// 读数据
        /// </summary>
        /// <param name="fileName">File Name</param>
        /// <returns>Object</returns>
        public System.Object Load(String fileName)
        {
            if (String.IsNullOrEmpty(fileName))
                return null;

            try
            {
                BinaryFormatter binaryFormatter = new BinaryFormatter();

                using (FileStream fs = new FileStream(fileName, FileMode.Open))
                {
                    System.Object objRet = binaryFormatter.Deserialize(fs);
                    return objRet;
                }
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Read data error!\n {0}", ex.Message));
            }
        }


        //public class SysData : ScriptableObject
        //{
        //    public List<Vector3> content;
        //} 

        //public class Export
        //{

        //    [MenuItem("Assets/Export")]
        //    public static void Execute()
        //    {

        //        //实例化SysData               

        //        SysData sd = ScriptableObject.CreateInstance<SysData>();

        //        //随便设置一些数据给content                 

        //        sd.content = new List<Vector3>();

        //        sd.content.Add(new Vector3(1, 2, 3));

        //        sd.content.Add(new Vector3(4, 5, 6));

        //        // SysData将创建为一个对象，这时在project面板上会看到这个对象。                 

        //        string p = "Assets/SysData.asset";

        //        AssetDatabase.CreateAsset(sd, p);

        //        Object o = AssetDatabase.LoadAssetAtPath(p, typeof(SysData));

        //        //打包为SysData.assetbundle文件。                 

        //        BuildPipeline.BuildAssetBundle(o, null, "SysData.assetbundle");

        //        //删除面板上的那个临时对象               

        //        AssetDatabase.DeleteAsset(p);

        //    }

        //}  

        //IEnumerator Start()
        //{
        //    WWW www = new WWW("file://" + Application.dataPath + "/../SysData.assetbundle");
        //    yield return www;

        //    //转换资源为SysData，这个sd对象将拥有原来在编辑器中设置的数据。                
        //    SysData sd = www.assetBundle.mainAsset as SysData;
        //    //如打印sd.content[0]，将得到Vector3(1,2,3);               
        //    print(sd.content[0]);
        //} 
	}
}
