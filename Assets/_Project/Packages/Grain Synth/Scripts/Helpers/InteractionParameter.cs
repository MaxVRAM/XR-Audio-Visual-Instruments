﻿using System.Collections;
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

    public InteractionParameterType _SourceParameter;

    [Range(0f, 1f)]
    public float _Smoothing = 0.2f;

    public override void UpdateInteractionSource(GameObject sourceObject, Collision collision)
    {
        _SourceObject = sourceObject;
        _RigidBody = _SourceObject.GetComponent<Rigidbody>();
        _Colliding = true;
    }

    private void Update()
    {
        float currentValue = _PreviousValue;

        if (_RigidBody != null)
        {
            switch (_SourceParameter)
            {
                case InteractionParameterType.Speed:
                    currentValue = _RigidBody.velocity.magnitude;
                    break;
                case InteractionParameterType.AccelerationAbsolute:
                    currentValue = Mathf.Abs((_RigidBody.velocity.magnitude - _PreviousValue) / Time.deltaTime);
                    _PreviousValue = _RigidBody.velocity.magnitude;
                    break;
                case InteractionParameterType.Acceleration:
                    currentValue = Mathf.Max((_RigidBody.velocity.magnitude - _PreviousValue) / Time.deltaTime, 0f);
                    _PreviousValue = _RigidBody.velocity.magnitude;
                    break;
                case InteractionParameterType.Deacceleration:
                    currentValue = Mathf.Abs(Mathf.Min((_RigidBody.velocity.magnitude - _PreviousValue) / Time.deltaTime, 0f));
                    _PreviousValue = _RigidBody.velocity.magnitude;
                    break;
                case InteractionParameterType.Scale:
                    currentValue = _SourceObject.transform.localScale.magnitude;
                    break;
                case InteractionParameterType.Roll:
                    if (_Colliding)
                        currentValue = _RigidBody.angularVelocity.magnitude;
                    else
                        currentValue = 0;
                    break;
                case InteractionParameterType.RollTimesMass:
                    if (_Colliding)
                        currentValue = _RigidBody.angularVelocity.magnitude * (_RigidBody.mass / 2 + 0.5f);
                    else
                        currentValue = 0;
                    break;
                case InteractionParameterType.Slide:
                    if (_Colliding)
                        currentValue = _RigidBody.velocity.magnitude / _RigidBody.angularVelocity.magnitude;
                    else
                        currentValue = 0;
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
}
