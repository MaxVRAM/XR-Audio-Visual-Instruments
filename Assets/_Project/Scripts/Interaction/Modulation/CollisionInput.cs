
using UnityEngine;


namespace PlaneWaver.Modulation
{
    public class CollisionInput : ModulationSource
    {
        public enum CollisionProperty
        {
            CollisionForce,
            CollisionForceTimesMass,
            CollisionPoint,
            CollisionNormal
        }

        public CollisionProperty _InputProperty;
        public bool _UseMassOfCollider = false;

        public override void ProcessCollisionValue(Collision collision)
        {
            float newValue = 0;
            switch (_InputProperty)
            {
                case CollisionProperty.CollisionForce:
                    newValue = collision.relativeVelocity.magnitude;
                    break;
                case CollisionProperty.CollisionForceTimesMass:
                    if (_Actors.ActorA.HasRigidBody)
                        if (_UseMassOfCollider && collision.rigidbody)
                            newValue = collision.relativeVelocity.magnitude * (1 - collision.rigidbody.mass / 2);
                        else
                            newValue = collision.relativeVelocity.magnitude * _Actors.ActorA.Rigidbody.mass;
                    break;
                case CollisionProperty.CollisionPoint:
                    break;
                case CollisionProperty.CollisionNormal:
                    break;
                default:
                    break;
            }

            _InputValue = newValue;
            _OutputValue = Mathf.Clamp(Map(newValue, _InputMin, _InputMax, 0, 1), 0, 1);
        }
    }
}