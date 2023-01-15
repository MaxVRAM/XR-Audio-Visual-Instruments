﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class ContinuousNoiseInput
{
    [Range(0f, 1.0f)]
    public float _Amount = 0f;
    public bool _Perlin = false;
}

[System.Serializable]
public class BurstNoiseInput
{
    [Range(0f, 1.0f)]
    public float _Amount = 0f;
    public bool _FreezeOnTrigger = false;
}

[System.Serializable]
public class EmitterProperty
{
    public ModulationSource _InputSource;
    public float _InputValue = 0f;

    public float GetValue()
    {
        if (_InputSource != null)
            _InputValue = _InputSource.GetValue();
        return _InputValue;
    }
}

[System.Serializable]
public class ContinuousDensity : EmitterProperty
{
    [Range(0.1f, 10f)]
    public float _Idle = 2f;
    // public InteractionInput _Interaction;
    [Range(-9.9f, 9.9f)]
    public float _InteractionAmount = 0f;
    [Range(0.5f, 5.0f)]
    public float _InteractionShape = 1f;
    public ContinuousNoiseInput _Noise;

    [HideInInspector]
    public float _Min = 0.1f;
    [HideInInspector]
    public float _Max = 10f;
}

[System.Serializable]
public class ContinuousDuration : EmitterProperty
{
    [Range(2f, 500f)]
    public float _Idle = 50f;
    // public InteractionInput _Interaction;
    [Range(-502f, 502f)]
    public float _InteractionAmount = 0f;
    [Range(0.5f, 5.0f)]
    public float _InteractionShape = 1f;
    public ContinuousNoiseInput _Noise;

    [HideInInspector]
    public float _Min = 2f;
    [HideInInspector]
    public float _Max = 500f;
}

[System.Serializable]
public class ContinuousPlayhead : EmitterProperty
{
    [Range(0f, 1f)]
    public float _Idle = 0f;
    // public InteractionInput _Interaction;
    [Range(-1f, 1f)]
    public float _InteractionAmount = 0f;
    [Range(0.5f, 5.0f)]
    public float _InteractionShape = 1f;
    public ContinuousNoiseInput _Noise;

    [HideInInspector]
    public float _Min = 0f;
    [HideInInspector]
    public float _Max = 1f;
}

[System.Serializable]
public class ContinuousTranspose : EmitterProperty
{
    [Range(-3f, 3f)]
    public float _Idle = 1;
    // public InteractionInput _Interaction;
    [Range(-6f, 6f)]
    public float _InteractionAmount = 0f;
    [Range(0.5f, 5.0f)]
    public float _InteractionShape = 1f;
    public ContinuousNoiseInput _Noise;

    [HideInInspector]
    public float _Min = -3f;
    [HideInInspector]
    public float _Max = 3f;
}

[System.Serializable]
public class ContinuousVolume : EmitterProperty
{
    [Range(0f, 2f)]
    public float _Idle = 1f;
    // public InteractionInput _Interaction;
    [Range(-2f, 2f)]
    public float _InteractionAmount = 0f;
    [Range(0.5f, 5.0f)]
    public float _InteractionShape = 1f;
    public ContinuousNoiseInput _Noise;

    [HideInInspector]
    public float _Min = 0f;
    [HideInInspector]
    public float _Max = 2f;
}



[System.Serializable]
public class BurstDensity : EmitterProperty
{
    [Range(0.1f, 10f)]
    public float _Start = 2f;
    [Range(0.1f, 10f)]
    public float _End = 2f;
    // public InteractionInput _Interaction;
    [Range(-9.9f, 9.9f)]
    public float _InteractionAmount = 0f;
    [Range(0.5f, 5.0f)]
    public float _InteractionShape = 1f;
    public BurstNoiseInput _Noise;

    [HideInInspector]
    public float _Min = 0.1f;
    [HideInInspector]
    public float _Max = 10f;
}

[System.Serializable]
public class BurstDuration : EmitterProperty
{
    [Range(10f, 1000f)]
    public float _Default = 100f;
    // public InteractionInput _Interaction;
    [Range(-990f, 990f)]
    public float _InteractionAmount = 0f;
    [Range(0.5f, 5.0f)]
    public float _InteractionShape = 1f;
    public BurstNoiseInput _Noise;

    [HideInInspector]
    public float _Min = 10f;
    [HideInInspector]
    public float _Max = 1000f;
}

[System.Serializable]
public class BurstGrainDuration : EmitterProperty
{
    [Range(5f, 500f)]
    public float _Start = 20f;
    [Range(5f, 500f)]
    public float _End = 20f;
    // public InteractionInput _Interaction;
    [Range(-495f, 495f)]
    public float _InteractionAmount = 0f;
    [Range(0.5f, 5.0f)]
    public float _InteractionShape = 1f;
    public BurstNoiseInput _Noise;

    [HideInInspector]
    public float _Min = 5f;
    [HideInInspector]
    public float _Max = 500f;
}

[System.Serializable]
public class BurstPlayhead : EmitterProperty
{
    [Range(0f, 1f)]
    public float _Start = 0f;
    public bool _LockStartValue = true;
    [Range(0f, 1f)]
    public float _End = 1f;
    // public InteractionInput _Interaction;
    [Range(-1f, 1f)]
    public float _InteractionAmount = 0f;
    [Range(0.5f, 5.0f)]
    public float _InteractionShape = 1f;
    public BurstNoiseInput _Noise;

    [HideInInspector]
    public float _Min = 0f;
    [HideInInspector]
    public float _Max = 1f;
}

[System.Serializable]
public class BurstTranspose : EmitterProperty
{
    [Range(-3f, 3f)]
    public float _Start = 0f;
    [Range(-3f, 3f)]
    public float _End = 0f;
    public bool _LockEndValue = true;
    // public InteractionInput _Interaction;
    [Range(-3f, 3f)]
    public float _InteractionAmount = 0f;
    [Range(0.5f, 5.0f)]
    public float _InteractionShape = 1f;
    public BurstNoiseInput _Noise;

    [HideInInspector]
    public float _Min = -3f;
    [HideInInspector]
    public float _Max = 3f;
}

[System.Serializable]
public class BurstVolume : EmitterProperty
{
    [Range(0f, 1f)]
    public float _Start = 0f;
    [Range(0f, 1f)]
    public float _End = 0f;
    public bool _LockEndValue = true;
    // public InteractionInput _Interaction;
    [Range(-2f, 2f)]
    public float _InteractionAmount = 1f;
    [Range(0.5f, 5.0f)]
    public float _InteractionShape = 1f;
    public BurstNoiseInput _Noise;

    [HideInInspector]
    public float _Min = 0f;
    [HideInInspector]
    public float _Max = 2f;
}