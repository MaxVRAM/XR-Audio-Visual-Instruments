using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
//     Provides an input value to use on a Grain Emitter, based on a physical interaction from the source rigid body.
/// <summary>
[System.Serializable]
public class InputValueParameter : InputValueClass
{
    public enum InputParameterType
    {
        Speed,
        Acceleration,
        Deacceleration,
        AccelerationAbsolute,
        AccelerationDirectional,
        Scale,
        Roll,
        RollTimesMass,
        Slide,
        Aux
    }

    public InputParameterType _InputProperty;

    [Range(0f, 1f)]
    public float _Smoothing = 0.2f;

    private void Update()
    {
        float value = _PreviousValue;

        if (_PrimaryRigidBody != null)
        {
            switch (_InputProperty)
            {
                case InputParameterType.Speed:
                    value = _PrimaryRigidBody.velocity.magnitude;
                    break;
                case InputParameterType.Acceleration:
                    value = Mathf.Max((_PrimaryRigidBody.velocity.magnitude - _PreviousValue) / Time.deltaTime, 0f);
                    _PreviousValue = _PrimaryRigidBody.velocity.magnitude;
                    break;
                case InputParameterType.Deacceleration:
                    value = Mathf.Abs(Mathf.Min((_PrimaryRigidBody.velocity.magnitude - _PreviousValue) / Time.deltaTime, 0f));
                    _PreviousValue = _PrimaryRigidBody.velocity.magnitude;
                    break;
                case InputParameterType.AccelerationAbsolute:
                    value = Mathf.Abs((_PrimaryRigidBody.velocity.magnitude - _PreviousValue) / Time.deltaTime);
                    _PreviousValue = _PrimaryRigidBody.velocity.magnitude;
                    break;
                case InputParameterType.AccelerationDirectional:
                    value = (_PrimaryRigidBody.velocity.magnitude - _PreviousValue) / Time.deltaTime;
                    _PreviousValue = _PrimaryRigidBody.velocity.magnitude;
                    break;
                case InputParameterType.Scale:
                    value = _PrimaryObject.transform.localScale.magnitude;
                    break;
                case InputParameterType.Roll:
                    if (_Colliding)
                        value = _PrimaryRigidBody.angularVelocity.magnitude;
                    else
                        value = 0;
                    break;
                case InputParameterType.RollTimesMass:
                    if (_Colliding)
                        value = _PrimaryRigidBody.angularVelocity.magnitude * (_PrimaryRigidBody.mass / 2 + 0.5f);
                    else
                        value = 0;
                    break;
                case InputParameterType.Slide:
                    if (_Colliding)
                        value = _PrimaryRigidBody.velocity.magnitude / _PrimaryRigidBody.angularVelocity.magnitude;
                    else
                        value = 0;
                    break;
                default:
                    break;
            }
        }
        else value = 0;
        UpdateSmoothedOutputValue(value, _Smoothing);
    }

    public void SetAuxValue(float val)
    {
        UpdateSmoothedOutputValue(val, _Smoothing);
    }
}
