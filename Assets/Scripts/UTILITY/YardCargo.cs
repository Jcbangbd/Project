using System;
using System.Collections;
using System.Collections.Generic;

namespace ZPMC.VR.Unity
{
    [Serializable]
    public class YardCargo
    {
        public int ID { get; set; }
        public string CtnID { get; set; }
        public string CtnMode { get; set; }
        public float CtnLength { get; set; }
        public float CtnHeight { get; set; }
        public float CtnWeight { get; set; }
        public string YardCode { get; set; }
        public bool IsEmpty { get; set; }
        public int ShipOwner { get; set; }
    }

    [Serializable]
    public class YardStorage 
    {
        public List<YardCargo> CargoMarshal { get; set; }

        public YardStorage()
        {
            this.CargoMarshal = new List<YardCargo>();
        }

        public void LoadStorage()
        {
            foreach (YardCargo item in CargoMarshal)
            {
                string blockName = string.Empty;
                int bay, column, tier;
                try
                {
                    BlockBuffer.ParseYardCode(item.YardCode, out blockName, out bay, out column, out tier);
                    BlockBuffer block = BlockManager.Instance.FindBlockBuffer(blockName);
                    int owner = ContainerSpawner.Instance.GetRandomOwnerIndex();
                    float length = item.CtnLength;
                    float hight = item.CtnHeight;
                    Container container = ContainerSpawner.Instance.SpawnContainer(owner, length, hight);
                    container.name = item.CtnID;
                    block.InitialContainer(bay, column, tier, container);//.OnInitialContainer
                }
                catch (Exception ex)
                {
                    throw new Exception(string.Format("Load Yard Container Error!\n SlotCode={0}\n", item.YardCode) + ex.Message);
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
