using System;
using UnityEngine;

using MaxVRAM.Math;

namespace MaxVRAM.Actors
{
    [Serializable]
    public class Actor
    {
        [SerializeField] private readonly Transform _Transform;
        private readonly Rigidbody _Rigidbody;
        private readonly Collider _Collider;

        public Actor(Transform transform)
        {
            _Transform = transform;
            _Rigidbody = transform.GetComponent<Rigidbody>();
            _Collider = transform.GetComponent<Collider>();
        }

        public bool HasRB { get { return (_Rigidbody != null); } }
        public Transform Transform { get { return _Transform; } }
        public Rigidbody Rigidbody { get { return _Rigidbody; } }
        public Vector3 Position { get { return _Transform.transform.position; } }
        public float Scale { get { return _Transform.localScale.magnitude; } }
        public Quaternion Rotation { get { return _Transform.transform.rotation; } }
        public Vector3 Velocity { get { return HasRB ? Rigidbody.velocity : Vector3.zero; } }
        public float Speed { get { return HasRB ? Velocity.magnitude : 0; } }
        public float Mass { get { return HasRB ? Rigidbody.mass : 0; } }
        public float AngularSpeed { get { return HasRB ? _Rigidbody.angularVelocity.magnitude : 0; } }
        // TODO: Pulled from old code. Check why (mass / 2 + 0.5f)
        public float AngularMomentum { get { return AngularSpeed * (_Rigidbody.mass / 2 + 0.5f); } }
        public float FrictionApproximation { get { return Speed / Mathf.Max(AngularSpeed, 1); } }

        // TODO: Refactor actor calculations to take quantum entanglement into consideration
        public Vector3 SpinVector { get { return new Vector3(0, Rando.PickOne(new int[] { -1, 1 }), 0); } }
    }

    [Serializable]
    public class ActorPair
    {
        [SerializeField] private Actor _ActorA;
        [SerializeField] private Actor _ActorB;

        public ActorPair(Transform actorA) { SetActorA(actorA);}
        public ActorPair(Transform actorA, Transform actorB) {  SetActorA(actorA); SetActorB(actorB); }
        public void SetActorA(Transform transform) { _ActorA = new Actor(transform); }
        public void SetActorB(Transform transform) { _ActorB = new Actor(transform); }
        public Actor ActorA { get { return _ActorA; } }
        public Actor ActorB { get { return _ActorB; } }

        public bool BothSet { get { return _ActorA != null && _ActorB != null; } }
        public bool HaveRBs { get { return _ActorA.HasRB && _ActorB.HasRB; } }

        public float Distance { get { return BothSet ? Vector3.Distance(ActorA.Position, ActorB.Position) : 0; } }
        public Vector3 DirectionAB { get { return BothSet ? (ActorB.Position - ActorA.Position).normalized : Vector3.zero; } }
        public Vector3 DirectionBA { get { return BothSet ? (ActorA.Position - ActorB.Position).normalized : Vector3.zero; } }
        public Vector3 RelativePosition { get { return BothSet ? ActorB.Position - ActorA.Position : Vector3.zero; } }
        public MaxMath.SphericalCoords SphericalCoords { get { return new MaxMath.SphericalCoords(RelativePosition); } }
        public float RelativeSpeed { get { return BothSet ? Vector3.Dot(ActorB.Rigidbody.velocity, ActorB.Rigidbody.velocity) : 0; } }

        public Quaternion Rotation(Vector3 previousDirection) { return Quaternion.FromToRotation(previousDirection, DirectionAB); }
        public float AngularSpeed(Quaternion rotation) { return MaxMath.AngularSpeedFromQuaternion(rotation); }
        public float AngularSpeed(Vector3 previousDirection) { return MaxMath.AngularSpeedFromQuaternion(Rotation(previousDirection)); }
    }
}
