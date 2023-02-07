
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
        private TimeUnit _Unit = TimeUnit.ms;
        private bool _CarryRemainder = true;
        private int _LatestTriggerCount = 0;
        public int LatestTriggerCount { get { return _LatestTriggerCount; } }

        private float _TimePeriod = 1000;
        private float _TimeCounter = 0;

        private int _SamplePeriod = AudioSettings.outputSampleRate;
        private int _SampleCounter = 0;

        public ActionTimer(TimeUnit unit, float timePeriod)
        {
            if (unit == TimeUnit.samples)
            {
                Debug.Log("ActionTimer: Sample-rate timer period can only be defined using integers. This timer will be disabled.");
                return;
            }

            _Unit = unit;
            _TimeCounter = 0;
            ChangePeriod(timePeriod);
        }

        public ActionTimer(TimeUnit unit, int samplePeriod)
        {
            if (unit != TimeUnit.samples)
                return;

            _Unit = unit;
            _SampleCounter = 0;
            ChangePeriod(samplePeriod);
        }

        public int UpdateTrigger(float delta, float? period)
        {
            if (_Unit == TimeUnit.samples)
                return 0;

            if (period.HasValue)
                ChangePeriod(period.Value);

            _TimeCounter += _Unit == TimeUnit.ms ? delta : delta * 1000;

            if (_TimeCounter < _TimePeriod)
                return 0;
            else
            {
                float factor = (_TimeCounter / _TimePeriod);
                int count = (int)factor;
                _TimeCounter = _CarryRemainder ? _TimeCounter - _TimePeriod * factor : 0;
                _LatestTriggerCount = count;
                return count;
            }
        }

        public int UpdateTrigger(int delta, int? period)
        {
            if (_Unit != TimeUnit.samples)
                return 0;

            if (period.HasValue)
                ChangePeriod(period.Value);

            _SampleCounter += delta;

            if (_SampleCounter < _SamplePeriod)
                return 0;
            else
            {
                int count = _SampleCounter / _SamplePeriod;
                _SampleCounter = _CarryRemainder ? _SampleCounter - _SamplePeriod * count : 0;
                _LatestTriggerCount = count;
                return count;
            }
        }

        public bool DrainTrigger()
        {
            if (_LatestTriggerCount > 0)
            {
                _LatestTriggerCount--;
                return true;
            }
            else
                return false;
        }

        public void Reset(TimeUnit? unit = null)
        {
            if (unit.HasValue)
                _Unit = unit.Value;

            _TimeCounter = 0;
            _SampleCounter = 0;
        }

        public void ChangePeriod(float timePeriod)
        {
            timePeriod = _Unit == TimeUnit.ms ? timePeriod : timePeriod * 1000;
            timePeriod = Mathf.Max(timePeriod, 0.01f);
            if (timePeriod == _TimePeriod)
                return;
            _TimePeriod = timePeriod;
        }

        public void ChangePeriod(int samplePeriod)
        {
            samplePeriod = Mathf.Max(samplePeriod, 10);
            if (samplePeriod == _SamplePeriod)
                return;
            _SamplePeriod = samplePeriod;
        }

    }
}