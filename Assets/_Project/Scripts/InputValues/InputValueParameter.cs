using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
//     Provides an input value to use on a Grain Emitter, based on a physical interaction from the source rigid body.
/// <summary>
[System.Serializable]
public class InputValueParameter : InputValueClass
{
    public enum InputValueType
    {
        Speed,
        Acceleration,
        Deceleration,
        AccelerationAbsolute,
        AccelerationDirectional,
        Scale,
        Roll,
        RollTimesMass,
        Slide,
        Aux
    }

    public InputValueType _InputProperty;

    [Range(0f, 1f)]
    public float _Smoothing = 0.2f;

    private void Update()
    {
        float value = _PreviousValue;
        Rigidbody rb = _Inputs._LocalRigidbody;
        if (rb != null)
        {
            switch (_InputProperty)
            {
                case InputValueType.Speed:
                    value = rb.velocity.magnitude;
                    break;
                case InputValueType.Acceleration:
                    value = Mathf.Max((rb.velocity.magnitude - _PreviousValue) / Time.deltaTime, 0f);
                    _PreviousValue = rb.velocity.magnitude;
                    break;
                case InputValueType.Deceleration:
                    value = Mathf.Abs(Mathf.Min((rb.velocity.magnitude - _PreviousValue) / Time.deltaTime, 0f));
                    _PreviousValue = rb.velocity.magnitude;
                    break;
                case InputValueType.AccelerationAbsolute:
                    value = Mathf.Abs((rb.velocity.magnitude - _PreviousValue) / Time.deltaTime);
                    _PreviousValue = rb.velocity.magnitude;
                    break;
                case InputValueType.AccelerationDirectional:
                    value = (rb.velocity.magnitude - _PreviousValue) / Time.deltaTime;
                    _PreviousValue = rb.velocity.magnitude;
                    break;
                case InputValueType.Scale:
                    value = _Inputs._LocalObject.transform.localScale.magnitude;
                    break;
                case InputValueType.Roll:
                    if (_Colliding)
                        value = rb.angularVelocity.magnitude;
                    else
                        value = 0;
                    break;
                case InputValueType.RollTimesMass:
                    if (_Colliding)
                        value = rb.angularVelocity.magnitude * (rb.mass / 2 + 0.5f);
                    else
                        value = 0;
                    break;
                case InputValueType.Slide:
                    if (_Colliding)
                        value = rb.velocity.magnitude / rb.angularVelocity.magnitude;
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
