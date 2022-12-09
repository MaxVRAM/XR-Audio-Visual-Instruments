using System.Collections;
using System.Collections.Generic;
using Unity.Physics;
using UnityEngine;

//
// Summary:
//     Provides an input value to use on a Grain Emitter, based on a physical interaction from the source rigid body.
//
[System.Serializable]
public class InteractionRelative : InteractionBase
{
    public enum InteractionRelativeType
    {
        DistanceX,
        DistanceY,
        DistanceZ,
        Radius,
        PolarPos,
        AzimuthPos,
        LinearSpeed,
        LinearAccelerationAbsolute,
        LinearAcceleration,
        LinearDeacceleration,
        TangentialSpeed,
        TangentialAccelerationAbsolute
    }

    public GameObject _TargetObject;
    public Rigidbody _TargetRigidBody;

    public InteractionRelativeType _SourceParameter;

    [Range(0f, 1f)]
    public float _Smoothing = 0.2f;
    float _PreviousDistance = 0f;

    public override void Initialise()
    {
        if (_TargetObject == null)
            _TargetObject = this.transform.parent.gameObject;

        _TargetRigidBody = _TargetObject.GetComponent<Rigidbody>();
    }


    private void Update()
    {
        float currentValue = _PreviousValue;

        if (_RigidBody != null && _TargetRigidBody != null)
        {
            switch (_SourceParameter)
            {
                case InteractionRelativeType.DistanceX:
                    currentValue = Mathf.Abs(RelativePosition().x);
                    break;
                case InteractionRelativeType.DistanceY:
                    currentValue = Mathf.Abs(RelativePosition().y);
                    break;
                case InteractionRelativeType.DistanceZ:
                    currentValue = Mathf.Abs(RelativePosition().z);
                    break;
                case InteractionRelativeType.Radius:
                    currentValue = Mathf.Abs(RelativePosition().magnitude);
                    break;
                case InteractionRelativeType.PolarPos:
                    currentValue = CartToSpherical(RelativePosition()).x;
                    break;
                case InteractionRelativeType.AzimuthPos:
                    currentValue = CartToSpherical(RelativePosition()).y;
                    break;
                case InteractionRelativeType.LinearSpeed:
                    currentValue = Mathf.Abs(RelativeVelocity().magnitude);
                    break;
                case InteractionRelativeType.LinearAccelerationAbsolute:
                    currentValue = Mathf.Abs((RelativeVelocity().magnitude - _PreviousValue) / Time.deltaTime);
                    _PreviousValue = currentValue;
                    break;
                case InteractionRelativeType.LinearAcceleration:
                    currentValue = Mathf.Max((RelativeVelocity().magnitude - _PreviousValue) / Time.deltaTime, 0f);
                    _PreviousValue = currentValue;
                    break;
                case InteractionRelativeType.LinearDeacceleration:
                    currentValue = Mathf.Abs(Mathf.Min((RelativeVelocity().magnitude - _PreviousValue) / Time.deltaTime, 0f));
                    _PreviousValue = currentValue;
                    break;
                case InteractionRelativeType.TangentialSpeed:
                    currentValue = CartToSpherical(RelativeVelocity()).magnitude;
                    break;
                case InteractionRelativeType.TangentialAccelerationAbsolute:
                    currentValue = Mathf.Abs((CartToSpherical(RelativeVelocity()).magnitude - _PreviousValue) / Time.deltaTime);
                    _PreviousValue = currentValue;
                    break;
                default:
                    break;
            }
        }
        else currentValue = 0;


        UpdateSmoothedOutputValue(currentValue, _Smoothing);
    }

    public void SetAuxValue(float val)
    {
        UpdateSmoothedOutputValue(val, _Smoothing);
    }

    public Vector3 RelativePosition()
    {
        return _SourceObject.transform.position - _TargetObject.transform.position;
    }
    public Vector3 RelativeVelocity()
    {
        return _RigidBody.velocity - _TargetRigidBody.velocity;
    }

    // TODO Add to global static utility
    public static Vector2 CartToSpherical(Vector3 position)
    {
        float polar = Mathf.Atan2(position.y, position.x);
        float azimuth = Mathf.Acos(position.z / position.magnitude);
        return new Vector2(polar, azimuth);
    }
}
