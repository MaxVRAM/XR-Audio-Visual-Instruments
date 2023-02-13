using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using PlaneWaver.Interaction;

namespace PlaneWaver.Modulation
{
    public class InputBehaviour : ModulationSource
    {
        public enum BehaviourProperty { Age, AgeNormalised }

        public BehaviourProperty _InputProperty;

        [Range(0f, 1f)]
        public float _Smoothing = 0f;

        protected SpawnableManager _DestroyTimer;

        public override void SetBehaviourInput(BehaviourClass behaviour)
        {
            if (behaviour is SpawnableManager timer)
                _DestroyTimer = timer;
        }

        private void Update()
        {
            float value = _PreviousValue;
            if (_DestroyTimer != null)
            {
                switch (_InputProperty)
                {
                    case BehaviourProperty.Age:
                        value = _DestroyTimer.CurrentAge;
                        break;
                    case BehaviourProperty.AgeNormalised:
                        value = _DestroyTimer.CurrentAgeNorm;
                        break;
                    default:
                        break;
                }
            }
            else value = 0;
            UpdateModulationValue(value, _Smoothing);
        }

        public void SetAuxValue(float val)
        {
            UpdateModulationValue(val, _Smoothing);
        }
    }
}