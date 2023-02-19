using UnityEngine;
using System;

using MaxVRAM;
using NaughtyAttributes;

namespace PlaneWaver
{
    [Serializable]
    public class ModulationInput
    {
        private Actor _LocalActor;
        private Actor _RemoteActor;

        public void SetLocalActor(Actor localActor)
        {
            _LocalActor = localActor;
        }

        public void SetRemoteActor(Actor remoteActor)
        {
            _RemoteActor = remoteActor;
        }

        public void SetActors(Actor localActor, Actor remoteActor)
        {
            _LocalActor = localActor;
            _RemoteActor = remoteActor;
        }

        public ModulationInput()
        {
        }
        public ModulationInput(Actor localActor)
        {
            _LocalActor = localActor;
        }
        public ModulationInput(Actor localActor, Actor remoteActor)
        {
            _LocalActor = localActor;
            _RemoteActor = remoteActor;
        }

        [AllowNesting]
        [HorizontalLine(color: EColor.Blue)]
        [SerializeField] private InputSourceGroups _ValueSource = InputSourceGroups.PrimaryActor;

        [AllowNesting]
        [EnableIf("ScenePropertySelected")]
        [SerializeField] private GeneralSources _SceneProperties = GeneralSources.StaticValue;

        [AllowNesting]
        [SerializeField]
        [EnableIf("PrimaryActorSelected")]
        private PrimaryActorSources _PrimaryActor = PrimaryActorSources.Speed;

        [AllowNesting]
        [EnableIf("LinkedActorsSelected")]
        [SerializeField] private LinkedActorSources _LinkedActors = LinkedActorSources.Radius;

        [AllowNesting]
        [EnableIf("CollisionInputSelected")]
        [SerializeField] private ActorCollisionSources _ActorCollisions = ActorCollisionSources.CollisionForce;

        [SerializeField] private float _InputValue = 0;
        private float _PreviousSmoothedValue = 0;

        [AllowNesting]
        [HorizontalLine(color: EColor.Clear)]
        [SerializeField] private Vector2 _InputRange = new(0, 1);
        [SerializeField] private float _AdjustMultiplier = 1;
        [SerializeField] private InputOnNewValue _OnNewValue;

        [SerializeField] private float _PreSmoothValue = 0;

        [AllowNesting]
        [HorizontalLine(color: EColor.Clear)]
        [SerializeField][Range(0f, 1f)] private float _Smoothing = 0.2f;
        [SerializeField] private InputLimitMode _LimiterMode;
        [SerializeField] private bool _FlipOutput = false;
        [SerializeField][Range(0.5f, 5.0f)] private float _ModulationExponent = 1f;

        [AllowNesting]
        [HorizontalLine(color: EColor.Blue)]
        [SerializeField][Range(0f, 1f)] private float _ModulationOutput = 0;

        public float Smoothing => _ValueSource != InputSourceGroups.ActorCollisions ? _Smoothing : 0;
        public float Result => _ModulationOutput;
        public float Exponent => _ModulationExponent;

        private Vector3 _PreviousVector = Vector3.zero;

        public bool ScenePropertySelected() { return _ValueSource == InputSourceGroups.General; }
        public bool PrimaryActorSelected() { return _ValueSource == InputSourceGroups.PrimaryActor; }
        public bool LinkedActorsSelected() { return _ValueSource == InputSourceGroups.LinkedActors; }
        public bool CollisionInputSelected() { return _ValueSource == InputSourceGroups.ActorCollisions; }
        public bool AccumulateSelected() { return _OnNewValue == InputOnNewValue.Accumulate; }

        public void ProcessValue()
        {
            GenerateRawValue();
            ProcessValue(_InputValue);
        }
        
        private void ProcessValue(float newValue)
        {
            newValue = MaxMath.ScaleToNormNoClamp(newValue, _InputRange) * _AdjustMultiplier;
            _PreSmoothValue = AccumulateSelected() ? _PreSmoothValue + newValue : newValue;
            newValue = MaxMath.Smooth(_PreviousSmoothedValue, _PreSmoothValue, Smoothing, Time.deltaTime);
            _PreviousSmoothedValue = newValue;

            if (_LimiterMode == InputLimitMode.Repeat)
                newValue = Mathf.Repeat(newValue, 1);
            else if (_LimiterMode == InputLimitMode.PingPong)
                newValue = Mathf.PingPong(newValue, 1);

            newValue = Mathf.Clamp01(newValue);
            newValue = _FlipOutput ? 1 - newValue : newValue;

            _ModulationOutput = newValue;
        }

        private void GenerateRawValue()
        {
            switch (_ValueSource)
            {
                case InputSourceGroups.General:
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

        private void EditorInputValueChangeCallback()
        {
            ProcessValue(_InputValue);
        }
        private void EditorAccumulateChangeCallback()
        {
            _PreSmoothValue = 0f;
            _PreviousSmoothedValue = 0f;
        }
    }

    public enum InputLimitMode { Clip, Repeat, PingPong }

    public enum InputOnNewValue { Replace, Accumulate }

    public enum InputSourceGroups { General, PrimaryActor, LinkedActors, ActorCollisions }

    public enum GeneralSources { StaticValue, TimeSinceStart, DeltaTime, SpawnAge, SpawnAgeNorm }

    public enum PrimaryActorSources { Scale, Mass, MassTimesScale, Speed, AngularSpeed, Acceleration, SlideMomentum, RollMomentum }

    public enum LinkedActorSources { DistanceX, DistanceY, DistanceZ, Radius, Polar, Elevation, RelativeSpeed, TangentialSpeed }

    public enum ActorCollisionSources { CollisionSpeed, CollisionForce }
}
