using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Random = UnityEngine.Random;

using MaxVRAM;
using MaxVRAM.Extensions;
using NaughtyAttributes;

namespace PlaneWaver
{
    [Serializable]
    public class ModulationInput
    {
        private Actor _LocalActor;
        private Actor _RemoteActor;

        public void SetLocalActor(Actor actor)
        {
            _LocalActor = actor;
        }

        public void SetRemoteActor(Actor actor)
        {
            _RemoteActor = actor;
        }

        public void SetBothActors(Actor actorA, Actor actorB)
        {
            _LocalActor = actorA;
            _RemoteActor = actorB;
        }

        public ModulationInput()
        {
        }
        public ModulationInput(Actor actorA)
        {
            _LocalActor = actorA;
        }
        public ModulationInput(Actor actorA, Actor actorB)
        {
            _LocalActor = actorA;
            _RemoteActor = actorB;
        }

        [AllowNesting]
        [SerializeField]
        [HorizontalLine(color: EColor.Gray)]
        private InputSourceGroups _ValueSource = InputSourceGroups.PrimaryActor;

        [AllowNesting]
        [SerializeField]
        [EnableIf("ScenePropertySelected")]
        private ScenePropertySources _SceneProperties = ScenePropertySources.DeltaTime;

        [AllowNesting]
        [SerializeField]
        [EnableIf("PrimaryActorSelected")]
        private PrimaryActorSources _PrimaryActor = PrimaryActorSources.Speed;

        [AllowNesting]
        [SerializeField]
        [EnableIf("LinkedActorsSelected")]
        private LinkedActorSources _LinkedActors = LinkedActorSources.Radius;

        [AllowNesting]
        [SerializeField]
        [EnableIf("CollisionInputSelected")]
        private ActorCollisionSources _ActorCollisions = ActorCollisionSources.CollisionForce;

        [AllowNesting]
        [OnValueChanged("EditorInputValueChangeCallback")]
        [SerializeField] private float _InputValue = 0;
        private float _PreviousSmoothedValue = 0;

        [HorizontalLine(color: EColor.Gray)]
        [SerializeField] private Vector2 _InputRange = new(0, 1);
        [SerializeField] private float _AdjustMultiplier = 1;

        [AllowNesting]
        [OnValueChanged("EditorAccumulateChangeCallback")]
        [SerializeField] private InputOnNewValue _OnNewValue;
        
        [SerializeField] private float _PreSmoothValue = 0;

        [HorizontalLine(color: EColor.Gray)]
        [SerializeField][Range(0f, 1f)] private float _Smoothing = 0.2f;
        [SerializeField] private InputLimitMode _InputLimiter;
        [SerializeField] private bool _FlipOutput = false;
        [SerializeField][Range(0f, 1f)] private float _OutputValue = 0;

        public float Smoothing => _ValueSource != InputSourceGroups.ActorCollisions ? _Smoothing : 0;
        public float OutputValue => _OutputValue;

        private Vector3 _PreviousVector = Vector3.zero;
        //private float _RandomValue = -1;

        public bool ScenePropertySelected() { return _ValueSource == InputSourceGroups.SceneProperties; }
        public bool PrimaryActorSelected() { return _ValueSource == InputSourceGroups.PrimaryActor; }
        public bool LinkedActorsSelected() { return _ValueSource == InputSourceGroups.LinkedActors; }
        public bool CollisionInputSelected() { return _ValueSource == InputSourceGroups.ActorCollisions; }
        public bool AccumulateSelected() { return _OnNewValue == InputOnNewValue.Accumulate; }
        public bool RandomAtSpawnSelected() { return ScenePropertySelected() && _SceneProperties == ScenePropertySources.RandomAtSpawn; }

        public void ProcessValue()
        {
            //if (RandomAtSpawnSelected())
            //{
            //    _InputValue = _RandomValue;
            //    return;
            //}

            GenerateRawValue();
            ProcessValue(_InputValue);
        }
        
        private void ProcessValue(float newValue)
        {
            newValue = MaxMath.ScaleToNormNoClamp(newValue, _InputRange) * _AdjustMultiplier;
            _PreSmoothValue = AccumulateSelected() ? _PreSmoothValue + newValue : newValue;
            newValue = MaxMath.Smooth(_PreviousSmoothedValue, _PreSmoothValue, Smoothing, Time.deltaTime);
            _PreviousSmoothedValue = newValue;

            if (_InputLimiter == InputLimitMode.Repeat)
                newValue = Mathf.Repeat(newValue, 1);
            else if (_InputLimiter == InputLimitMode.PingPong)
                newValue = Mathf.PingPong(newValue, 1);

            newValue = Mathf.Clamp01(newValue);
            newValue = _FlipOutput ? 1 - newValue : newValue;

            _OutputValue = newValue;
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

            //Debug.Log($"Return Value:   {returnValue}");

            //if (ScenePropertySelected())
            //    GenerateScenePropertyValue();
            //else if (PrimaryActorSelected())
            //    _LocalActor.GetActorValue(ref _InputValue, ref _PreviousVector, _PrimaryActor);
            //else if (LinkedActorsSelected())
            //    _LocalActor.GetActorPairValue(ref _InputValue, ref _PreviousVector, _RemoteActor, _LinkedActors);
            //else if (CollisionInputSelected())
            //    _LocalActor.GetCollisionValue(ref _InputValue, _ActorCollisions);
        }

        private void GenerateScenePropertyValue()
        {
            switch (_SceneProperties)
            {
                case ScenePropertySources.Static:
                    break;
                case ScenePropertySources.RandomAtSpawn:
                    //_RandomValue = _RandomValue == -1 ? Random.Range(0, 1) : _RandomValue;
                    //_InputValue = _RandomValue;
                    //_OutputValue = _RandomValue;
                    break;
                case ScenePropertySources.TimeSinceStart:
                    _InputValue = Time.time;
                    break;
                case ScenePropertySources.DeltaTime:
                    _InputValue = Time.deltaTime;
                    break;
                case ScenePropertySources.SpawnAge:
                    break;
                case ScenePropertySources.SpawnAgeNorm:
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

    public enum InputSourceGroups { SceneProperties, PrimaryActor, LinkedActors, ActorCollisions }

    public enum ScenePropertySources { Static, TimeSinceStart, DeltaTime, SpawnAge, SpawnAgeNorm, RandomAtSpawn }

    public enum PrimaryActorSources { Scale, Mass, MassTimesScale, Speed, AngularSpeed, Acceleration, SlideMomentum, RollMomentum }

    public enum LinkedActorSources { DistanceX, DistanceY, DistanceZ, Radius, Polar, Elevation, RelativeSpeed, TangentialSpeed }

    public enum ActorCollisionSources { CollisionSpeed, CollisionForce }
}
