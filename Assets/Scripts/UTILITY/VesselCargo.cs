using System;
using System.Collections;
using System.Collections.Generic;

namespace ZPMC.VR.Unity
{
    [Serializable]
	public class VesselCargo
	{
        public int ID { get; set; }
        public string CtnID { get; set; }
        public string CtnMode { get; set; }
        public float CtnLength { get; set; }
        public float CtnHeight { get; set; }
        public float CtnWeight { get; set; }
        public string VesselName { get; set; }
        public string ShipCode { get; set; }
        public bool IsEmpty { get; set; }
        public int ShipOwner { get; set; }
	}

    [Serializable]
    public class VesselStorage //: IEnumerable
    {
        public List<VesselCargo> CargoMarshal { get; set; }

        public VesselStorage()
        {
            this.CargoMarshal = new List<VesselCargo>();
        }

        public void LoadStorage()
        {
            foreach (VesselCargo item in CargoMarshal)
            {
                string vesselName = item.VesselName;
                string vesselCode = item.ShipCode;
                int bay, column, tier;
                try
                {
                    VesselBuffer.ParseVesselCode(vesselCode, out bay, out column, out tier);
                    VesselBuffer buffer = VesselManager.GlobalInstance().FindVesselBuffer(vesselName);
                    int owner = ContainerSpawner.Instance.GetRandomOwnerIndex();
                    float length = item.CtnLength;
                    float hight = item.CtnHeight;
                    Container container = ContainerSpawner.Instance.SpawnContainer(owner, length, hight);
                    container.name = item.CtnID;
                    buffer.InitialContainer(bay, column, tier, container);
                }
                catch (Exception ex)
                {
                    throw new Exception(string.Format("Load Vessel Container Error!\n SlotCode={0}\n", item.ShipCode) + ex.Message);
                }
            }
        }

        public void Clear()
        {
            this.CargoMarshal.Clear();
            this.CargoMarshal = null;
        }
    }
}
