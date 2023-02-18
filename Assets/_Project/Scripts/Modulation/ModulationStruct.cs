using UnityEngine;
using System;

using MaxVRAM;
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
            _FlipOutput = false;
            _ModulationExponent = 1f;
            _ModulationAmount = 0;
            _Result = 0;
            _PreviousVector = Vector3.zero;
        }

        [HorizontalLine(color: EColor.Blue)]
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

        [SerializeField] private bool _FlipOutput;
        [SerializeField][Range(0.5f, 5.0f)] private float _ModulationExponent;
        [SerializeField][Range(-1f, 1f)] private float _ModulationAmount;

        [HorizontalLine(color: EColor.Blue)]
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
            if (!_Initialised)
                return 0;

            GenerateRawValue();
            ProcessValue();
            return _Result;
        }

        public float GetProcessedValue(float offsetInputValue)
        {
            if (!_Initialised)
                return offsetInputValue;

            GenerateRawValue();
            ProcessValue(offsetInputValue);
            return _Result;
        }

        //float parameterRange = Mathf.Abs(mod._Max - mod._Min);
        //float modulation = Mathf.Pow(mod._Input / 1, mod._Exponent) * mod._Modulation * parameterRange;

        private void ProcessValue(float offsetInputValue = float.MaxValue)
        {
            float newValue = MaxMath.ScaleToNormNoClamp(_InputValue, _InputRange) * _AdjustMultiplier;
            _PreSmoothValue = _OnNewValue == InputOnNewValue.Accumulate ? _PreSmoothValue + newValue : newValue;
            newValue = MaxMath.Smooth(_PreviousSmoothedValue, _PreSmoothValue, Smoothing, Time.deltaTime);
            _PreviousSmoothedValue = newValue;

            if (_LimiterMode == InputLimitMode.Repeat)
                newValue = Mathf.Repeat(newValue, 1);
            else if (_LimiterMode == InputLimitMode.PingPong)
                newValue = Mathf.PingPong(newValue, 1);

            // TODO: There's very likely a better way to do this. Logic behind this double up is that
            // the modulation value needs to be scaled with the exponent prior to being applied to the
            // input value. But it needs to be within 0..1 for correct scaling. So applying the limiter
            // first resolves its range, and limiting function is reapplied once combined with input.
            if (offsetInputValue != float.MaxValue)
            {
                newValue = Mathf.Pow(newValue / 1, Exponent) * Amount;
                newValue += offsetInputValue;

                if (_LimiterMode == InputLimitMode.Repeat)
                {
                    if (newValue < 0)       newValue = 1;
                    else if (newValue > 1)  newValue -= 1;
                }
                else if (_LimiterMode == InputLimitMode.PingPong)
                {
                    if (newValue < 0)       newValue = -newValue;
                    else if (newValue > 1)  newValue = 1 - newValue;
                }
            }

            newValue = Mathf.Clamp01(newValue);
            newValue = _FlipOutput ? 1 - newValue : newValue;

            _Result = newValue;
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
