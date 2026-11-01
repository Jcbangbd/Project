using UnityEngine;
using System.Collections;

namespace ZPMC.VR.Unity
{
    public class SimMove : SimBase
    {
        /// <summary>
        /// 最大速度
        /// </summary>
        public float MaxSpeed;
        /// <summary>
        /// 加速时间
        /// </summary>
        public float AccTime;

        protected float keyTimeAcc;
        protected float keyTimeDec;
        protected float lengthAcc;
        protected float modifyAccTime;
        protected float modifyAcc;

        protected delegate void KinematicUpdate();

        protected KinematicUpdate TranslateMethod;

        public SimMove(Transform t, DOF dof)
            : base(t, dof)
        {
            if (dof == DOF.TranslationX)
            {
                this.TranslateMethod = this.TranslateX;
            }
            else if (dof == DOF.TranslationY)
            {
                this.TranslateMethod = this.TranslateY;
            }
            else if (dof == DOF.TranslationZ)
            {
                this.TranslateMethod = this.TranslateZ;
            }
            else
                throw new UnityException("Error");
        }

        public override void UserStart()
        {
        }

        public override void InitialParse(SimRecord record)
        {
            base.InitialParse(record);

            this.beginGauge = ParseHelper.TryAsSingle(record.StartPosition);
            this.finishGauge = ParseHelper.TryAsSingle(record.EndPosition);

            float currnetTime = TimeHelper.Instance.RectifiedTime;
            if (currnetTime >= this.finishTime)
            {
                this.CompletedKinematic();
                return;
            }

            float spanTime = finishTime - beginTime;
            float spanGauge = Mathf.Abs(finishGauge - beginGauge);
            float normalTime = this.CalculateTime(beginGauge, finishGauge);
            float ratio = spanTime / normalTime;
            this.modifyAccTime = this.AccTime * ratio;

            if (spanGauge > this.MaxSpeed * this.AccTime)
            {
                keyTimeAcc = this.beginTime + this.modifyAccTime;
                keyTimeDec = finishTime - this.modifyAccTime;
                speed = (spanGauge - this.MaxSpeed * this.AccTime) / (spanTime - 2 * this.modifyAccTime);
                modifyAcc = this.MaxSpeed / this.AccTime / ratio / ratio;
                lengthAcc = 0.5f * this.MaxSpeed * this.AccTime;
            }
            else
            {
                keyTimeAcc = this.beginTime + spanTime / 2;
                keyTimeDec = finishTime - spanTime / 2;
                speed = 0;
                modifyAcc = spanGauge / (spanTime / 2 * spanTime / 2);
                lengthAcc = spanGauge / 2;
            }

            if (finishGauge < beginGauge)
            {
                modifyAcc = -modifyAcc;
                speed = -speed;
                lengthAcc = -lengthAcc;
            }
        }

        public override void UserUpdate(ref float currentTime)
        {
            if (!this.IsChanged)
                return;

            if (currentTime <= keyTimeAcc)
            {
                float timeCumulate = currentTime - this.beginTime;
                this.currentValue = this.beginGauge + 0.5f * modifyAcc * timeCumulate * timeCumulate;
            }
            else if (currentTime < keyTimeDec)
            {
                float timeCumulate = currentTime - keyTimeAcc;//
                this.currentValue = this.beginGauge + lengthAcc + this.speed * timeCumulate;
            }
            else if (currentTime < this.finishTime)
            {
                float timeCumulate = this.finishTime - currentTime;// - this.startTime
                this.currentValue = this.finishGauge - 0.5f * modifyAcc * timeCumulate * timeCumulate;
            }
            else
            {
                this.CompletedKinematic();
                return;
            }

            this.TranslateMethod();
        }

        public override void CompletedKinematic()
        {
            this.currentValue = this.finishGauge;
            this.TranslateMethod();

            this.IsChanged = false;
        }

        protected void TranslateX()
        {
            this.transform.position = new Vector3(this.currentValue, this.transform.position.y, this.transform.position.z);
        }

        protected void TranslateY()
        {
            this.transform.position = new Vector3(this.transform.position.x, this.currentValue, this.transform.position.z);//
        }

        protected void TranslateZ()
        {
            this.transform.position = new Vector3(this.transform.position.x, this.transform.position.y, this.currentValue);//
        }

        public float CalculateTime(float spanGauge)
        {
            float absGauge = Mathf.Abs(spanGauge);
            if (absGauge > this.MaxSpeed * this.AccTime)
            {
                return 2 * this.AccTime + (absGauge - this.MaxSpeed * this.AccTime) / MaxSpeed;
            }
            else
            {
                return 2 * Mathf.Sqrt(absGauge * this.AccTime / this.MaxSpeed);
            }
        }

        public float CalculateTime(float startGauge, float endGauge)
        {
            float spanGauge = endGauge - startGauge;
            return CalculateTime(spanGauge);
        }
    }
}