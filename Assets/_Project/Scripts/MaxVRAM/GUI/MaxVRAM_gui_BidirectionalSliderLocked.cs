using UnityEngine;
using UnityEditor;

using MaxVRAM.Math;
using MaxVRAM.Extensions;

namespace MaxVRAM.GUI
{
    [CustomPropertyDrawer(typeof(BidirectionalSliderLockedAttribute))]
    public class BidirectionalSliderLockedDrawer : PropertyDrawer
    {
        float _PreviousLower = float.MaxValue;
        float _PreviousUpper = float.MaxValue;

        bool _LowerLastChanged = false;
        bool _UpperLastChanged = false;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            BidirectionalSliderLockedAttribute minMaxAttribute = (BidirectionalSliderLockedAttribute)attribute;
            SerializedPropertyType propertyType = property.propertyType;

            bool lockAtCentre = minMaxAttribute.lockAtCentre;
            float mid = minMaxAttribute.min + (minMaxAttribute.max - minMaxAttribute.min) * 0.5f;
            Debug.Log($"Min: {minMaxAttribute.min}    Mid: {mid}    Max: {minMaxAttribute.max}");

            label.tooltip = minMaxAttribute.min.ToString("F2") + " to " + minMaxAttribute.max.ToString("F2");
            Rect controlRect = EditorGUI.PrefixLabel(position, label);
            Rect[] splittedRect = SplitRect(controlRect, 3);

            if (propertyType == SerializedPropertyType.Vector2)
            {
                EditorGUI.BeginChangeCheck();

                Vector2 vector = property.vector2Value;
                float lower = vector.x;
                float upper = vector.y;
                bool lowerChanged = false;
                bool upperChanged = false;

                lower = EditorGUI.FloatField(splittedRect[0], float.Parse(lower.ToString("F2")));
                upper = EditorGUI.FloatField(splittedRect[2], float.Parse(upper.ToString("F2")));

                float holdLower = lower;
                float holdUpper = upper;

                EditorGUI.MinMaxSlider(splittedRect[1], ref lower, ref upper, minMaxAttribute.min, minMaxAttribute.max);

                float lowerDiff = Mathf.Abs(holdLower - lower);
                float upperDiff = Mathf.Abs(holdUpper - upper);

                if (!Mathf.Approximately(holdLower, lower) && !Mathf.Approximately(holdUpper, upper) &&
                    Mathf.Approximately(lowerDiff, upperDiff))
                {
                    lower = _PreviousLower;
                    upper = _PreviousUpper;
                }
                else
                {
                    if (!Mathf.Approximately(lowerDiff, upperDiff))
                    {
                        if (Mathf.Abs(lowerDiff) > Mathf.Abs(upperDiff))
                        {
                            lower = Mathf.Min(lower, mid);
                            upper = mid + (mid - lower);
                        }

                    }
                }

                //if (!Mathf.Approximately(holdLower, lower) && !Mathf.Approximately(holdUpper, upper))
                //{
                //    lower = _PreviousLower;
                //    upper = _PreviousUpper;
                //}
                //else if (!Mathf.Approximately(holdLower, lower) && !_UpperLastChanged)
                //{
                //    lower = Mathf.Min(lower, mid);
                //    upper = mid + (mid - lower);
                //    _LowerLastChanged = true;
                //}
                //else if (!Mathf.Approximately(holdUpper, upper) && !_LowerLastChanged)
                //{
                //    upper = Mathf.Max(mid, upper);
                //    lower = mid - (upper - mid);
                //    _UpperLastChanged = true;
                //}
                //else
                //{
                //    _LowerLastChanged = false;
                //    _UpperLastChanged = false;
                //}


                _PreviousLower = lower;
                _PreviousUpper = upper;


                //    Debug.Log("Lower changed");

                //if (!Mathf.Approximately(holdUpper, upper))
                //    Debug.Log("Upper changed");


                //if (_PreviousLower == float.MaxValue || _PreviousUpper == float.MaxValue)
                //{
                //    lower = Mathf.Min(lower, mid);
                //    upper = mid + (mid - lower);
                //    _PreviousLower = lower;
                //    _PreviousUpper = upper;
                //}

                ////float lowerDiff = lower - _PreviousLower;
                ////float upperDiff = upper - _PreviousUpper;
                //float lowerDiff = 0;
                //float upperDiff = 0;

                //if (!Mathf.Approximately(_PreviousLower, lower))
                //{
                //    lower = Mathf.Min(lower, mid);
                //    lowerDiff = lower - _PreviousLower;
                //    lowerChanged = true;
                //    Debug.Log($"Lower changed by {lowerDiff}");
                //}

                //if (!Mathf.Approximately(_PreviousUpper, upper))
                //{
                //    upper = Mathf.Max(mid, upper);
                //    upperDiff = upper - _PreviousUpper;
                //    upperChanged = true;
                //    Debug.Log($"Upper changed by {upperDiff}");
                //}

                //if (Mathf.Approximately(lowerDiff, upperDiff))
                //{
                //    lower = _PreviousLower;
                //    upper = _PreviousUpper;
                //}
                //else if (lockAtCentre && (lowerChanged ^ upperChanged))
                //{
                //    if (lowerChanged)
                //    {
                //        upper = mid + (mid - lower);
                //    }
                //    else if (upperChanged)
                //    {
                //        lower = mid - (upper - mid);
                //    }
                //}




                // Do mirroring with flags


                vector = new Vector2(lower, upper);

                if (EditorGUI.EndChangeCheck())
                    property.vector2Value = vector;

                //lower = Mathf.Clamp(lower, minMaxAttribute.min, minMaxAttribute.max);
                //upper = Mathf.Clamp(upper, minMaxAttribute.min, minMaxAttribute.max);

                //if (Mathf.Abs(lower - _PreviousVector.x) > Mathf.Epsilon)
                //{
                //    Debug.Log($"Lower value updated from {_PreviousVector.x} to {lower}");
                //    upper = mid + (mid - lower);
                //}
                //else if (Mathf.Abs(upper - _PreviousVector.y) > Mathf.Epsilon)
                //{
                //    Debug.Log($"Upper value updated from {_PreviousVector.y} to {upper}");
                //    lower = mid - (upper - mid);

                //upper = mid + (mid - lower);
                //else if (upper != _PreviousVector.y)
                //    lower = mid - (upper - mid);

                //newValue = lower > upper ? new Vector2(upper, lower) : new Vector2(lower, upper);

                //newValue = new Vector2(lower, upper).Clamp(minMaxAttribute.min, minMaxAttribute.max);

            }
        }

        //Vector2 MakeMirroredVector(float value, float mid)
        //{
        //    float diff = Mathf.Abs(value - mid);
        //    return new Vector2(mid - diff, mid + diff);
        //}

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
}
