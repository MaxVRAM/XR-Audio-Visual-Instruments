using System;
using UnityEngine;

using MaxVRAM.Math;
using static MaxVRAM.Math.MaxMath;

namespace PlaneWaver.Modulation
{
    /// <summary>
    ///     Provides an input value to use on a Grain Emitter, based on a physical interaction from the source rigid body.
    /// <summary>
    [Serializable]
    public class RelativeInput : ModulationSource
    {
        public enum RelativeProperty
        {
            DistanceX,
            DistanceY,
            DistanceZ,
            Radius,
            Polar,
            Elevation,
            SpeedToward,
            TangentialSpeed
        }

        public RelativeProperty _InputProperty;

        [Range(0f, 1f)]
        public float _Smoothing = 0.2f;

        private Vector3 _PreviousDirection;

        private void Update()
        {
            float newValue = _PreviousValue;

            if (_Actors.BothHaveRigidBodies)
            {
                Quaternion rotationAboutObject = Quaternion.FromToRotation(_PreviousDirection, _Actors.DirectionAB);
                _PreviousDirection = _Actors.DirectionAB;

                switch (_InputProperty)
                {
                    case RelativeProperty.DistanceX:
                        newValue = Mathf.Abs(_Actors.DeltaPosition.x);
                        break;
                    case RelativeProperty.DistanceY:
                        newValue = Mathf.Abs(_Actors.DeltaPosition.y);
                        break;
                    case RelativeProperty.DistanceZ:
                        newValue = Mathf.Abs(_Actors.DeltaPosition.z);
                        break;
                    case RelativeProperty.Radius:
                        newValue = _Actors.SphericalCoords.radius;
                        break;
                    case RelativeProperty.Polar:
                        newValue = _Actors.SphericalCoords.polar;
                        break;
                    case RelativeProperty.Elevation:
                        newValue = _Actors.SphericalCoords.elevation;
                        break;
                    case RelativeProperty.SpeedToward:
                        newValue = _Actors.SpeedTowards;
                        break;
                    case RelativeProperty.TangentialSpeed:
                        // TODO: Implementing in _Actors class. Will do shortly.
                        break;
                    default:
                        break;
                }
            }
            else newValue = 0;
            UpdateModulationValue(newValue, _Smoothing);
        }
    }
}
