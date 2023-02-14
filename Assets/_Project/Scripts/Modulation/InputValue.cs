using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using MaxVRAM.Math;
using MaxVRAM.Actors;
using Selector = MaxVRAM.Actors.ActorValueSelection;

namespace PlaneWaver.Modulation
{
    public class InputValue : MonoBehaviour
    {
        public ActorPair _Actors;
        public Selector _Input;
        [SerializeField] private float _InputValue;
        [SerializeField] private Vector2 _InputMinMax = new Vector2(0, 1);
        [SerializeField] private bool _InvertValue = false;
        [SerializeField][Range(0f, 1f)] private float _Smoothing = 0.2f;
        [SerializeField] private float _OutputValue;

        private bool _Colliding = false;
        private PhysicMaterial _ColliderMaterial;

        private float _FloatValueHolder = 0;
        private Vector3 _VectorValueHolder = Vector3.zero;

        public bool CollisionSelected { get { return _Input != Selector.CollisionSpeed && _Input != Selector.CollisionForce; } }
        public float Smoothing { get {return !CollisionSelected ? _Smoothing : 0; } }

        public float OutputValue { get { return _OutputValue; } }

        public void ProcessOutputValue(float value)
        {
            _InputValue = value;
            value = MaxMath.MapToNorm(value, _InputMinMax.x, _InputMinMax.y);
            value = _InvertValue ? 1 - value : value;
            if (Smoothing > 0.001f && Mathf.Abs(_OutputValue - value) > 0.001f)
                _OutputValue = Mathf.Lerp(_OutputValue, value, (1 - Smoothing) * 10f * Time.deltaTime);
            else
                _OutputValue = value;

            _OutputValue = Mathf.Clamp(_OutputValue, 0f, 1f);
        }

        public void ProcessValue()
        {
            ProcessOutputValue(_InputValue);
        }

        public void ProcessInputValue()
        {
            switch (_Input)
            {
                case Selector.Speed:
                    _InputValue = _Actors.ActorA.Speed;
                    break;
                case Selector.Scale:
                    _InputValue = _Actors.ActorA.Scale;
                    break;
                case Selector.Mass:
                    _InputValue = _Actors.ActorA.Mass;
                    break;
                case Selector.Friction:
                    _InputValue = _Actors.ActorA.Friction;
                    break;
                case Selector.Acceleration:
                    _InputValue = _Actors.ActorA.Acceleration(_FloatValueHolder);
                    _FloatValueHolder = _Actors.ActorA.Speed;
                    break;
                case Selector.AngularSpeed:
                    _InputValue = _Actors.ActorA.AngularSpeed;
                    break;
                case Selector.AngularMomentum:
                    _InputValue = _Actors.ActorA.AngularMomentum;
                    break;
                case Selector.DistanceX:
                    _InputValue = _Actors.DistanceVector.x;
                    break;
                case Selector.DistanceY:
                    _InputValue = _Actors.DistanceVector.y;
                    break;
                case Selector.DistanceZ:
                    _InputValue = _Actors.DistanceVector.z;
                    break;
                case Selector.Radius:
                    _InputValue = _Actors.SphericalCoords.Radius;
                    break;
                case Selector.Polar:
                    _InputValue = _Actors.SphericalCoords.Polar;
                    break;
                case Selector.Elevation:
                    _InputValue = _Actors.SphericalCoords.Elevation;
                    break;
                case Selector.RelativeSpeed:
                    _InputValue = _Actors.RelativeSpeed;
                    break;
                case Selector.TangentialSpeed:
                    _InputValue = _Actors.TangentalSpeed(_VectorValueHolder);
                    _VectorValueHolder = _Actors.DirectionBA;
                    break;
            }
        }

        public void SetCollisionValue(Collision collision)
        {
            switch (_Input)
            {
                case Selector.CollisionSpeed:
                    _InputValue = _Actors.CollisionSpeed(collision);
                    break;
                case Selector.CollisionForce:
                    _InputValue = _Actors.CollisionForce(collision);
                    break;
            }
        }

        public void SetCollidingState(bool colliding, PhysicMaterial material)
        {
            _Colliding = colliding;
            _ColliderMaterial = material;
        }
    }
}