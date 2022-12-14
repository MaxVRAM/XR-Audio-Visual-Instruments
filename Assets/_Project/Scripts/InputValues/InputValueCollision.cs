using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputValueCollision : InputValueClass
{
    public enum InputCollisionType
    {
        CollisionForce,
        CollisionForceTimesMass,
        CollisionPoint,
        CollisionNormal,
    }

    public InputCollisionType _InputProperty;
    public bool _UseMassOfCollider = false;

    public override void ProcessCollision(Collision collision)
    {
        switch (_InputProperty)
        {
            case InputCollisionType.CollisionForce:
                _OutputValue = collision.relativeVelocity.magnitude;
                break;
            case InputCollisionType.CollisionForceTimesMass:
                if (_PrimaryRigidBody != null)
                    if (_UseMassOfCollider)
                        if (_SecondaryRigidBody != null)
                            _OutputValue = collision.relativeVelocity.magnitude * (1 - _SecondaryRigidBody.mass / 2);
                    else
                        _OutputValue = collision.relativeVelocity.magnitude * _PrimaryRigidBody.mass;
                break;
            case InputCollisionType.CollisionPoint:
                break;
            case InputCollisionType.CollisionNormal:
                _OutputValue = collision.GetContact(0).normal.magnitude;
                break;
            default:
                break;
        }
        _OutputValue = Map(_OutputValue, _InputMin, _InputMax, 0, 1);
        
    }
}
