using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractionCollision : InteractionBase
{
    public enum InteractionCollisionType
    {
        CollisionForce,
        CollisionForceTimesMass,
        CollisionPoint,
        CollisionNormal,
    }

    public InteractionCollisionType _InputProperty;
    public bool _UseMassOfCollider = false;

    public override void ProcessCollision(Collision collision)
    {
        switch (_InputProperty)
        {
            case InteractionCollisionType.CollisionForce:
                _OutputValue = collision.relativeVelocity.magnitude;
                break;
            case InteractionCollisionType.CollisionForceTimesMass:
                if (_PrimaryRigidBody != null)
                    if (_UseMassOfCollider)
                        if (_SecondaryRigidBody != null)
                            _OutputValue = collision.relativeVelocity.magnitude * (1 - _SecondaryRigidBody.mass / 2);
                    else
                        _OutputValue = collision.relativeVelocity.magnitude * _PrimaryRigidBody.mass;
                break;
            case InteractionCollisionType.CollisionPoint:
                break;
            case InteractionCollisionType.CollisionNormal:
                _OutputValue = collision.GetContact(0).normal.magnitude;
                break;
            default:
                break;
        }
        _OutputValue = Map(_OutputValue, _InputMin, _InputMax, 0, 1);
        
    }
}
