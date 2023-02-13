using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using PlaneWaver.Interaction;
using SphericalCoords = MaxVRAM.Math.MaxMath.SphericalCoords;

namespace PlaneWaver.Modulation
{
    #region MODULATION SOURCE CLASS

    public class ModulationSource : MonoBehaviour
    {
        public InputActors _Actors;
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

    [System.Serializable]
    public class InputActors
    {
        private Actor _ActorA;
        private Actor _ActorB;
        public Actor ActorA { get { return _ActorA; } }
        public Actor ActorB { get { return _ActorB; } }

        private SphericalCoords _SphericalCoords;

        public void SetLocal(Transform transform) { _ActorA = new Actor(transform); }
        public void SetRemote(Transform transform) { _ActorB = new Actor(transform); }
        public bool BothActorsSet { get { return _ActorA != null && _ActorB != null; } }
        public bool BothHaveRigidBodies { get { return _ActorA.Rigidbody != null && _ActorB.Rigidbody != null; } }
        public float Distance { get { return Vector3.Distance(ActorA.Position, ActorB.Position); } }
        public Vector3 DeltaPosition { get { return ActorA.Position - ActorB.Position; } }
        public Vector3 DirectionAB { get { return (ActorB.Position - ActorA.Position).normalized; } }
        public Vector3 DirectionBA { get { return (ActorA.Position - ActorB.Position).normalized; } }
        public float SpeedTowards { get { return (ActorB.Rigidbody.velocity - ActorB.Rigidbody.velocity).magnitude; } }
        public SphericalCoords SphericalCoords { get { return new SphericalCoords(DeltaPosition); } }

        // TOOD: Finish Math AngularSpeedFromQuaternion function and complete RelativeInput implementation

        public class Actor
        {
            private readonly Transform _Transform;
            private readonly Rigidbody _Rigidbody;
            public Actor(Transform transform)
            {
                _Transform = transform;
                if (!transform.TryGetComponent(out _Rigidbody))
                    Debug.Log("Warning: No Rigidbody component on modulation actor " + transform.name);
            }

            // TODO: Create function to return states of all (relevant) current transform and rb (velocity, rotation, etc)
            public bool HasRigidBody { get { return (_Rigidbody != null); } }
            public Transform Transform { get { return _Transform; } }
            public Rigidbody Rigidbody { get { return _Rigidbody; } }
            public Vector3 Position { get { return _Transform.transform.position; } }
            public Quaternion Rotation { get { return _Transform.transform.rotation; } }
        }
    }

    #endregion
}