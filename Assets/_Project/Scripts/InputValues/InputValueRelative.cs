using System.Collections;
using System.Collections.Generic;
using Unity.Physics;
using UnityEngine;

//
/// <summary>
//     Provides an input value to use on a Grain Emitter, based on a physical interaction from the source rigid body.
/// <summary>
[System.Serializable]
public class InputValueRelative : InputValueClass
{
    public enum InputRelativeType
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
        LinearDeceleration,
        TangentialSpeed,
        TangentialAccelerationAbsolute
    }

    public InputRelativeType _InputProperty;

    [Range(0f, 1f)]
    public float _Smoothing = 0.2f;

    private void Update()
    {
        float currentValue = _PreviousValue;

        if (_Inputs._LocalRigidbody != null && _Inputs._RemoteRigidbody != null)
        {
            switch (_InputProperty)
            {
                case InputRelativeType.DistanceX:
                    currentValue = Mathf.Abs(RelativePosition().x);
                    break;
                case InputRelativeType.DistanceY:
                    currentValue = Mathf.Abs(RelativePosition().y);
                    break;
                case InputRelativeType.DistanceZ:
                    currentValue = Mathf.Abs(RelativePosition().z);
                    break;
                case InputRelativeType.Radius:
                    currentValue = Mathf.Abs(RelativePosition().magnitude);
                    break;
                case InputRelativeType.PolarPos:
                    currentValue = CartToSpherical(RelativePosition()).x;
                    break;
                case InputRelativeType.AzimuthPos:
                    currentValue = CartToSpherical(RelativePosition()).y;
                    break;
                case InputRelativeType.LinearSpeed:
                    currentValue = Mathf.Abs(RelativeVelocity().magnitude);
                    break;
                case InputRelativeType.LinearAccelerationAbsolute:
                    currentValue = Mathf.Abs((RelativeVelocity().magnitude - _PreviousValue) / Time.deltaTime);
                    _PreviousValue = currentValue;
                    break;
                case InputRelativeType.LinearAcceleration:
                    currentValue = Mathf.Max((RelativeVelocity().magnitude - _PreviousValue) / Time.deltaTime, 0f);
                    _PreviousValue = currentValue;
                    break;
                case InputRelativeType.LinearDeceleration:
                    currentValue = Mathf.Abs(Mathf.Min((RelativeVelocity().magnitude - _PreviousValue) / Time.deltaTime, 0f));
                    _PreviousValue = currentValue;
                    break;
                case InputRelativeType.TangentialSpeed:
                    currentValue = CartToSpherical(RelativeVelocity()).magnitude;
                    break;
                case InputRelativeType.TangentialAccelerationAbsolute:
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
        return _Inputs._LocalObject.transform.position - _Inputs._RemoteObject.transform.position;
    }
    public Vector3 RelativeVelocity()
    {
        return _Inputs._LocalRigidbody.velocity - _Inputs._RemoteRigidbody.velocity;
    }

    // TODO Add to global static utility
    public static Vector2 CartToSpherical(Vector3 position)
    {
        float polar = Mathf.Atan2(position.y, position.x);
        float azimuth = Mathf.Acos(position.z / position.magnitude);
        return new Vector2(polar, azimuth);
    }
}
