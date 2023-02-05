
using UnityEngine;

namespace MaxVRAM
{
    public struct Mathx
    {
        public static float Map(float val, float inMin, float inMax, float outMin, float outMax)
        {
            return outMin + (outMax - outMin) / (inMax - inMin) * (val - inMin);
        }

        public static float Map(float val, float inMin, float inMax, float outMin, float outMax, float exp)
        {
            return Mathf.Pow((val - inMin) / (inMax - inMin), exp) * (outMax - outMin) + outMin;
        }
    }

}