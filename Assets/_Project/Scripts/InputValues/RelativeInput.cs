using System.Collections;
using System.Collections.Generic;
using Unity.Physics;
using UnityEngine;

//
/// <summary>
//     Provides an input value to use on a Grain Emitter, based on a physical interaction from the source rigid body.
/// <summary>
[System.Serializable]
public class RelativeInput : ModulationSource
{
    public enum RelativeProperty
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

    public RelativeProperty _InputProperty;

    [Range(0f, 1f)]
    public float _Smoothing = 0.2f;

    private void Update()
    {
        float currentValue = _PreviousValue;

        if (_Objects._LocalRigidbody != null && _Objects._RemoteRigidbody != null)
        {
            switch (_InputProperty)
            {
                case RelativeProperty.DistanceX:
                    currentValue = Mathf.Abs(RelativePosition().x);
                    break;
                case RelativeProperty.DistanceY:
                    currentValue = Mathf.Abs(RelativePosition().y);
                    break;
                case RelativeProperty.DistanceZ:
                    currentValue = Mathf.Abs(RelativePosition().z);
                    break;
                case RelativeProperty.Radius:
                    currentValue = Mathf.Abs(RelativePosition().magnitude);
                    break;
                case RelativeProperty.PolarPos:
                    currentValue = CartToSpherical(RelativePosition()).x;
                    break;
                case RelativeProperty.AzimuthPos:
                    currentValue = CartToSpherical(RelativePosition()).y;
                    break;
                case RelativeProperty.LinearSpeed:
                    currentValue = Mathf.Abs(RelativeVelocity().magnitude);
                    break;
                case RelativeProperty.LinearAccelerationAbsolute:
                    currentValue = Mathf.Abs((RelativeVelocity().magnitude - _PreviousValue) / Time.deltaTime);
                    _PreviousValue = currentValue;
                    break;
                case RelativeProperty.LinearAcceleration:
                    currentValue = Mathf.Max((RelativeVelocity().magnitude - _PreviousValue) / Time.deltaTime, 0f);
                    _PreviousValue = currentValue;
                    break;
                case RelativeProperty.LinearDeceleration:
                    currentValue = Mathf.Abs(Mathf.Min((RelativeVelocity().magnitude - _PreviousValue) / Time.deltaTime, 0f));
                    _PreviousValue = currentValue;
                    break;
                case RelativeProperty.TangentialSpeed:
                    currentValue = CartToSpherical(RelativeVelocity()).magnitude;
                    break;
                case RelativeProperty.TangentialAccelerationAbsolute:
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
        return _Objects._LocalObject.transform.position - _Objects._RemoteObject.transform.position;
    }
    public Vector3 RelativeVelocity()
    {
        return _Objects._LocalRigidbody.velocity - _Objects._RemoteRigidbody.velocity;
    }

    // TODO Add to global static utility
    public static Vector2 CartToSpherical(Vector3 position)
    {
        float polar = Mathf.Atan2(position.y, position.x);
        float azimuth = Mathf.Acos(position.z / position.magnitude);
        return new Vector2(polar, azimuth);
    }
}
