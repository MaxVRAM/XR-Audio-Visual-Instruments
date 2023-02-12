using UnityEngine;

namespace MaxVRAM.GUI.MinMidMaxSlider
{    
    public class MinMidMaxSliderAttribute : PropertyAttribute
    {
        public float min;
        public float mid;
        public float max;

        public MinMidMaxSliderAttribute(float min, float mid, float max)
        {
            this.min = min;
            this.mid = mid;
            this.max = max;
        }
    }
}
