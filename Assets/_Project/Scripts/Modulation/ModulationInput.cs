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
        private ActorPair _Actors;

        [AllowNesting]
        [SerializeField]
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
        private float _PreviousInputValue = 0;

        [HorizontalLine(color: EColor.Gray)]
        [SerializeField][Range(0f, 1f)] private float _Smoothing = 0.2f;
        [SerializeField] private Vector2 _InputRange = new(0, 1);
        [SerializeField] private InputOnNewValue _OnNewValue;
        [AllowNesting]
        [EnableIf("AccumulateSelected")]
        [SerializeField] private float _AccumulateFactor = 1;
        [SerializeField] private float _PreLimitValue = 0;

        [HorizontalLine(color: EColor.Gray)]
        [SerializeField] private InputLimitMode _InputLimiter;
        [SerializeField] private bool _FlipOutput = false;
        [SerializeField][Range(0f, 1f)] private float _OutputValue = 0;

        public float Smoothing => _ValueSource != InputSourceGroups.ActorCollisions ? _Smoothing : 0;
        public float OutputValue => _OutputValue;

        private Vector3 _PreviousVector = Vector3.zero;
        private bool _RandomHasBeenSet = false;
        private float _DeltaTime = 0.02f;

        public bool ScenePropertySelected() { return _ValueSource == InputSourceGroups.SceneProperties; }
        public bool PrimaryActorSelected() { return _ValueSource == InputSourceGroups.PrimaryActor; }
        public bool LinkedActorsSelected() { return _ValueSource == InputSourceGroups.LinkedActors; }
        public bool CollisionInputSelected() { return _ValueSource == InputSourceGroups.ActorCollisions; }
        public bool AccumulateSelected() { return _OnNewValue == InputOnNewValue.Accumulate; }
        public bool RandomAtSpawnSelected() { return ScenePropertySelected() && _SceneProperties == ScenePropertySources.RandomAtSpawn; }


        public void ProcessValue()
        {
            if (RandomAtSpawnSelected())
                return;

            ProcessValue(_InputValue);
        }
        
        private void ProcessValue(float newValue)
        {
            newValue = newValue.Smooth(ref _PreviousInputValue, Smoothing, _DeltaTime);
            newValue = MaxMath.ScaleToNormNoClamp(newValue, _InputRange);
            newValue = AccumulateSelected() ? _PreLimitValue.AccumulateScaledValue(newValue, _AccumulateFactor) : newValue;

            if (_InputLimiter == InputLimitMode.Repeat)
                newValue = Mathf.Repeat(newValue, 1);
            else if (_InputLimiter == InputLimitMode.PingPong)
                newValue = Mathf.PingPong(newValue, 1);

            newValue = Mathf.Clamp01(newValue);
            newValue = _FlipOutput ? 1 - newValue : newValue;

            _OutputValue = newValue;
        }

        public void GenerateRawValue()
        {
            _DeltaTime = Time.deltaTime;

            if (ScenePropertySelected())
                GenerateScenePropertyValue();
            else if (PrimaryActorSelected())
                _Actors.GetActorValue(ref _InputValue, ref _PreviousVector, _PrimaryActor);
            else if (LinkedActorsSelected())
                _Actors.GetActorValue(ref _InputValue, ref _PreviousVector, _LinkedActors);
        }

        private void GenerateScenePropertyValue()
        {
            switch (_SceneProperties)
            {
                case ScenePropertySources.Static:
                    break;
                case ScenePropertySources.DeltaTime:
                    _InputValue = Time.deltaTime;
                    break;
                case ScenePropertySources.SpawnAge:
                    break;
                case ScenePropertySources.SpawnAgeNorm:
                    break;
                case ScenePropertySources.RandomAtSpawn:
                    if (!_RandomHasBeenSet)
                    {
                        _RandomHasBeenSet = true;
                        _InputValue = Random.Range(0, 1);
                        _OutputValue = _InputValue;
                    }
                    break;
                default:
                    break;
            }
        }

        public void GenerateCollisionValue(Collision collision)
        {
            if (!CollisionInputSelected())
                return;

            switch (_ActorCollisions)
            {
                case ActorCollisionSources.CollisionSpeed:
                    _InputValue = _Actors.CollisionSpeed(collision);
                    break;
                case ActorCollisionSources.CollisionForce:
                    _InputValue = _Actors.CollisionForce(collision);
                    break;
                default:
                    break;
            }
        }

        private void EditorInputValueChangeCallback()
        {
            ProcessValue(_InputValue);
        }
    }

    public enum InputLimitMode { Clip, Repeat, PingPong }

    public enum InputOnNewValue { Replace, Accumulate }

    public enum InputSourceGroups { SceneProperties, PrimaryActor, LinkedActors, ActorCollisions }

    public enum ScenePropertySources { Static, DeltaTime, SpawnAge, SpawnAgeNorm, RandomAtSpawn }

    public enum PrimaryActorSources { Scale, Mass, MassTimesScale, Speed, AngularSpeed, Acceleration, SlideMomentum, RollMomentum }

    public enum LinkedActorSources { DistanceX, DistanceY, DistanceZ, Radius, Polar, Elevation, RelativeSpeed, TangentialSpeed }

    public enum ActorCollisionSources { CollisionSpeed, CollisionForce }
}
