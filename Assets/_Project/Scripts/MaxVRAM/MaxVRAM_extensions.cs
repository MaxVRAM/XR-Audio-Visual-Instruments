using System;
using Unity.Mathematics;

namespace MaxVRAM.Extensions
{
    public static class FloatExtensions
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
    }
}
