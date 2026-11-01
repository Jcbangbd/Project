using UnityEngine;
using System.Collections;

namespace ZPMC.VR.Unity
{
    public class SimRun : SimMove 
    {
        private float modifyDec = 0;

        private float currentTargetSpeed = 0; //速度，带符号的啊
        private float lastTargetSpeed = 0;//速度，带符号的啊

        public SimRun(Transform t, DOF dof)
            : base(t, dof)
        {
        }

        public override void UserStart()
        {
        }

        public override void InitialParse(SimRecord record)
        {
            if (this.IsChanged)
                this.CompletedKinematic();

            this.IsChanged = true;

            this.beginTime = record.StartTime;
            this.finishTime = record.EndTime;
            this.beginGauge = ParseHelper.TryAsSingle(record.StartPosition);
            this.finishGauge = ParseHelper.TryAsSingle(record.EndPosition);
            if (this.beginGauge == this.finishGauge)
            {
                this.CompletedKinematic();
                return;
            }
            float initialSpeed = 0;
            float targetSpeed = 0;
            if (!string.IsNullOrEmpty(record.Tag))
            {
                string[] subStr = record.Tag.Split('|');
                initialSpeed = ParseHelper.TryAsSingle(subStr[0]);
                targetSpeed = ParseHelper.TryAsSingle(subStr[1]);
            }
            float currnetTime = TimeHelper.Instance.RectifiedTime;
            if (currnetTime >= this.finishTime)
            {
                this.CompletedKinematic();
                return;
            }

            int sign = finishGauge >= beginGauge ? 1 : -1; //方向标志
            this.lastTargetSpeed = initialSpeed * sign >= 0 ? initialSpeed : -initialSpeed;
            this.currentTargetSpeed = targetSpeed * sign >= 0 ? targetSpeed : -targetSpeed;

            float spanTime = finishTime - beginTime;
            float spanGauge = Mathf.Abs(finishGauge - beginGauge);
            float tempTime1, refGauge1, tempTime2, refGauge2;
            tempTime1 = Mathf.Abs(this.MaxSpeed - Mathf.Abs(this.lastTargetSpeed)) / this.MaxSpeed / this.AccTime;
            refGauge1 = 0.5f * tempTime1 * (this.MaxSpeed + Mathf.Abs(this.lastTargetSpeed));
            tempTime2 = Mathf.Abs(targetSpeed - this.MaxSpeed) / this.MaxSpeed / this.AccTime;
            refGauge2 = 0.5f * tempTime2 * (this.MaxSpeed + targetSpeed);
            if (spanGauge >= refGauge1 + refGauge2) //先往最大速度上靠
            {
                float normalTime = tempTime1 + tempTime2 + (spanGauge - refGauge1 - refGauge2) / this.MaxSpeed;
                float ratio = spanTime / normalTime;

                this.keyTimeAcc = beginTime + tempTime1 * ratio;
                this.keyTimeDec = finishTime - tempTime2 * ratio;
                this.speed = sign * this.MaxSpeed / ratio;
                this.lastTargetSpeed = this.lastTargetSpeed / ratio;
                //this.modifyAcc = tempTime1 > 0 ? sign * 2 * refGauge1 / (tempTime1 * tempTime1 * ratio * ratio) - 2 * this.lastTargetSpeed / (tempTime1 * ratio) : 0;
                //this.modifyDec = tempTime2 > 0 ? 2 * this.currentTargetSpeed / (tempTime2 * ratio) - sign * 2 * refGauge2 / (tempTime2 * tempTime2 * ratio * ratio) : 0;
                this.modifyAcc = (this.speed - this.lastTargetSpeed) / tempTime1 / ratio;
                this.modifyDec = (this.currentTargetSpeed / ratio - this.speed) / tempTime2 / ratio;

                this.lengthAcc = beginGauge + refGauge1 * sign;
            }
            else
            {
                this.keyTimeAcc = beginTime;
                this.keyTimeDec = finishTime;
                this.modifyAcc = 0;
                this.modifyDec = 0;
                this.speed = sign * spanGauge / spanTime;
                this.lengthAcc = beginGauge;
            }
        }

        public override void UserUpdate(ref float currentTime)
        {
            if (!this.IsChanged)
                return;

            if (currentTime < keyTimeAcc) //
            {
                float timeCumulate = currentTime - this.beginTime;
                this.currentValue = this.beginGauge + timeCumulate * this.lastTargetSpeed + 0.5f * timeCumulate * timeCumulate * modifyAcc;
            }
            else if (currentTime < keyTimeDec)
            {
                float timeCumulate = currentTime - keyTimeAcc;
                this.currentValue = this.lengthAcc + this.speed * timeCumulate;
            }
            else if (currentTime < this.finishTime)
            {
                //float timeCumulate = this.finishTime - currentTime;
                //this.currentValue = finishGauge - timeCumulate * this.currentTargetSpeed + 0.5f * timeCumulate * timeCumulate * modifyDec;
                float timeCumulate = currentTime - keyTimeDec;
                this.currentValue = this.lengthAcc + (currentTime - keyTimeAcc) * this.speed + 0.5f * timeCumulate * timeCumulate * modifyDec;
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

        public float CalculateTime(float startGauge, float endGauge, float startSpeed, float finishSpeed)
        {
            float spanGauge = Mathf.Abs(endGauge - startGauge);
            float tempTime1, refGauge1, tempTime2, refGauge2;
            tempTime1 = Mathf.Abs(this.MaxSpeed - startSpeed) / this.MaxSpeed / this.AccTime;
            refGauge1 = 0.5f * tempTime1 * (this.MaxSpeed + startSpeed);
            tempTime2 = Mathf.Abs(finishSpeed - this.MaxSpeed) / this.MaxSpeed / this.AccTime;
            refGauge2 = 0.5f * tempTime2 * (this.MaxSpeed + finishSpeed);

            if (spanGauge >= refGauge1 + refGauge2) //先往最大速度上靠
                return tempTime1 + tempTime2 + (spanGauge - refGauge1 - refGauge2) / this.MaxSpeed;
            else
                return 2 * spanGauge / this.MaxSpeed;
        }
    }
}