using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace ZPMC.VR.Unity
{
    //[Serializable]
    public class CellHeld : IEnumerable
	{
        protected SortedList<int, Container> ctnMarshal;
        public int ColumnNO { get; set; }
        public BayHeld AttchedBay { get; set; }

        /// <summary>
        /// 集装箱数量
        /// </summary>
        public int Count
        {
            get { return this.ctnMarshal.Count; }
        }
             
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="columnNO">Column NO</param>
        /// <param name="offset">Vector3</param>
        public CellHeld(int columnNO)
        {
            this.ctnMarshal = new SortedList<int, Container>();

            this.ColumnNO = columnNO;
        }

        /// <summary>
        /// 获取当前Cell上端的物理坐标
        /// </summary>
        /// <returns></returns>
        public virtual float GetTierPosition(int tier)
        {
            float height = 0;
            for (int i = 1; i < tier; i++)
            {
                if (this.ctnMarshal.ContainsKey(i))
                    height += this.ctnMarshal[i].HeightMetre;
                else
                    height += Container.EightHalfFeet;
            }
            return height;
        }

        public void AddContainer(int tier, Container containerGO)
        {
            if (this.ctnMarshal.ContainsKey(tier))
            {
                Debug.Log(string.Format("There is a container = {0} in the slot when add a container={1} to a buffer.\n Tier={2},Column={3},Bay={4}",
                    this.ctnMarshal[tier].SlotFlag, containerGO.SlotFlag, tier, this.ColumnNO, this.AttchedBay.BayNO));
            }
            //for (int i = 1; i < tier; i++)
            //{
            //    if (!this.ctnMarshal.ContainsKey(i))
            //    {
            //        Debug.Log(string.Format("There isn't container under the slot.\n Tier={0},Column={1},Bay={2},Block={3}",
            //            tier, this.ColumnNO, this.AttchedBay.BayNO, this.AttchedBay.AttachedBlock.name));
            //        break;
            //    }
            //}
            this.ctnMarshal.Add(tier, containerGO);
            if (containerGO.HeightMetre != Container.EightHalfFeet)
            {
                this.AdjustAboveContainer(tier, containerGO.HeightMetre - Container.EightHalfFeet);
            }
        }

        protected virtual void AdjustAboveContainer(int tierNO, float offset)
        {
            for (int max = this.ctnMarshal.Count, tier0 = this.ctnMarshal.IndexOfKey(tierNO), i = tier0 + 1; i < max; i++)
            {
                Container container = this.ctnMarshal.Values.ElementAt(i);
                container.transform.Translate(0, offset, 0, Space.World);
            }
        }

        public Container FindContainer(int tier)
        {
            if (this.ctnMarshal.ContainsKey(tier))
                return this.ctnMarshal[tier];
            return null;
        }

        public Container RemoveContainer(int tier)
        {
            Container container = this.ctnMarshal[tier];
            this.ctnMarshal.Remove(tier);
            return container;
        }

        public void DestroyAllContainer()
        {
            foreach (var item in this.ctnMarshal.Values)
            {
                UnityEngine.GameObject.DestroyObject(item.gameObject, 0);//
            }
            this.ctnMarshal.Clear();
        }

        public IEnumerator GetEnumerator()
        {
            return this.ctnMarshal.Values.GetEnumerator();
        }
	}
}
