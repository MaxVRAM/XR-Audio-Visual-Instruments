using System;
using Unity.Mathematics;
using UnityEngine;

namespace MaxVRAM.Extensions
{
    public static class ExtendFloats
    {
        public static float AccumulateScaledValue(this float accumulator, float valueToAdd, float scaleFactor)
        {
            return accumulator + valueToAdd * scaleFactor;
        }

        public static float Smooth(this float targetValue, float currentValue, float smoothing, float deltaTime, float epsilon = -1)
        {
            epsilon = epsilon == -1 ? Mathf.Epsilon : epsilon;

            if (smoothing > epsilon && Mathf.Abs(currentValue - targetValue) > epsilon)
                return Mathf.Lerp(currentValue, targetValue, (1 - smoothing) * 10f * deltaTime);
            else
                return targetValue;
        }

        public static bool IsInRange(this float value, bool inclusive = true)
        {
            if (inclusive)
                return value is >= 0f and <= 1f;
            else
                return value is > 0f and < 1f;
        }

        public static bool IsInRange(this float value, float min = 0f, float max = 1f, bool inclusive = true)
        {
            if (inclusive)
                return value >= min && value <= max;
            else
                return value > min && value < max;
        }

        public static bool IsInRange(this float value, float2 minMax, bool inclusive = true)
        {
            if (inclusive)
                return value >= minMax.x && value <= minMax.y;
            else
                return value > minMax.x && value < minMax.y;
        }

        public static float Mirrored(this float value, float mid)
        {
            float diff = value - mid;
            return diff > 0 ? mid - diff: mid + mid;
        }

        public static Vector2 MakeMirroredVector(this float value, float mid)
        {
            float diff = Mathf.Abs(value - mid);
            return new Vector2(mid - diff, mid + diff);
        }
    }
}
