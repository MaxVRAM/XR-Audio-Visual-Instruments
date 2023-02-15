﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using MaxVRAM;

namespace PlaneWaver
{
    #region MODULATION SOURCE CLASS

    public class ModulationSource : MonoBehaviour
    {
        public ActorPair _Actors;
        public float _InputValue = 0;
        public float _InputMin = 0f;
        public float _InputMax = 1f;
        public float _OutputValue = 0;
        protected float _PreviousValue = 0;
        protected bool _HoldTempValue = false;
        [SerializeField] protected bool _Colliding = false;
        protected PhysicMaterial _ColliderMaterial;

        public float GetValue()
        {
            return _OutputValue;
        }

        public void UpdateModulationValue(float value, float smoothing = 0)
        {
            _InputValue = value;
            value = Map(value, _InputMin, _InputMax, 0, 1);

            if (smoothing > 0.001f && Mathf.Abs(_OutputValue - value) > 0.001f)
                _OutputValue = Mathf.Lerp(_OutputValue, value, (1 - smoothing) * 10f * Time.deltaTime);
            else
                _OutputValue = value;

            _OutputValue = Mathf.Clamp(_OutputValue, 0f, 1f);
        }

        public void SetInputCollision(bool colliding, PhysicMaterial material)
        {
            _Colliding = colliding;
            _ColliderMaterial = material;
        }

        public virtual void SetBehaviourInput(BehaviourClass behaviour) { }

        public virtual void ProcessCollisionValue(Collision collision) { }

        public static float Map(float val, float inMin, float inMax, float outMin, float outMax)
        {
            return outMin + ((outMax - outMin) / (inMax - inMin)) * (val - inMin);
        }
    }

    #endregion

    #region MODULATION INPUT CLASSES

    public class BlankModulation : ModulationSource { }

    #endregion
}