
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

    public enum TimeUnit { sec = 0, ms = 1, samples = 2 }

    public class ActionTimer
    {
        public TimeUnit _Unit = TimeUnit.ms;
        public bool _CarryRemainder = true;

        public float _PeriodMS = 1000;
        public float _CounterMS = 0;

        public int _PeriodSamples = AudioSettings.outputSampleRate;
        public int _CounterSamples = 0;


        public ActionTimer(TimeUnit unit, float time = -1)
        {
            _Unit = unit;
            _CounterMS = 0;
            _CounterSamples = 0;

            if (_Unit == TimeUnit.samples)
            {
                Debug.Log("ActionTimer: Sample-rate timer period can only be defined using integers. This timer will be disabled.");
                return;
            }

            _PeriodMS = _Unit == TimeUnit.ms ? time : time * 1000;
        }

        public ActionTimer(TimeUnit unit, int samples = -1)
        {
            _Unit = unit;
            _CounterMS = 0;
            _CounterSamples = 0;

            if (_Unit == TimeUnit.samples)
                _PeriodSamples = samples;
        }

        public void Reset(TimeUnit? unit = null)
        {
            if (unit != null)
                _Unit = unit.Value;

            _CounterMS = 0;
            _CounterSamples = 0;
        }

        public bool TimeDelta(float delta)
        {
            if (_Unit == TimeUnit.samples)
                return false;

            if (_Unit == TimeUnit.ms)
                _CounterMS += delta;
            else
                _CounterMS += delta * 1000;

            if (_CounterMS >= _PeriodMS)
            {
                _CounterMS = 0;
                return true;
            }
            else
                return false;
        }

        public bool SampleDelta(int delta)
        {
            if (_Unit != TimeUnit.samples)
                return false;

            _CounterSamples += delta;

            if (_CounterSamples >= _PeriodSamples)
            {
                _CounterSamples = 0;
                return true;
            }
            else
                return false;
        }
    }
}