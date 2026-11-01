using System;
using System.Collections.Generic;

using UnityEngine;

namespace ZPMC.VR.Unity
{
    public class SimSwerve : SimBase
	{
        private int swerveType;
        private int signX;
        private int signZ;
        private int signY;
        private float ratioX;
        private float ratioZ;
        const float StandardSwerveTime = 9.051589f;
        const float StandardSwerveRaidus = 16f;
        private float beginX, finishX;
        private float beginZ, finishZ;

        private Vector3 originRotation;

        public float SwerveFunctionIn(float t)
        {
            return -0.0001807f * Mathf.Pow(t, 5) + 0.0035237f * Mathf.Pow(t, 4) - 0.0269338f * Mathf.Pow(t, 3) - 0.0478310f * Mathf.Pow(t, 2) + 3.0059467f * t + 0.0137327f;
        }

        public float SwerveFunctionOut(float t)
        {
            return -0.0001804f * Mathf.Pow(t, 5) + 0.0046458f * Mathf.Pow(t, 4) - 0.0473295f * Mathf.Pow(t, 3) + 0.3868312f * Mathf.Pow(t, 2) - 0.0919002f * t - 0.0037678f;
        }

        public float SwerveAngle(float t)
        {
            float temp = 0.0000080f * Mathf.Pow(t, 6) + 0.0098691f * Mathf.Pow(t, 5) - 0.2260161f * Mathf.Pow(t, 4) + 1.4931703f * Mathf.Pow(t, 3) - 1.6945395f * Mathf.Pow(t, 2) + 4.0048954f * t - 0.8215796f;
            return Mathf.Clamp(temp, 0, 90);
        }

        public SimSwerve(Transform t, DOF dof)
            : base(t, dof)
        {
            EquipBase equip = this.transform.gameObject.GetComponent<EquipBase>();
            if (equip.GetType() == typeof(AGV))
                this.originRotation = new Vector3(-90,0,0);// t.rotation.eulerAngles - new Vector3(0, 90, 0);
            else
                this.originRotation = new Vector3(0, 0 , 0);//180
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
          
            string[] subStr1 = record.StartPosition.Split('|');
            string[] subStr2 = record.EndPosition.Split('|');
            //float[] subValue1 = new float[subStr1.Length];
            //float[] subValue2 = new float[subStr2.Length];
            this.beginGauge = ParseHelper.TryAsSingle(subStr1[0]);
            this.finishGauge = ParseHelper.TryAsSingle(subStr2[0]);
            signY = beginGauge < finishGauge ? 1 : -1;
            this.speed = StandardSwerveTime / (finishTime - beginTime);
            this.beginX = ParseHelper.TryAsSingle(subStr1[1]);
            this.finishX = ParseHelper.TryAsSingle(subStr2[1]); ;
            this.beginZ = ParseHelper.TryAsSingle(subStr1[2]);
            this.finishZ = ParseHelper.TryAsSingle(subStr2[2]);

            float currnetTime = TimeHelper.Instance.RectifiedTime;
            if (currnetTime >= this.finishTime)
            {
                this.CompletedKinematic();
                return;
            }

            signX = this.beginX < this.finishX ? 1 : -1;
            signZ = this.beginZ < this.finishZ ? 1 : -1;
            ratioX = Mathf.Abs(this.beginX - this.finishX) / StandardSwerveRaidus;
            ratioZ = Mathf.Abs(this.beginZ - this.finishZ) / StandardSwerveRaidus;
            this.swerveType = ParseHelper.TryAsInteger(record.Tag);
        }

        public override void UserUpdate(ref float currentTime)
        {
            if (!this.IsChanged)
                return;

            if (currentTime < this.finishTime)
            {
                float timeCumulate = this.speed * (currentTime - this.beginTime);
                float angle, x, z;
                angle = this.beginGauge + signY * this.SwerveAngle(timeCumulate);
                if (swerveType == 1)//x 2 z
                {
                    x = this.beginX + signX * ratioX * SwerveFunctionIn(timeCumulate);
                    z = this.beginZ + signZ * ratioZ * SwerveFunctionOut(timeCumulate);
                }
                else
                {
                    x = this.beginX + signX * ratioX * SwerveFunctionOut(timeCumulate);
                    z = this.beginZ + signZ * ratioZ * SwerveFunctionIn(timeCumulate);
                }
                this.Advance(angle, x, z);
            }
            else
            {
                this.CompletedKinematic();
            }
        }

        private void Advance(float angle, float x, float z)
        {
            this.transform.rotation = Quaternion.Euler(originRotation.x, originRotation.y + angle, originRotation.z);
            this.transform.position = new Vector3(x, transform.position.y, z);
        }

        public override void CompletedKinematic()
        {
            this.Advance(this.finishGauge, this.finishX, this.finishZ);
            this.IsChanged = false;
        }
	}
}
