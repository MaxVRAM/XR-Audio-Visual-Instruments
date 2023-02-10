
using System;
using UnityEngine;
using Random = UnityEngine.Random;

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

        public static float FadeInOut(float norm, float inEnd, float outStart)
        {
            norm = Mathf.Clamp01(norm);
            float fade = 1;

            if (inEnd != 0 && norm < inEnd)
                fade = norm / inEnd;

            if (outStart != 1 && norm > outStart)
                fade = (1 - norm) / (1 - outStart);

            return fade;
        }

        public static float FadeInOut(float normPosition, float inOutPoint)
        {
            return FadeInOut(normPosition, inOutPoint, 1 - inOutPoint);
        }
    }

    public struct Totes
    {
        public static float Rando(Vector2 range)
        {
            return Random.Range(range.x, range.y);
        }
    }

    [Serializable]
    public class Envelope
    {
        // Source: https://michaelkrzyzaniak.com/AudioSynthesis/2_Audio_Synthesis/11_Granular_Synthesis/1_Window_Functions/

        public enum WindowFunctionSelection { sine = 0, hann = 1, hamming = 2, tukey = 3, gaussian = 4 }
        public WindowFunctionSelection _WindowFunction = WindowFunctionSelection.hann;

        public int _EnvelopeSize = 512;
        [Range(0.1f, 1.0f)] public float _TukeyHeight = 0.5f;
        [Range(0.1f, 1.0f)] public float _GaussianSigma = 0.5f;
        [Range(0.0f, 0.5f)] public float _LinearInOutFade = 0.1f;

        private float[] _WindowArray;
        public float[] WindowArray { get { return _WindowArray; } }

        public Envelope(WindowFunctionSelection windowFunction, int envelopeSize, float tukeyHeight, float gaussianSigma, float linearInOutFade)
        {
            _WindowFunction = windowFunction;
            _EnvelopeSize = envelopeSize;
            _TukeyHeight = tukeyHeight;
            _GaussianSigma = gaussianSigma;
            _LinearInOutFade = linearInOutFade;
        }

        public float[] BuildWindowArray()
        {
            _WindowArray = new float[_EnvelopeSize];
            for (int i = 1; i < _WindowArray.Length; i++)
                _WindowArray[i] = AmplitudeAtIndex(i);
            return _WindowArray;            
        }

        public float AmplitudeAtIndex(int index)
        {
            return ApplyFunction(index) * Mathx.FadeInOut((float)index / (_EnvelopeSize - 1), _LinearInOutFade);
        }

        private float ApplyFunction(int index)
        {
            //index = Math.Clamp(index, 0, _EnvelopeSize);
            float amplitude = 1;

            switch (_WindowFunction)
            {
                case WindowFunctionSelection.sine:
                    amplitude = Mathf.Sin(Mathf.PI * index / _EnvelopeSize);
                    break;
                case WindowFunctionSelection.hann:
                    amplitude = 0.5f * (1 - Mathf.Cos(2 * Mathf.PI * index / _EnvelopeSize));
                    break;
                case WindowFunctionSelection.hamming:
                    amplitude = 0.54f - 0.46f * Mathf.Cos(2 * Mathf.PI * index / _EnvelopeSize);
                    break;
                case WindowFunctionSelection.tukey:
                    amplitude = 1 / (2 * _TukeyHeight) * (1 - Mathf.Cos(2 * Mathf.PI * index / _EnvelopeSize));
                    break;
                case WindowFunctionSelection.gaussian:
                    amplitude = Mathf.Pow(Mathf.Exp(1), -0.5f * Mathf.Pow((index - (float)_EnvelopeSize / 2) / (_GaussianSigma * _EnvelopeSize / 2), 2));
                    break;
            }
            return Mathf.Clamp01(amplitude);
        }
    }

    public enum TimeUnit { seconds = 0, samples = 1 }

    public class TimerTrigger
    {
        private TimeUnit _Unit = TimeUnit.seconds;

        private bool _CarryRemainder = true;
        private int _LastTriggerCount = 0;
        public int LastTriggerCount { get { return _LastTriggerCount; } }

        private float _TimePeriod = 1;
        private float _TimeCounter = 0;
        public float TimeCounter { get { return _TimeCounter; } }

        private int _SamplePeriod = AudioSettings.outputSampleRate;
        private int _SampleCounter = 0;
        public float SampleCounter { get { return _SampleCounter; } }

        public TimerTrigger(TimeUnit unit, float timePeriod)
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

        public TimerTrigger(TimeUnit unit, int samplePeriod)
        {
            if (unit != TimeUnit.samples)
                return;

            _Unit = unit;
            _SampleCounter = 0;
            ChangePeriod(samplePeriod);
        }

        public bool DrainTrigger()
        {
            if (_LastTriggerCount > 0)
            {
                _LastTriggerCount--;
                return true;
            }
            else
                return false;
        }

        public int UpdateTrigger(float delta, float? period)
        {
            if (_Unit != TimeUnit.seconds)
                return 0;

            if (period.HasValue)
                ChangePeriod(period.Value);

            _TimeCounter += delta;

            if (_TimeCounter < _TimePeriod)
                return 0;

            int count = (int)(_TimeCounter / _TimePeriod);
            _TimeCounter = _CarryRemainder ? _TimeCounter - _TimePeriod * count : 0;
            _LastTriggerCount = count;
            return count;
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
                _LastTriggerCount = count;
                return count;
            }
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