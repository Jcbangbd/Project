using UnityEngine;
using System.Collections;

namespace ZPMC.VR.Unity
{
    public abstract class SimBase 
    {
        protected DOF dof;
        /// <summary>
        /// 在设备中的编号
        /// </summary>
        public int IndexInEquip;

        protected bool IsChanged = false;
        protected Transform transform;

        protected float beginGauge;
        protected float finishGauge;
        protected float beginTime;
        protected float finishTime;
        protected float currentValue = 0;
        protected float speed;

        public SimBase(Transform t, DOF dof)
        {
            this.transform = t;
            this.dof = dof;
        }

        public abstract void UserStart();

        public virtual void InitialParse(SimRecord record)
        {
            if (this.IsChanged)
                this.CompletedKinematic();

            this.beginTime = record.StartTime;
            this.finishTime = record.EndTime;

            this.IsChanged = true;
        }

        // Update is called once per frame
        public abstract void UserUpdate(ref float currentTime);

        public abstract void CompletedKinematic();

        public void TryCompleteKinematic(float currentTime)
        {
            if (this.IsChanged)
            {
                if (this.finishTime <= currentTime)
                    this.CompletedKinematic();
            }
        }

        //public virtual void SetValue(float currentValue)
        //{
        //}
    }
}