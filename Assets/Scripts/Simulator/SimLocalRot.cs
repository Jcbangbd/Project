using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZPMC.VR.Unity
{
    public class SimLocalRot : SimBase
	{
        /// <summary>
        /// 最大速度
        /// </summary>
        public float MaxSpeed = 10f;
        /// <summary>
        /// 加速时间
        /// </summary>
        public float AccTime = 2f;

        private Vector3 KinematicVector;
        private Quaternion beginQ;
        private Quaternion finishQ;

        public SimLocalRot(Transform t, DOF dof)
            : base(t, dof)
        {
            //this.originAngles = this.transform.eulerAngles;
            if (this.dof == DOF.LocalRotX)
            {
                this.KinematicVector = new Vector3(1, 0, 0);
            }
            else if (this.dof == DOF.LocalRotY)
            {
                this.KinematicVector = new Vector3(0, 1, 0);
            }
            else if (this.dof == DOF.LocalRotZ)
            {
                this.KinematicVector = new Vector3(0, 0, 1);
            }
            else
                throw new UnityException("Error");
        }

        public override void UserStart()
        {
            throw new NotImplementedException();
        }
        
        public override void InitialParse(SimRecord record)
        {
            base.InitialParse(record);
            this.beginGauge = ParseHelper.TryAsSingle(record.StartPosition);
            this.finishGauge = ParseHelper.TryAsSingle(record.EndPosition);
            if (this.beginGauge == this.finishGauge)
            {
                this.CompletedKinematic();
                return;
            }
            this.beginQ = Quaternion.Euler(this.KinematicVector * this.beginGauge);
            this.finishQ = Quaternion.Euler(this.KinematicVector * this.finishGauge);

            float currnetTime = TimeHelper.Instance.RectifiedTime;
            if (currnetTime >= this.finishTime)
            {
                this.CompletedKinematic();
                return;
            }
        }

        public override void UserUpdate(ref float currentTime)
        {
            if (!IsChanged)
                return;

            if (currentTime < this.finishTime)
                this.transform.localRotation = Quaternion.Lerp(this.beginQ, this.finishQ, (currentTime - this.beginTime) / (this.finishTime - this.beginTime));
            else
                this.CompletedKinematic();
        }

        public override void CompletedKinematic()
        {
            this.currentValue = finishGauge;
            this.transform.localRotation = finishQ;//Quaternion.Euler(this.KinematicVector * this.currentValue);//暂时只支持一个旋转

            this.IsChanged = false;
        }
	}
}
