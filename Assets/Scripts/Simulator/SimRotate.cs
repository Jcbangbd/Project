using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZPMC.VR.Unity
{
    public class SimRotate : SimBase
	{
        /// <summary>
        /// 最大速度
        /// </summary>
        public float MaxSpeed = 10f;
        /// <summary>
        /// 加速时间
        /// </summary>
        public float AccTime = 2f;

        //private float keyTimeAcc;
        //private float keyTimeDec;
        //private float lengthAcc;

        //private float modifyAcc;
        private Vector3 KinematicVector;
        //private Vector3 originAngles;

        private Quaternion beginQ;
        private Quaternion finishQ;

        public SimRotate(Transform t, DOF dof)
            : base(t, dof)
        {
            //this.originAngles = this.transform.eulerAngles;
            if (this.dof == DOF.RotationX)
            {
                this.KinematicVector = new Vector3(1, 0, 0);
            }
            else if (this.dof == DOF.RotationY)
            {
                this.KinematicVector = new Vector3(0, 1, 0);
            }
            else if (this.dof == DOF.RotationZ)
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

            //int sign = finishGauge >= beginGauge ? 1 : -1;
            //float spanTime = finishTime - beginTime;
            //float spanGauge = Mathf.Abs(finishGauge - beginGauge);

            //if (spanGauge > this.MaxSpeed * this.AccTime)
            //{
            //    float normalTime = 2 * this.AccTime + (spanGauge - this.MaxSpeed * this.AccTime) / this.MaxSpeed;
            //    float ratio = spanTime / normalTime;

            //    this.keyTimeAcc = beginTime + this.AccTime * ratio;
            //    this.keyTimeDec = finishTime - this.AccTime * ratio;
            //    this.speed = sign * this.MaxSpeed / ratio;
            //    this.modifyAcc = sign * this.MaxSpeed * this.AccTime / (this.AccTime * this.AccTime * ratio * ratio);//this.maxSpeed / this.accTime / ratio
            //    this.lengthAcc = sign * 0.5f * this.MaxSpeed * this.AccTime;
            //}
            //else
            //{
            //    this.keyTimeAcc = beginTime + spanTime / 2;
            //    this.keyTimeDec = finishTime - spanTime / 2;
            //    this.speed = 0;
            //    this.modifyAcc = sign * 4 * spanGauge / (spanTime * spanTime);
            //    this.lengthAcc = sign * spanGauge / 2;
            //}
        }

        public override void UserUpdate(ref float currentTime)
        {
            if (!IsChanged)
                return;

            //if (currentTime < keyTimeAcc) //
            //{
            //    float timeCumulate = currentTime - this.beginTime;//
            //    this.currentValue = this.beginGauge + 0.5f * modifyAcc * timeCumulate * timeCumulate;
            //}
            //else if (currentTime <= keyTimeDec) //
            //{
            //    float timeCumulate = currentTime - keyTimeAcc;//
            //    this.currentValue = this.beginGauge + lengthAcc + this.speed * timeCumulate;
            //}
            //else if (currentTime < this.finishTime) //
            //{
            //    float timeCumulate = this.finishTime - currentTime;//
            //    this.currentValue = this.finishGauge - 0.5f * modifyAcc * timeCumulate * timeCumulate;
            //}
            //else
            //{
            //    this.CompletedKinematic();
            //    return;
            //}

            //this.transform.rotation = Quaternion.Euler(this.KinematicVector * this.currentValue);//暂时只支持一个旋转

            if (currentTime < this.finishTime)
                this.transform.rotation = Quaternion.Lerp(this.beginQ, this.finishQ, (currentTime - this.beginTime) / (this.finishTime - this.beginTime));
            else
                this.CompletedKinematic();
        }

        public override void CompletedKinematic()
        {
            this.currentValue = finishGauge;
            this.transform.rotation = finishQ;// Quaternion.Euler(this.KinematicVector * this.currentValue);//暂时只支持一个旋转

            this.IsChanged = false;
        }

        //public override void SetValue(float currentValue)
        //{
        //    this.transform.rotation = Quaternion.Euler(this.KinematicVector * this.currentValue + this.originAngles);
        //}

	}
}
