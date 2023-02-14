using System;
using Unity.Mathematics;
using UnityEngine;

namespace MaxVRAM.Extensions
{
    public static class ExtendFloats
    {
        public static bool InRange(this float value, bool inclusive = true)
        {
            if (inclusive)
                return value >= 0f && value <= 1f;
            else
                return value > 0f && value < 1f;
        }
        public static bool InRange(this float value, float min = 0f, float max = 1f, bool inclusive = true)
        {
            if (inclusive)
                return value >= min && value <= max;
            else
                return value > min && value < max;
        }
        public static bool InRange(this float value, float2 minMax, bool inclusive = true)
        {
            if (inclusive)
                return value >= minMax.x && value <= minMax.y;
            else
                return value > minMax.x && value < minMax.y;
        }
        public static float GetMirroredValue(this float value, float mid)
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
