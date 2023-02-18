using System;
using Unity.Mathematics;
using UnityEngine;

namespace MaxVRAM.Extensions
{
    public static class ExtendFloats
    {
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

        /// <summary>
        /// Replicates the functionality of Mathf.PingPong() more efficently. Limited to normalised output range.
        /// </summary>
        public static float PingPongNorm(this float value)
        {
            value = value > 0 ? value : -value;
            value %= 2;
            if (value < 1)
                return value;
            else
                return 2 * 1 - value;
        }

        /// <summary>
        /// Replicates the functionality of Mathf.Repeat() more efficently. Limited to normalised output range.
        /// </summary>
        public static float RepeatNorm(this float value)
        {
            return value - Mathf.FloorToInt(value);
        }

        public static Vector2 MakeMirroredVector(this float value, float mid)
        {
            float diff = Mathf.Abs(value - mid);
            return new Vector2(mid - diff, mid + diff);
        }
    }
}
