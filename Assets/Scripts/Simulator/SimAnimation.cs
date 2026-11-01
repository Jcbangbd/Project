using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZPMC.VR.Unity
{
    public class SimAnimation : SimBase
    {
        //private string ClipName = "Clip01";
        private Animation _animation;
        private AnimationState _state;

        private AnimationBuffer bindingBuffer;//WithEngaged

        public float NormalizedTime
        {
            get { return _state.normalizedTime; }
        }

        public void SetNormalizedTime(float aTime)
        {
            //Debug.Log(string.Format("normalized state={0} enable={1}", _state.normalizedTime, _state.enabled));
            _state.normalizedTime = Mathf.Clamp01(aTime);
            _state.normalizedSpeed = 0;
            //_state.time = _state.length * aTime;
        }

        public SimAnimation(Transform t, DOF dof)
            : base(t, dof)
        {
            _animation = this.transform.GetComponent<Animation>();
            _state = _animation[_animation.clip.name];
            if (_state == null)
            {
                throw new UnityException(string.Format("Can't find the Animation Component.\n Equipment is {0}", this.transform.tag));
            }
            this.bindingBuffer = this.transform.GetComponent<AnimationBuffer>();
            if (this.bindingBuffer)
            {
                this.bindingBuffer.DirectionVector = new Vector3(0, 1, 0);
            }
        }

        public override void UserStart()
        {
        }

        public override void InitialParse(SimRecord record)
        {
            base.InitialParse(record);

            this.beginGauge = ParseHelper.TryAsSingle(record.StartPosition);
            this.finishGauge = ParseHelper.TryAsSingle(record.EndPosition);
            this.beginGauge = Mathf.Clamp01(this.beginGauge);
            this.finishGauge = Mathf.Clamp01(this.finishGauge);

            float currnetTime = TimeHelper.Instance.RectifiedTime;
            if (currnetTime >= this.finishTime)
            {
                if (!_state.enabled && _state.normalizedTime != this.finishGauge)
                    _state.enabled = true;
                this.CompletedKinematic();
                return;
            }

            //this.speed = (finishGauge - beginGauge) / (finishTime - beginTime);
            //_state.normalizedSpeed = this.speed; //不能用normalizedSpeed
            //_state.normalizedTime = beginGauge + this.speed * (currnetTime - beginTime); // Mathf.Lerp(beginGauge, finishGauge, (currnetTime - beginTime) / (finishTime - beginTime));

            this.speed = (finishGauge - beginGauge) / (finishTime - beginTime);//_state.length *
            this.currentValue = _state.length * Mathf.Lerp(beginGauge, finishGauge, (currnetTime - beginTime) / (finishTime - beginTime));// _state.length * (beginGauge + this.speed * (currnetTime - beginTime));// 
            _state.time = this.currentValue;

            //若选择Play Automatically, 则注销以下代码
            //_state.speed = _state.length * this.speed;
            //_animation.Play();
        }

        public override void UserUpdate(ref float currentTime)
        {
            if (!this.IsChanged)
                return;

            //若选择Play Automatically, 则注销以下代码
            //if (!_state.enabled || this.speed * (_state.normalizedTime - this.finishGauge) >= 0) //((this.speed > 0 && this.currentValue >= finishGauge) || (this.speed < 0 && this.currentValue <= finishGauge))
            //{
            //    this.CompletedKinematic();
            //    return;
            //}
            //this.currentValue = _state.normalizedTime;

            //若未选择Play Automatically, 则注销以下代码
            this.currentValue = Mathf.Lerp(beginGauge, finishGauge, (currentTime - beginTime) / (finishTime - beginTime));//beginGauge + this.speed * (currnetTime - beginTime);//
            _state.normalizedTime = this.currentValue;// Mathf.Clamp(this.currentValue, 0.001f, 0.999f);
            if (this.currentValue == this.finishGauge)
            {
                this.CompletedKinematic();
                return;
            }

            if (this.bindingBuffer != null)
            {
                this.bindingBuffer.UserUpdate(this.speed, this.currentValue);
            }
        }

        public override void CompletedKinematic()
        {
            if (this.bindingBuffer)
                this.bindingBuffer.UserUpdate(this.speed, finishGauge);
            this.currentValue = finishGauge;
            _state.normalizedTime = finishGauge;
            _state.normalizedSpeed = 0f;
            //Debug.Log(string.Format("Complete state={0} enable={1} Gauge={2}", _state.normalizedTime,_state.enabled,_state.normalizedTime));
            this.IsChanged = false;
        }
    }
}
