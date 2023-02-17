using UnityEngine;
using Serializable = System.SerializableAttribute;

using NaughtyAttributes;

// TODO: Replace all these various with single struct/object.
// Use normalised values for consistency, and display scaled values in editor via custom drawers

namespace PlaneWaver
{
    // TODO: It would super sweet if the amount of noise could be modulated too.
    // If I went down that path, it would be worth investing in a totally modular modulation system
    // using lists of modulators applying whatever operators between them.

    [Serializable]
    public class ContinuousNoiseInput
    {
        public float _Amount = 0f;
        public float _Speed = 1f;
        public bool _Perlin = false;
    }

    [Serializable]
    public class BurstNoiseInput
    {
        public float _Amount = 0f;
        public bool _HoldForBurstDuration = false;
    }

    [Serializable]
    public class EmitterProperty
    {
        public ModulationInput _ModulationSource;

        public void SetModulationInput(ModulationInput modulationSource)
        {
            _ModulationSource = modulationSource;
        }

        public void SetModulationActors(Actor localActor, Actor remoteActor)
        {
            _ModulationSource.SetActors(localActor, remoteActor);
        }

        public float GetValue()
        {
            return _ModulationSource.Result;
        }
    }

    [Serializable]
    public class ContinuousDensity : EmitterProperty
    {
        [Range(-9.9f, 9.9f)]
        public float _ModulationAmount = 0f;
        [Range(0.5f, 5.0f)]
        public float _ModulationExponent = 1f;
        [Range(0.1f, 10f)]
        public float _Idle = 2f;
        public ContinuousNoiseInput _Noise;

        [HideInInspector] public float _Min = 0.1f;
        [HideInInspector] public float _Max = 10f;
    }

    [Serializable]
    public class ContinuousDuration : EmitterProperty
    {
        [Range(-502f, 502f)]
        public float _ModulationAmount = 0f;
        [Range(0.5f, 5.0f)]
        public float _ModulationExponent = 1f;
        [Range(2f, 500f)]
        public float _Idle = 50f;
        public ContinuousNoiseInput _Noise;

        [HideInInspector] public float _Min = 2f;
        [HideInInspector] public float _Max = 500f;
    }

    [Serializable]
    public class ContinuousPlayhead : EmitterProperty
    {
        [Range(-1f, 1f)]
        public float _ModulationAmount = 0f;
        [Range(0.5f, 5.0f)]
        public float _ModulationExponent = 1f;
        [Range(0f, 1f)]
        public float _Idle = 0f;
        public ContinuousNoiseInput _Noise;

        [HideInInspector] public float _Min = 0f;
        [HideInInspector] public float _Max = 1f;
    }

    [Serializable]
    public class ContinuousTranspose : EmitterProperty
    {
        [Range(-6f, 6f)]
        public float _ModulationAmount = 0f;
        [Range(0.5f, 5.0f)]
        public float _ModulationExponent = 1f;
        [Range(-3f, 3f)]
        public float _Idle = 1;
        public ContinuousNoiseInput _Noise;

        [HideInInspector] public float _Min = -3f;
        [HideInInspector] public float _Max = 3f;
    }

    [Serializable]
    public class ContinuousVolume : EmitterProperty
    {
        [Range(-2f, 2f)]
        public float _ModulationAmount = 0f;
        [Range(0.5f, 5.0f)]
        public float _ModulationExponent = 1f;
        [Range(0f, 2f)]
        public float _Idle = 1f;
        public ContinuousNoiseInput _Noise;

        [HideInInspector] public float _Min = 0f;
        [HideInInspector] public float _Max = 2f;
    }

    [Serializable]
    public class BurstDensity : EmitterProperty
    {
        [Range(-9.9f, 9.9f)]
        public float _ModulationAmount = 0f;
        [Range(0.5f, 5.0f)]
        public float _ModulationExponent = 1f;
        [Range(0.1f, 10f)]
        public float _Start = 2f;
        [Range(0.1f, 10f)]
        public float _End = 2f;
        public BurstNoiseInput _Noise;

        [HideInInspector] public float _Min = 0.1f;
        [HideInInspector] public float _Max = 10f;
    }

    [Serializable]
    public class BurstDuration : EmitterProperty
    {
        [Range(-990f, 990f)]
        public float _ModulationAmount = 0f;
        [Range(0.5f, 5.0f)]
        public float _ModulationExponent = 1f;
        [Range(10f, 1000f)]
        public float _Default = 100f;
        public BurstNoiseInput _Noise;

        [HideInInspector] public float _Min = 10f;
        [HideInInspector] public float _Max = 1000f;
    }

    [Serializable]
    public class BurstGrainDuration : EmitterProperty
    {
        [Range(-495f, 495f)]
        public float _ModulationAmount = 0f;
        [Range(0.5f, 5.0f)]
        public float _ModulationExponent = 1f;
        [Range(5f, 500f)]
        public float _Start = 20f;
        [Range(5f, 500f)]
        public float _End = 20f;
        public BurstNoiseInput _Noise;

        [HideInInspector] public float _Min = 5f;
        [HideInInspector] public float _Max = 500f;
    }

    [Serializable]
    public class BurstPlayhead : EmitterProperty
    {
        [Range(-1f, 1f)]
        public float _ModulationAmount = 0f;
        [Range(0.5f, 5.0f)]
        public float _ModulationExponent = 1f;
        [Range(0f, 1f)]
        public float _Start = 0f;
        [Range(0f, 1f)]
        public float _End = 1f;
        public bool _StartIgnoresModulation = true;
        public BurstNoiseInput _Noise;

        [HideInInspector] public float _Min = 0f;
        [HideInInspector] public float _Max = 1f;
    }

    [Serializable]
    public class BurstTranspose : EmitterProperty
    {
        [Range(-3f, 3f)]
        public float _ModulationAmount = 0f;
        [Range(0.5f, 5.0f)]
        public float _ModulationExponent = 1f;
        [Range(-3f, 3f)]
        public float _Start = 0f;
        [Range(-3f, 3f)]
        public float _End = 0f;
        public bool _EndIgnoresModulation = true;
        public BurstNoiseInput _Noise;

        [HideInInspector] public float _Min = -3f;
        [HideInInspector] public float _Max = 3f;
    }

    [Serializable]
    public class BurstVolume : EmitterProperty
    {
        [Range(-2f, 2f)]
        public float _ModulationAmount = 1f;
        [Range(0.5f, 5.0f)]
        public float _ModulationExponent = 1f;
        [Range(0f, 1f)]
        public float _Start = 0f;
        [Range(0f, 1f)]
        public float _End = 0f;
        public bool _EndIgnoresModulation = true;
        public BurstNoiseInput _Noise;

        [HideInInspector] public float _Min = 0f;
        [HideInInspector] public float _Max = 2f;
    }
}