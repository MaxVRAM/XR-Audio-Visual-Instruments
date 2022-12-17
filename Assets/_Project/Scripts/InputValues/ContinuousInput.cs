using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
//     Provides an input value to use on a Grain Emitter, based on a physical interaction from the source rigid body.
/// <summary>
[System.Serializable]
public class ContinuousInput : ModulationSource
{
    public enum ContinuousProperty
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

    public ContinuousProperty _InputProperty;

    [Range(0f, 1f)]
    public float _Smoothing = 0.2f;

    private void Update()
    {
        float value = _PreviousValue;
        Rigidbody rb = _Objects._LocalRigidbody;
        if (rb != null)
        {
            switch (_InputProperty)
            {
                case ContinuousProperty.Speed:
                    value = rb.velocity.magnitude;
                    break;
                case ContinuousProperty.Acceleration:
                    value = Mathf.Max((rb.velocity.magnitude - _PreviousValue) / Time.deltaTime, 0f);
                    _PreviousValue = rb.velocity.magnitude;
                    break;
                case ContinuousProperty.Deceleration:
                    value = Mathf.Abs(Mathf.Min((rb.velocity.magnitude - _PreviousValue) / Time.deltaTime, 0f));
                    _PreviousValue = rb.velocity.magnitude;
                    break;
                case ContinuousProperty.AccelerationAbsolute:
                    value = Mathf.Abs((rb.velocity.magnitude - _PreviousValue) / Time.deltaTime);
                    _PreviousValue = rb.velocity.magnitude;
                    break;
                case ContinuousProperty.AccelerationDirectional:
                    value = (rb.velocity.magnitude - _PreviousValue) / Time.deltaTime;
                    _PreviousValue = rb.velocity.magnitude;
                    break;
                case ContinuousProperty.Scale:
                    value = _Objects._LocalObject.transform.localScale.magnitude;
                    break;
                case ContinuousProperty.Roll:
                    if (_Colliding)
                        value = rb.angularVelocity.magnitude;
                    else
                        value = 0;
                    break;
                case ContinuousProperty.RollTimesMass:
                    if (_Colliding)
                        value = rb.angularVelocity.magnitude * (rb.mass / 2 + 0.5f);
                    else
                        value = 0;
                    break;
                case ContinuousProperty.Slide:
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
