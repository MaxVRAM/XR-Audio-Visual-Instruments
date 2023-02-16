using System;
using UnityEngine;

using MaxVRAM;

namespace PlaneWaver
{
    [Serializable]
    public struct Actor
    {
        // TODO: Add collision events and return implementation within this actor class.
        [SerializeField] private readonly Transform _Transform;
        private readonly Rigidbody _Rigidbody;
        private readonly Collider _Collider;
        private Collision _LatestCollision;
        private bool _IsColliding;
        private bool _IsAlive;

        public Actor(Transform transform)
        {
            _Transform = transform;
            _Rigidbody = transform.GetComponent<Rigidbody>();
            _Collider = transform.GetComponent<Collider>();
            _LatestCollision = null;
            _IsColliding = false;
            _IsAlive = true;
        }

        public Actor(bool isSet = false)
        {
            _Transform = null;
            _Rigidbody = null;
            _Collider = null;
            _LatestCollision = null;
            _IsColliding = false;
            _IsAlive = isSet;
        }

        public bool StillAlive()
        {
            _IsAlive = _Transform != null;
            return _IsAlive;
        }

        public bool HasRB => (_Rigidbody != null);
        public bool HasCollider => _Collider != null;

        public Transform Transform => _Transform;
        public Rigidbody Rigidbody => _Rigidbody;
        public Vector3 Position => _Transform.transform.position;
        public Quaternion Rotation => _Transform.transform.rotation;
        public Vector3 Velocity => HasRB ? Rigidbody.velocity : Vector3.zero;
        public float Speed => HasRB ? Velocity.magnitude : 0;
        public float Scale => _Transform.localScale.magnitude;
        public float Mass => HasRB ? Rigidbody.mass : 0;
        public float ContactMomentum => Mass * Speed / Mathf.Max(AngularSpeed, 1);
        public float AngularSpeed => HasRB ? _Rigidbody.angularVelocity.magnitude : 0;
        // TODO: Pulled from old code. Check why (mass / 2 + 0.5f)
        public float AngularMomentum => AngularSpeed * ((Mass / 2) + 0.5f);
        // TODO: Refactor actor calculations to take quantum entanglement into consideration
        public Vector3 SpinVector => new(0, Rando.PickOne(new int[] { -1, 1 }), 0);
        public float Acceleration(Vector3 previousVelocity) { return (Velocity - previousVelocity).magnitude; }
        public float Acceleration(float previousSpeed) { return Speed - previousSpeed; }
        public Collision LatestCollision { get => _LatestCollision; set => _LatestCollision = value; }
        public bool CollidingState { get => _IsColliding; set => _IsColliding = value; }
    }

    [Serializable]
    public class ActorPair
    {
        [SerializeField] private Actor _ActorA;
        [SerializeField] private Actor _ActorB;

        public ActorPair(Transform actorA)
        {
            _ActorA = new(actorA);
            _ActorB = new(true);
        }
        public ActorPair(Transform actorA, Transform actorB)
        { 
            _ActorA = new(actorA);
            _ActorB = new(actorB); 
        }

        public void SetActorA(Transform transform) { _ActorA = new Actor(transform); }
        public void SetActorB(Transform transform) { _ActorB = new Actor(transform); }
        public Actor ActorA => _ActorA;
        public Actor ActorB => _ActorB;

        public bool BothSet => _ActorA.StillAlive() && _ActorB.StillAlive();
        public bool HaveRBs => _ActorA.HasRB && _ActorB.HasRB;

        // TODO: Move these functions to a static struct that's used by the Actor struct.
        // Arrays of Actors can be added to a field with ActorB instance passed into linked comparison functions at runtime.
        // Way more flexiblity and unties Actors from the "Pair" concept.
        public Vector3 RelativePosition => BothSet ? ActorB.Position - ActorA.Position : Vector3.zero;
        public Vector3 DirectionAB => BothSet ? (ActorB.Position - ActorA.Position).normalized : Vector3.zero;
        public Vector3 DirectionBA => BothSet ? (ActorA.Position - ActorB.Position).normalized : Vector3.zero;
        public float Distance => BothSet ? Vector3.Distance(ActorA.Position, ActorB.Position) : 0;
        public float RelativeSpeed => BothSet ? Vector3.Dot(ActorB.Rigidbody.velocity, ActorB.Rigidbody.velocity) : 0;
        public MaxMath.SphericalCoords SphericalCoords => new(RelativePosition);

        public Quaternion Rotation(Vector3 previousDirection) { return Quaternion.FromToRotation(previousDirection, DirectionBA); }
        public float TangentalSpeed(Quaternion rotation) { return MaxMath.TangentalSpeedFromQuaternion(rotation); }
        public float TangentalSpeed(Vector3 previousDirection) { return MaxMath.TangentalSpeedFromQuaternion(Rotation(previousDirection)); }
        public float CollisionSpeed(Collision collision) { return collision.relativeVelocity.magnitude; }
        public float CollisionForce(Collision collision) { return collision.impulse.magnitude; }
        public bool CollidingState { get => ActorA.CollidingState; set => _ActorA.CollidingState = value; }

        public void GetActorValue(ref float returnValue, ref Vector3 previousVector, PrimaryActorSources selection)
        {
            switch (selection)
            {
                case PrimaryActorSources.Speed:
                    returnValue = ActorA.Speed;
                    break;
                case PrimaryActorSources.Scale:
                    returnValue = ActorA.Scale;
                    break;
                case PrimaryActorSources.Mass:
                    returnValue = ActorA.Mass;
                    break;
                case PrimaryActorSources.MassTimesScale:
                    returnValue = ActorA.Mass * ActorA.Scale;
                    break;
                case PrimaryActorSources.SlideMomentum:
                    returnValue = ActorA.ContactMomentum;
                    break;
                case PrimaryActorSources.AngularSpeed:
                    returnValue = ActorA.AngularSpeed;
                    break;
                case PrimaryActorSources.RollMomentum:
                    returnValue = ActorA.AngularMomentum;
                    break;
                case PrimaryActorSources.Acceleration:
                    returnValue = ActorA.Acceleration(previousVector);
                    previousVector = ActorA.Velocity;
                    break;
                default:
                    break;
            }
        }

        public void GetActorValue(ref float returnValue, ref Vector3 previousVector, LinkedActorSources selection)
        {
            if (!_ActorB.StillAlive())
            {
                returnValue = 0;
                previousVector = Vector3.zero;
                return;
            }

            switch (selection)
            {
                case LinkedActorSources.DistanceX:
                    returnValue = Mathf.Abs(RelativePosition.x);
                    break;
                case LinkedActorSources.DistanceY:
                    returnValue = Mathf.Abs(RelativePosition.y);
                    break;
                case LinkedActorSources.DistanceZ:
                    returnValue = Mathf.Abs(RelativePosition.z);
                    break;
                case LinkedActorSources.Radius:
                    returnValue = SphericalCoords.Radius;
                    break;
                case LinkedActorSources.Polar:
                    returnValue = SphericalCoords.Polar;
                    break;
                case LinkedActorSources.Elevation:
                    returnValue = SphericalCoords.Elevation;
                    break;
                case LinkedActorSources.RelativeSpeed:
                    returnValue = RelativeSpeed;
                    break;
                case LinkedActorSources.TangentialSpeed:
                    returnValue = TangentalSpeed(previousVector);
                    previousVector = DirectionBA;
                    break;
                default:
                    break;
            }
        }
    }
}
