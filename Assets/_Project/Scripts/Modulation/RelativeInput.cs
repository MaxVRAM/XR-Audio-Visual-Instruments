using System;
using UnityEngine;


namespace PlaneWaver
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


        public enum ModulationInput
        {
            RelativeDistanceX,
            RelativeDistanceY,
            RelativeDistanceZ,
            RelativeRadius,
            RelativePolar,
            RelativeElevation,
            RelativeSpeed,
            RelativeTangentialSpeed
        }


        public RelativeProperty _InputProperty;

        [Range(0f, 1f)]
        public float _Smoothing = 0.2f;

        private Vector3 _PreviousDirection;

        private void Update()
        {
            float newValue = _PreviousValue;

            //if (_Actors.HaveRBs)
            //{
            //    Quaternion rotationAboutObject = Quaternion.FromToRotation(_PreviousDirection, _Actors.DirectionAB);
            //    _PreviousDirection = _Actors.DirectionAB;

            //    switch (_InputProperty)
            //    {
            //        case RelativeProperty.DistanceX:
            //            newValue = Mathf.Abs(_Actors.RelativePosition.x);
            //            break;
            //        case RelativeProperty.DistanceY:
            //            newValue = Mathf.Abs(_Actors.RelativePosition.y);
            //            break;
            //        case RelativeProperty.DistanceZ:
            //            newValue = Mathf.Abs(_Actors.RelativePosition.z);
            //            break;
            //        case RelativeProperty.Radius:
            //            newValue = _Actors.SphericalCoords.Radius;
            //            break;
            //        case RelativeProperty.Polar:
            //            newValue = _Actors.SphericalCoords.Polar;
            //            break;
            //        case RelativeProperty.Elevation:
            //            newValue = _Actors.SphericalCoords.Elevation;
            //            break;
            //        case RelativeProperty.SpeedToward:
            //            newValue = _Actors.RelativeSpeed;
            //            break;
            //        case RelativeProperty.TangentialSpeed:
            //            // TODO: Implementing in _Actors class. Will do shortly.
            //            break;
            //        default:
            //            break;
            //    }
            //}
            //else newValue = 0;
            //UpdateModulationValue(newValue, _Smoothing);
        }
    }
}
