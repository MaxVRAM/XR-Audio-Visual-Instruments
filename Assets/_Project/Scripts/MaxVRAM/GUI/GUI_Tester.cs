using UnityEngine;
using MaxVRAM.GUI.MinMidMaxSlider;

public class GUI_Tester : MonoBehaviour
{
    [MinMidMaxSlider(-20, -10, 20)] public Vector2 _CoolRange = new Vector2(-11, 0);
}
