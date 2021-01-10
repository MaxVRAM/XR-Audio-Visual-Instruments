﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        Slide,
        Aux
    }

    public InteractionParameterType _SourceParameter;

    [Range(0f, 1f)]
    public float _Smoothing = 0.2f;

    private void FixedUpdate()
    {
        float currentValue = _PreviousInputValue;

        switch (_SourceParameter)
        {
            case InteractionParameterType.Speed:
                currentValue = _RigidBody.velocity.magnitude;
                break;
            case InteractionParameterType.AccelerationAbsolute:
                currentValue = Mathf.Abs((_RigidBody.velocity.magnitude - _PreviousInputValue) / Time.deltaTime);
                _PreviousInputValue = _RigidBody.velocity.magnitude;
                break;
            case InteractionParameterType.Acceleration:
                currentValue = Mathf.Max((_RigidBody.velocity.magnitude - _PreviousInputValue) / Time.deltaTime, 0f);
                _PreviousInputValue = _RigidBody.velocity.magnitude;
                break;
            case InteractionParameterType.Deacceleration:
                currentValue = Mathf.Abs(Mathf.Min((_RigidBody.velocity.magnitude - _PreviousInputValue) / Time.deltaTime, 0f));
                _PreviousInputValue = _RigidBody.velocity.magnitude;
                break;
            case InteractionParameterType.Scale:
                currentValue = _SourceObject.transform.localScale.magnitude;
                break;
            case InteractionParameterType.Roll:
                if (_CurrentCollisionCount > 0)
                    currentValue = _RigidBody.angularVelocity.magnitude;
                else
                    currentValue = 0;
                break;
            case InteractionParameterType.Slide:
                if (_CurrentCollisionCount > 0)
                    currentValue = _RigidBody.velocity.magnitude;
                else
                    currentValue = 0;
                break;
            default:
                break;
        }

        UpdateSmoothedOutputValue(currentValue, _Smoothing);
    }

    public override void SetCollisionData(Collision collision, int numCollisions)
    {
        _CurrentCollisionCount = numCollisions;
    }

    public void SetAuxValue(float val)
    {
        UpdateSmoothedOutputValue(val, _Smoothing);
    }
}
