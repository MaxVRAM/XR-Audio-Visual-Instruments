using UnityEngine;
using UnityEditor;
using MaxVRAM.Math;
using MaxVRAM.Extensions;
using MaxVRAM.GUI.MinMidMaxSlider;

[CustomPropertyDrawer(typeof(MinMidMaxSliderAttribute))]
public class MinMidMaxSliderDrawer : PropertyDrawer
{
    float _PreviousRange = float.MaxValue;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var minMidMaxAttribute = (MinMidMaxSliderAttribute)attribute;
        var propertyType = property.propertyType;

        label.tooltip = minMidMaxAttribute.min.ToString("F2") + " to " + minMidMaxAttribute.max.ToString("F2");
        Rect controlRect = EditorGUI.PrefixLabel(position, label);
        Rect[] splittedRect = SplitRect(controlRect, 3);

        if (propertyType == SerializedPropertyType.Vector2)
        {
            EditorGUI.BeginChangeCheck();

            Vector2 newValue = property.vector2Value;
            float lower = newValue.x;
            float upper = newValue.y;

            if (_PreviousRange == float.MaxValue)
                _PreviousRange = minMidMaxAttribute.max - minMidMaxAttribute.min;

            lower = EditorGUI.FloatField(splittedRect[0], float.Parse(lower.ToString("F2")));
            upper = EditorGUI.FloatField(splittedRect[2], float.Parse(upper.ToString("F2")));

            EditorGUI.MinMaxSlider(splittedRect[1], ref lower, ref upper, minMidMaxAttribute.min, minMidMaxAttribute.max);

            if (lower.InRange(minMidMaxAttribute.min, minMidMaxAttribute.mid) &&
                upper.InRange(minMidMaxAttribute.mid, minMidMaxAttribute.max))
            {
                _PreviousRange = upper - lower;
            }

            // TODO: Occasionally buggy when quickly moving one value to mid limit. Works well enough, but could be improved.

            if (MaxMath.ClampCheck(ref lower, minMidMaxAttribute.min, minMidMaxAttribute.mid))
                upper = Mathf.Clamp(lower + _PreviousRange, minMidMaxAttribute.mid, minMidMaxAttribute.max);

            if (MaxMath.ClampCheck(ref upper, minMidMaxAttribute.mid, minMidMaxAttribute.max))
                lower = Mathf.Clamp(upper - _PreviousRange, minMidMaxAttribute.min, minMidMaxAttribute.mid);

            newValue = new Vector2(lower, upper);

            if (EditorGUI.EndChangeCheck())
                property.vector2Value = newValue;

        }
        else if (propertyType == SerializedPropertyType.Vector2Int)
        {
            EditorGUI.BeginChangeCheck();

            Vector2Int propertyValue = property.vector2IntValue;
            float lower = propertyValue.x;
            float upper = propertyValue.y;

            lower = EditorGUI.FloatField(splittedRect[0], lower);
            upper = EditorGUI.FloatField(splittedRect[2], upper);

            EditorGUI.MinMaxSlider(splittedRect[1], ref lower, ref upper, minMidMaxAttribute.min, minMidMaxAttribute.max);

            lower = Mathf.Clamp(lower, minMidMaxAttribute.min, minMidMaxAttribute.mid);
            upper = Mathf.Clamp(upper, minMidMaxAttribute.mid, minMidMaxAttribute.max);

            //propertyValue = new Vector2Int(Mathf.FloorToInt(lower > upper ? upper : lower), Mathf.FloorToInt(upper));
            propertyValue = new Vector2Int(Mathf.FloorToInt(lower), Mathf.FloorToInt(upper));

            if (EditorGUI.EndChangeCheck())
                property.vector2IntValue = propertyValue;
        }
    }

    Rect[] SplitRect(Rect rectToSplit, int n)
    {
        Rect[] rects = new Rect[n];

        for (int i = 0; i < n; i++)
        {
            rects[i] = new Rect(
                rectToSplit.position.x + (i * rectToSplit.width / n),
                rectToSplit.position.y, rectToSplit.width / n, rectToSplit.height);
        }

        int padding = (int)rects[0].width - 50;
        int space = 5;

        rects[0].width -= padding + space;
        rects[2].width -= padding + space;

        rects[1].x -= padding;
        rects[1].width += padding * 2;

        rects[2].x += padding + space;

        return rects;
    }
}