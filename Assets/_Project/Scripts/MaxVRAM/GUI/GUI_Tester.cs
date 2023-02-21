using UnityEngine;
using MaxVRAM.GUI;

public class GUI_Tester : MonoBehaviour
{
    [MinMidMaxSlider(-20, -10, 20)] public Vector2 _CoolRange = new Vector2(-11, 0);
    [BidirectionalSliderLocked(-10, 10, true)] public Vector2 _AwesomeLinkedRange = new Vector2(-2, 2);
}
