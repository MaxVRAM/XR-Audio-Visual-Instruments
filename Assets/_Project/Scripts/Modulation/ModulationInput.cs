using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Random = UnityEngine.Random;

using MaxVRAM;
using NaughtyAttributes;

namespace PlaneWaver
{
    [Serializable]
    public class ModulationInput
    {
        private ActorPair _Actors;

        [SerializeField]
        [BoxGroup("Input Source")]
        private InputSourceGroups _InputSourceGroup;
        
        [AllowNesting]
        [SerializeField]
        [BoxGroup("Input Source")]
        [ShowIf("GeneralInputSelected")]
        private GeneralInputSource _GeneralInput;

        [AllowNesting]
        [SerializeField]
        [BoxGroup("Input Source")]
        [ShowIf("ActorInputSelected")]
        private ActorInteractionSource _ActorInteraction;

        [AllowNesting]
        [SerializeField]
        [BoxGroup("Input Source")]
        [ShowIf("RelativeInputSelected")]
        private RelativeInteractionSource _RelativeInteraction;

        [AllowNesting]
        [SerializeField]
        [BoxGroup("Input Source")]
        [ShowIf("CollisionInputSelected")]
        private ActorCollisionSource _ActorCollision;

        public bool GeneralInputSelected() { return _InputSourceGroup == InputSourceGroups.General; }
        public bool ActorInputSelected() { return _InputSourceGroup == InputSourceGroups.ActorInteraction; }
        public bool RelativeInputSelected() { return _InputSourceGroup == InputSourceGroups.RelativeInteraction; }
        public bool CollisionInputSelected() { return _InputSourceGroup == InputSourceGroups.ActorCollision; }


        [SerializeField] private float _InputValue = 0;
        [SerializeField] private InputOnNewValue _InputOnNewValue;
        [SerializeField][Range(0f, 1f)] private float _Smoothing = 0.2f;
        [SerializeField] private Vector2 _InputScaleRange = new(0, 1);

        [SerializeField] private InputLimitMode _InputLimitMode;

        [SerializeField] private bool _InvertInput = false;
        public float Smoothing => _InputSourceGroup != InputSourceGroups.ActorCollision ? _Smoothing : 0;

        [SerializeField] private float _OutputValue = 0;
        public float OutputValue => _OutputValue;
        private Vector3 _PreviousVector = Vector3.zero;
        private bool _RandomHasBeenSet = false;

        public void ProcessValue()
        {
            if (GeneralInputSelected() && _GeneralInput == GeneralInputSource.RandomAtSpawn)
                return;

            ProcessValue(_InputValue);
        }
        private void ProcessValue(float value)
        {
            _InputValue = value;
            value = MaxMath.Smooth(value, OutputValue, Smoothing, Time.deltaTime);
            value = MaxMath.ScaleToNormNoClamp(value, _InputScaleRange);

            if (_InputLimitMode == InputLimitMode.Repeat)
                value = Mathf.Repeat(value, 1);
            else if (_InputLimitMode == InputLimitMode.PingPong)
                value = Mathf.PingPong(value, 1);

            _OutputValue = _InvertInput ? 1 - value : value;
        }

        public void GetRawValue()
        {
            if (GeneralInputSelected())
                SetGeneralValue();
            else if (ActorInputSelected())
                _Actors.GetActorValue(_ActorInteraction, ref _InputValue, ref _PreviousVector);
            else if (RelativeInputSelected())
                _Actors.GetActorValue(_RelativeInteraction, ref _InputValue, ref _PreviousVector);
        }

        public void SetCollisionValue(Collision collision)
        {
            if (!CollisionInputSelected())
                return;

            switch (_ActorCollision)
            {
                case ActorCollisionSource.CollisionSpeed:
                    _InputValue = _Actors.CollisionSpeed(collision);
                    break;
                case ActorCollisionSource.CollisionForce:
                    _InputValue = _Actors.CollisionForce(collision);
                    break;
            }
        }

        private void SetGeneralValue()
        {
            switch (_GeneralInput)
            {
                case GeneralInputSource.Static:
                    break;
                case GeneralInputSource.DeltaTime:
                    break;
                case GeneralInputSource.SpawnAge:
                    break;
                case GeneralInputSource.SpawnAgeNorm:
                    break;
                case GeneralInputSource.RandomAtSpawn:
                    if (!_RandomHasBeenSet)
                    {
                        _RandomHasBeenSet = true;
                        _InputValue = Random.Range(0, 1);
                        _OutputValue = _InputValue;
                    }
                    break;
            }
        }
    }

    public enum InputLimitMode { Clip, Repeat, PingPong }

    public enum InputOnNewValue { Replace, Accumulate }

    public enum InputSourceGroups { General, ActorInteraction, RelativeInteraction, ActorCollision }

    public enum GeneralInputSource { Static, DeltaTime, SpawnAge, SpawnAgeNorm, RandomAtSpawn }

    public enum ActorInteractionSource { Speed, Scale, Mass, MassTimesScale, ContactMomentum, AngularSpeed, AngularMomentum, Acceleration }

    public enum RelativeInteractionSource { DistanceX, DistanceY, DistanceZ, Radius, Polar, Elevation, RelativeSpeed, TangentialSpeed }

    public enum ActorCollisionSource { CollisionSpeed, CollisionForce }
}
