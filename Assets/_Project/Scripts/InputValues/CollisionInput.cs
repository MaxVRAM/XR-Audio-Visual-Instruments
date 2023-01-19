
using UnityEngine;

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
                if (_Objects._LocalRigidbody != null)
                    if (_UseMassOfCollider)
                        if (_Objects._RemoteRigidbody != null)
                            newValue = collision.relativeVelocity.magnitude * (1 - _Objects._RemoteRigidbody.mass / 2);
                    else
                        newValue = collision.relativeVelocity.magnitude * _Objects._LocalRigidbody.mass;
                break;
            case CollisionProperty.CollisionPoint:
                break;
            case CollisionProperty.CollisionNormal:
                newValue = collision.GetContact(0).normal.magnitude;
                break;
            default:
                break;
        }
        
        UpdateModulationValue(newValue);
    }
}
