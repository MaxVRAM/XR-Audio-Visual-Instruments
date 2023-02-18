using UnityEngine;
using System;

using MaxVRAM;
using MaxVRAM.Extensions;
using NaughtyAttributes;

namespace PlaneWaver
{
    [Serializable]
    public struct ModulationStruct
    {
        private bool _Initialised;
        private Actor _LocalActor;
        private Actor _RemoteActor;

        public void SetLocalActor(Actor localActor)
        {
            if (localActor.Exists())
                _Initialised = true;
            _LocalActor = localActor;
        }

        public void SetRemoteActor(Actor remoteActor)
        {
            _RemoteActor = remoteActor;
        }

        public void SetActors(Actor localActor, Actor remoteActor)
        {
            // Not sure why, perhaps passing structs around, but if I debug print _Initialised here it's true.
            // Then debugging in VS shows it as false when stepping through a call to GetProcessed Value.. hmmm
            if (localActor.Exists())
                _Initialised = true;
            _LocalActor = localActor;
            _RemoteActor = remoteActor;
        }

        public ModulationStruct(bool initialised = false)
        {
            _Initialised = initialised;
            _LocalActor = new();
            _RemoteActor = new();
            _ValueSource = InputSourceGroups.PrimaryActor;
            _SceneProperties = GeneralSources.StaticValue;
            _PrimaryActor = PrimaryActorSources.Speed;
            _LinkedActors = LinkedActorSources.Radius;
            _ActorCollisions = ActorCollisionSources.CollisionForce;
            _InputValue = 0;
            _PreviousSmoothedValue = 0;
            _InputRange = new(0, 1);
            _AdjustMultiplier = 1;
            _OnNewValue = InputOnNewValue.Replace;
            _PreSmoothValue = 0;
            _Smoothing = 0.2f;
            _LimiterMode = InputLimitMode.Clip;
            _InvertModulation = false;
            _ModulationExponent = 1f;
            _ModulationAmount = 0;
            _Result = 0;
            _PreviousVector = Vector3.zero;
        }

        [HorizontalLine(color: EColor.Gray)]
        public InputSourceGroups _ValueSource;

        [ShowIf("ScenePropertySelected")]
        [AllowNesting]
        public GeneralSources _SceneProperties;

        [ShowIf("PrimaryActorSelected")]
        [AllowNesting]
        public PrimaryActorSources _PrimaryActor;

        [ShowIf("LinkedActorsSelected")]
        [AllowNesting]
        public LinkedActorSources _LinkedActors;

        [ShowIf("CollisionInputSelected")]
        [AllowNesting]
        public ActorCollisionSources _ActorCollisions;

        [SerializeField] private float _InputValue;
        private float _PreviousSmoothedValue;

        [HorizontalLine(color: EColor.Clear)]
        [SerializeField] private Vector2 _InputRange;
        [SerializeField] private float _AdjustMultiplier;
        [SerializeField] private InputOnNewValue _OnNewValue;
        [SerializeField] private float _PreSmoothValue;

        [HorizontalLine(color: EColor.Clear)]
        [SerializeField][Range(0f, 1f)] private float _Smoothing;
        [SerializeField] private InputLimitMode _LimiterMode;

        [SerializeField] private bool _InvertModulation;
        [SerializeField][Range(0.5f, 5.0f)] private float _ModulationExponent;
        [SerializeField][Range(-1f, 1f)] private float _ModulationAmount;

        [HorizontalLine(color: EColor.Gray)]
        [SerializeField][Range(0f, 1f)] private float _Result;

        public float Smoothing => _ValueSource != InputSourceGroups.ActorCollisions ? _Smoothing : 0;
        public float Exponent => _ModulationExponent;
        public float Amount => _ModulationAmount;

        private Vector3 _PreviousVector;

        public bool ScenePropertySelected() { return _ValueSource == InputSourceGroups.SceneProperties; }
        public bool PrimaryActorSelected() { return _ValueSource == InputSourceGroups.PrimaryActor; }
        public bool LinkedActorsSelected() { return _ValueSource == InputSourceGroups.LinkedActors; }
        public bool CollisionInputSelected() { return _ValueSource == InputSourceGroups.ActorCollisions; }

        public float GetProcessedValue()
        {
            //if (!_Initialised)
            //    return 0;

            GenerateRawValue();
            _Result = ProcessValue(_InputValue);
            return _Result;
        }

        public float GetProcessedValue(float offset)
        {
            //if (!_Initialised)
            //    return offset;

            GenerateRawValue();
            _Result = ApplyModulationToOffset(offset, ProcessValue(_InputValue));
            return _Result;
        }

        //float parameterRange = Mathf.Abs(mod._Max - mod._Min);
        //float modulation = Mathf.Pow(mod._Input / 1, mod._Exponent) * mod._Modulation * parameterRange;

        private float ProcessValue(float inputValue)
        {
            float newValue = MaxMath.ScaleToNormNoClamp(inputValue, _InputRange) * _AdjustMultiplier;
            _PreSmoothValue = _OnNewValue == InputOnNewValue.Accumulate ? _PreSmoothValue + newValue : newValue;
            newValue = MaxMath.Smooth(_PreviousSmoothedValue, _PreSmoothValue, Smoothing, Time.deltaTime);
            _PreviousSmoothedValue = newValue;

            if (_LimiterMode == InputLimitMode.Repeat)
                newValue = newValue.RepeatNorm();
            else if (_LimiterMode == InputLimitMode.PingPong)
                newValue = newValue.PingPongNorm();

            newValue = _InvertModulation ? 1 - newValue : newValue;
            return newValue;
        }

        private float ApplyModulationToOffset(float offset, float modulation)
        {
            modulation = Mathf.Pow(modulation, Exponent) * Amount;
            float result = offset + modulation;

            if (_LimiterMode == InputLimitMode.Repeat)
            {
                if (result < 0)         result += 1;
                else if (result > 1)    result -= 1;
            }
            else if (_LimiterMode == InputLimitMode.PingPong)
            {
                if (result < 0)         result = -result;
                else if (result > 1)    result = -result + 1;
            }

            return result;
        }

        private void GenerateRawValue()
        {
            switch (_ValueSource)
            {
                case InputSourceGroups.SceneProperties:
                    GenerateScenePropertyValue();
                    break;
                case InputSourceGroups.PrimaryActor:
                    _LocalActor.GetActorValue(ref _InputValue, ref _PreviousVector, _PrimaryActor);
                    break;
                case InputSourceGroups.LinkedActors:
                    _LocalActor.GetActorPairValue(ref _InputValue, ref _PreviousVector, _RemoteActor, _LinkedActors);
                    break;
                case InputSourceGroups.ActorCollisions:
                    _LocalActor.GetCollisionValue(ref _InputValue, _ActorCollisions);
                    break;
                default:
                    break;
            }
        }

        private void GenerateScenePropertyValue()
        {
            switch (_SceneProperties)
            {
                case GeneralSources.StaticValue:
                    break;
                case GeneralSources.TimeSinceStart:
                    _InputValue = Time.time;
                    break;
                case GeneralSources.DeltaTime:
                    _InputValue = Time.deltaTime;
                    break;
                case GeneralSources.SpawnAge:
                    break;
                case GeneralSources.SpawnAgeNorm:
                    break;
                default:
                    break;
            }
        }
    }
}
