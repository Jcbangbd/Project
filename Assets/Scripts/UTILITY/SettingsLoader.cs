using System;
using System.Collections.Generic;

namespace ZPMC.VR.Unity
{
	public class SettingsLoader
	{
        private MapTableMarshal YardMaps;
        private MapTableMarshal ShipMaps;
        private YardStorage YardContainers;
        private VesselStorage VesselContainers;
        private PositionSetting PositionSet;

        public static string fileNameYardMap = "/YardMap.xml";
        public static string fileNameShipMap = "/ShipMap.xml";
        public static string fileNameYardFreight = "/YardFreight.xml";
        public static string fileNameVesselFreight = "/VesselFreight.xml";
        public static string fileNameEquipPosition = "/EquipPosition.xml";

        #region Constructor
        private static SettingsLoader localObject = null;
        private SettingsLoader()
        {
        }
        public static SettingsLoader Instance()
        {
            if (localObject == null)
            {
                localObject = new SettingsLoader();
            }
            return localObject;
        }
        #endregion

        public void LoadingSettings()
        {
            string filePath = FileHelper.GetDataPath(), fullFileName;

            fullFileName = filePath +  fileNameYardMap;
            MapTemplate yardTemplate = (MapTemplate)FileHelper.DeserializeObject(fullFileName, typeof(MapTemplate));
            this.YardMaps = new MapTableMarshal(yardTemplate);

            fullFileName = filePath + fileNameShipMap;
            MapTemplate shipTemplate = (MapTemplate)FileHelper.DeserializeObject(fullFileName, typeof(MapTemplate));
            this.ShipMaps = new MapTableMarshal(shipTemplate);

            fullFileName = filePath + fileNameEquipPosition;
            this.PositionSet = (PositionSetting)FileHelper.DeserializeObject(fullFileName, typeof(PositionSetting));
            fullFileName = filePath + fileNameYardFreight;
            this.YardContainers = (YardStorage)FileHelper.DeserializeObject(fullFileName, typeof(YardStorage));
            fullFileName = filePath + fileNameVesselFreight;
            this.VesselContainers = (VesselStorage)FileHelper.DeserializeObject(fullFileName, typeof(VesselStorage));
        }

        public void InitialSettings()
        {
            this.InitialBlocks();
            this.InitialVessels();

            this.YardContainers.LoadStorage();
            this.YardContainers.Clear();
            this.VesselContainers.LoadStorage();
            this.VesselContainers.Clear();
            this.PositionSet.InitilizePosition();
            this.PositionSet.Clear();

            this.YardMaps = null;
            this.ShipMaps = null;

            localObject = null;
        }

        private void InitialBlocks()
        {
            foreach (InstanceInfo item in this.YardMaps.GetInstanceInfos())
            {
                try
                {
                    /*BlockBuffer buffer = BlockManager.Instance.FindOrAddBuffer(item.ObjectName);
                    buffer.BindingMap = this.YardMaps.GetMapTable(item.BayFunction, item.ColumnFunction);//若场景中已存在该对象，会覆盖原地图
                    buffer.transform.position = new UnityEngine.Vector3(item.PositionX, item.PositionY, item.PositionZ);
                    buffer.transform.rotation = UnityEngine.Quaternion.Euler(0, item.EulerAngleY, 0);//, UnityEngine.Space.World*/
                    BlockBuffer buffer = BlockManager.Instance.FindOrAddBuffer(item.ObjectName);

                    MapTable mapTable = this.YardMaps.GetMapTable(
                        item.BayFunction,
                        item.ColumnFunction
                    );

                    // 只对 A40 补齐 1-125 的贝位坐标
                    if (item.ObjectName == "A40" && mapTable != null)
                    {
                        mapTable.FillMissingBays(1, 125);
                    }

                    buffer.BindingMap = mapTable;

                    buffer.transform.position = new UnityEngine.Vector3(
                        item.PositionX,
                        item.PositionY,
                        item.PositionZ
                    );

                    buffer.transform.rotation = UnityEngine.Quaternion.Euler(
                        0,
                        item.EulerAngleY,
                        0
                    );
                }
                catch (Exception ex)
                {
                    throw new Exception("Initial Block Buffer error!\n" + ex.Message );
                }
            }
        }

        private void InitialVessels()
        {
            foreach (InstanceInfo item in this.ShipMaps.GetInstanceInfos())
            {
                Vessel vessel = VesselManager.GlobalInstance().FindVessel(item.ObjectName);
                if (!vessel)
                {
                    vessel = VesselManager.GlobalInstance().CreateVessel(item.PrefabAlias);
                    vessel.name = item.ObjectName;
                    VesselManager.GlobalInstance().AddVessel(vessel, vessel.nestedBuffer);
                }
                else
                {
                    UnityEngine.GameObject obj = VesselManager.GlobalInstance().GetPrefab(item.PrefabAlias);
                    vessel.transform.rotation = obj.transform.rotation;
                }
                vessel.transform.position = new UnityEngine.Vector3(item.PositionX, item.PositionY, item.PositionZ);
                if (item.EulerAngleY != 0)
                    vessel.transform.Rotate(0, item.EulerAngleY, 0, UnityEngine.Space.World);                
                VesselBuffer buffer = vessel.nestedBuffer;

                buffer.BindingMap = this.ShipMaps.GetMapTable(item.BayFunction, item.ColumnFunction);
                buffer.transform.position = vessel.transform.position + UnityEngine.Quaternion.Euler(0, item.EulerAngleY, 0)* new UnityEngine.Vector3(item.OffsetX, item.OffsetY, item.OffsetZ);
            }
        }

        public MapTable GetYardMapByStackName(string blockName)
        {
            if (this.YardMaps != null)
                return this.YardMaps.GetMapTableByName(blockName);
            else
                return null;
        }

        public MapTable GetMapByTemplateName(string vesselName)
        {
            if (this.ShipMaps != null)
                return this.ShipMaps.GetMapTableByName(vesselName);
            else
                return null;
        }

        public void Clear()
        {
            //this.settings = null;
            //this.ShipMaps = null;
            //this.YardMaps = null;
            localObject = null;
        }

	}

}
