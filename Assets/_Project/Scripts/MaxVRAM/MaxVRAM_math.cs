
using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace MaxVRAM.Math
{
    public struct MaxMath
    {
        public static float Map(float val, float inMin, float inMax, float outMin, float outMax)
        {
            return outMin + (outMax - outMin) / (inMax - inMin) * (val - inMin);
        }

        public static float Map(float val, float inMin, float inMax, float outMin, float outMax, float exp)
        {
            return Mathf.Pow((val - inMin) / (inMax - inMin), exp) * (outMax - outMin) + outMin;
        }

        public static bool ClampCheck(ref float value, float min, float max)
        {
            if (value < min)
            {
                value = min;
                return true;
            }
            if (value > max)
            {
                value = max;
                return true;
            }
            return false;
        }

        public static bool InRange(float value, float min, float max)
        {
            return value >= min && value <= max;
        }

        public static bool InRange(float value, Vector2 range)
        {
            return value >= range.x && value <= range.y;
        }

        public static float FadeInOut(float norm, float inEnd, float outStart)
        {
            norm = Mathf.Clamp01(norm);
            float fade = 1;

            if (inEnd != 0 && norm < inEnd)
                fade = norm / inEnd;

            if (outStart != 1 && norm > outStart)
                fade = (1 - norm) / (1 - outStart);

            return fade;
        }

        public static float FadeInOut(float normPosition, float inOutPoint)
        {
            return FadeInOut(normPosition, inOutPoint, 1 - inOutPoint);
        }

        public static void SphericalToCartesian(float radius, float polar, float elevation, out Vector3 outCart)
        {
            // https://blog.nobel-joergensen.com/2010/10/22/spherical-coordinates-in-unity/
            float a = radius * Mathf.Cos(elevation);
            outCart.x = a * Mathf.Cos(polar);
            outCart.y = radius * Mathf.Sin(elevation);
            outCart.z = a * Mathf.Sin(polar);
        }

        public static void CartesianToSpherical(Vector3 cartCoords, out float outRadius, out float outPolar, out float outElevation)
        {
            // https://blog.nobel-joergensen.com/2010/10/22/spherical-coordinates-in-unity/
            if (cartCoords.x == 0)
                cartCoords.x = Mathf.Epsilon;
            outRadius = Mathf.Sqrt((cartCoords.x * cartCoords.x)
                            + (cartCoords.y * cartCoords.y)
                            + (cartCoords.z * cartCoords.z));
            outPolar = Mathf.Atan(cartCoords.z / cartCoords.x);
            if (cartCoords.x < 0)
                outPolar += Mathf.PI;
            outElevation = Mathf.Asin(cartCoords.y / outRadius);
        }

        public class SphericalCoords
        {
            public float radius;
            public float polar;
            public float elevation;

            public SphericalCoords(Vector3 cartesianCoords)
            {
                CartesianToSpherical(cartesianCoords, out radius, out polar, out elevation);
            }

            public Vector3 GetAsVector()
            {
                return new Vector3(radius, polar, elevation);
            }
            
            public void GetAsVector(out Vector3 sphericalCoods)
            {
                sphericalCoods = GetAsVector();
            }

            public SphericalCoords FromCartesian(Vector3 cartesianCoords)
            {
                CartesianToSpherical(cartesianCoords, out radius, out polar, out elevation);
                return this;
            }

            public Vector3 ToCartesian()
            {
                SphericalToCartesian(radius, polar, elevation, out Vector3 cartesianCoords);
                return cartesianCoords;
            }
        }

        public static float AngularSpeedFromQuaternion(Quaternion quat)
        {
            // TOOD - I'm so tired. do later....
            float angleInDegrees;
            Vector3 rotationAxis;
            quat.ToAngleAxis(out angleInDegrees, out rotationAxis);
            Vector3 angularDisplacement = rotationAxis * angleInDegrees * Mathf.Deg2Rad;
            Vector3 angularSpeed = angularDisplacement / Time.deltaTime;
            return 0;
        }

    }

    public struct Rando
    {
        public static float Range(Vector2 range)
        {
            return Random.Range(range.x, range.y);
        }

    }
}