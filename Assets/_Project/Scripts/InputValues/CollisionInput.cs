using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CollisionInput : ModulationSource
{
    public enum CollisionProperty
    {
        CollisionForce,
        CollisionForceTimesMass,
        CollisionPoint,
        CollisionNormal,
    }

    public CollisionProperty _InputProperty;
    public bool _UseMassOfCollider = false;

    public override void ProcessCollisionValue(Collision collision)
    {
        switch (_InputProperty)
        {
            case CollisionProperty.CollisionForce:
                _OutputValue = collision.relativeVelocity.magnitude;
                break;
            case CollisionProperty.CollisionForceTimesMass:
                if (_Objects._LocalRigidbody != null)
                    if (_UseMassOfCollider)
                        if (_Objects._RemoteRigidbody != null)
                            _OutputValue = collision.relativeVelocity.magnitude * (1 - _Objects._RemoteRigidbody.mass / 2);
                    else
                        _OutputValue = collision.relativeVelocity.magnitude * _Objects._LocalRigidbody.mass;
                break;
            case CollisionProperty.CollisionPoint:
                break;
            case CollisionProperty.CollisionNormal:
                _OutputValue = collision.GetContact(0).normal.magnitude;
                break;
            default:
                break;
        }
        _OutputValue = Map(_OutputValue, _InputMin, _InputMax, 0, 1);
        
    }
}
