using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
//     Provides an input value to use on a Grain Emitter, based on a physical interaction from the source rigid body.
/// <summary>
[System.Serializable]
public class InteractionParameter : InteractionBase
{
    public enum InteractionParameterType
    {
        Speed,
        AccelerationAbsolute,
        Acceleration,
        Deacceleration,
        Scale,
        Roll,
        RollTimesMass,
        Slide,
        Aux
    }

    public InteractionParameterType _InputProperty;

    [Range(0f, 1f)]
    public float _Smoothing = 0.2f;

    public override void UpdateInteractionSource(GameObject primaryObject)
    {
        _PrimaryObject = primaryObject;
        _PrimaryRigidBody = _PrimaryObject.GetComponent<Rigidbody>();
        _Colliding = true;
    }

    private void Update()
    {
        float value = _PreviousValue;

        if (_PrimaryRigidBody != null)
        {
            switch (_InputProperty)
            {
                case InteractionParameterType.Speed:
                    value = _PrimaryRigidBody.velocity.magnitude;
                    break;
                case InteractionParameterType.AccelerationAbsolute:
                    value = Mathf.Abs((_PrimaryRigidBody.velocity.magnitude - _PreviousValue) / Time.deltaTime);
                    _PreviousValue = _PrimaryRigidBody.velocity.magnitude;
                    break;
                case InteractionParameterType.Acceleration:
                    value = Mathf.Max((_PrimaryRigidBody.velocity.magnitude - _PreviousValue) / Time.deltaTime, 0f);
                    _PreviousValue = _PrimaryRigidBody.velocity.magnitude;
                    break;
                case InteractionParameterType.Deacceleration:
                    value = Mathf.Abs(Mathf.Min((_PrimaryRigidBody.velocity.magnitude - _PreviousValue) / Time.deltaTime, 0f));
                    _PreviousValue = _PrimaryRigidBody.velocity.magnitude;
                    break;
                case InteractionParameterType.Scale:
                    value = _PrimaryObject.transform.localScale.magnitude;
                    break;
                case InteractionParameterType.Roll:
                    if (_Colliding)
                        value = _PrimaryRigidBody.angularVelocity.magnitude;
                    else
                        value = 0;
                    break;
                case InteractionParameterType.RollTimesMass:
                    if (_Colliding)
                        value = _PrimaryRigidBody.angularVelocity.magnitude * (_PrimaryRigidBody.mass / 2 + 0.5f);
                    else
                        value = 0;
                    break;
                case InteractionParameterType.Slide:
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
