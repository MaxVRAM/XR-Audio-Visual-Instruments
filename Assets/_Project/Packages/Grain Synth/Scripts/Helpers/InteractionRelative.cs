using System.Collections;
using System.Collections.Generic;
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
        LinearAcceleration,
        TangentialSpeed,

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

    public GameObject _TargetObject;
    public InteractionRelativeType _SourceParameter;

    [Range(0f, 1f)]
    public float _Smoothing = 0.2f;
    protected float previousDistance = 0f;

    private void Update()
    {
        float currentValue = _PreviousInputValue;

        if (_SourceObject != null && _RigidBody != null && _TargetObject != null)
        {
            Vector3 positionDiff = _SourceObject.transform.position - _TargetObject.transform.position;
            float radius = Mathf.Abs((positionDiff).magnitude);

            switch (_SourceParameter)
            {
                case InteractionRelativeType.DistanceX:
                    currentValue = Mathf.Abs(_SourceObject.transform.position.x - _TargetObject.transform.position.x);
                    break;
                case InteractionRelativeType.DistanceY:
                    currentValue = Mathf.Abs(_SourceObject.transform.position.y - _TargetObject.transform.position.y);
                    break;
                case InteractionRelativeType.DistanceZ:
                    currentValue = Mathf.Abs(_SourceObject.transform.position.z - _TargetObject.transform.position.z);
                    break;
                case InteractionRelativeType.Radius:
                    currentValue = radius;
                    break;
                case InteractionRelativeType.PolarPos:
                    currentValue = CartToSpherical(positionDiff).x;
                    break;
                case InteractionRelativeType.AzimuthPos:
                    currentValue = CartToSpherical(positionDiff).y;
                    break;
                case InteractionRelativeType.LinearSpeed:
                    currentValue = Mathf.Max((_RigidBody.velocity.magnitude - previousDistance) / Time.deltaTime, 0f);
                    _PreviousInputValue = _RigidBody.velocity.magnitude;
                    break;
                case InteractionRelativeType.LinearAcceleration:
                    currentValue = CartToSpherical(positionDiff).x;
                    break;
                case InteractionRelativeType.TangentialSpeed:
                    currentValue = CartToSpherical(positionDiff).y;
                    break;






                case InteractionRelativeType.Speed:
                    currentValue = _RigidBody.velocity.magnitude;
                    break;
                case InteractionRelativeType.AccelerationAbsolute:
                    currentValue = Mathf.Abs((_RigidBody.velocity.magnitude - _PreviousInputValue) / Time.deltaTime);
                    _PreviousInputValue = _RigidBody.velocity.magnitude;
                    break;
                case InteractionRelativeType.Acceleration:
                    currentValue = Mathf.Max((_RigidBody.velocity.magnitude - _PreviousInputValue) / Time.deltaTime, 0f);
                    _PreviousInputValue = _RigidBody.velocity.magnitude;
                    break;
                case InteractionRelativeType.Deacceleration:
                    currentValue = Mathf.Abs(Mathf.Min((_RigidBody.velocity.magnitude - _PreviousInputValue) / Time.deltaTime, 0f));
                    _PreviousInputValue = _RigidBody.velocity.magnitude;
                    break;
                case InteractionRelativeType.Scale:
                    currentValue = _SourceObject.transform.localScale.magnitude;
                    break;
                case InteractionRelativeType.Roll:
                    if (_Colliding)
                        currentValue = _RigidBody.angularVelocity.magnitude;
                    else
                        currentValue = 0;
                    break;
                case InteractionRelativeType.RollTimesMass:
                    if (_Colliding)
                        currentValue = _RigidBody.angularVelocity.magnitude * (_RigidBody.mass / 2 + 0.5f);
                    else
                        currentValue = 0;
                    break;
                case InteractionRelativeType.Slide:
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

    public static Vector2 CartToSpherical(Vector3 position)
    {
        float polar = Mathf.Atan2(position.y, position.x);
        float azimuth = Mathf.Acos(position.z / position.magnitude);
        return new Vector2(polar, azimuth);
    }
}
