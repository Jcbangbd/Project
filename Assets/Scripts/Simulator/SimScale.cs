using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZPMC.VR.Unity
{
    public class SimScale : SimBase
	{
        //const float DefaultValue = 40f;
        private Vector3 kinematicVector;
        private Vector3 remainVector;

        public SimScale(Transform t, DOF dof)
            : base(t, dof)
        {
            if (dof == DOF.ScaleX)
            {
                this.kinematicVector = new Vector3(1, 0, 0);
                this.remainVector = new Vector3(0, 1, 1);
            }
            else if (dof == DOF.ScaleY)
            {
                this.kinematicVector = new Vector3(0, 1, 0);
                this.remainVector = new Vector3(1, 0, 1);
            }
            else if (dof == DOF.ScaleZ)
            {
                this.kinematicVector = new Vector3(0, 0, 1);
                this.remainVector = new Vector3(1, 1, 0);
            }
            else
            {
                throw new UnityException("Error");
            }
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

            float currnetTime = TimeHelper.Instance.RectifiedTime;
            if (currnetTime >= this.finishTime)
            {
                this.CompletedKinematic();
                return;
            }

            this.speed = (this.finishGauge - this.beginGauge) / (finishTime - beginTime);
        }

        public override void UserUpdate(ref float currentTime)
        {
            if (!IsChanged)
                return;
            if (currentTime < finishTime)
                this.currentValue = beginGauge + this.speed * (currentTime - beginTime);
            else
            {
                this.CompletedKinematic();
                return;
            }

            this.transform.localScale = this.kinematicVector * this.currentValue + remainVector;
        }

        public override void CompletedKinematic()
        {
            this.currentValue = this.finishGauge;
            this.transform.localScale = this.kinematicVector * this.currentValue + remainVector;
            this.IsChanged = false;
        }
	}
}
