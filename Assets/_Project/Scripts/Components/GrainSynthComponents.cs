﻿using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

#region ---------- COMPONENTS

public struct DSPTimerComponent : IComponentData
{
    public int _CurrentSampleIndex;
    public int _GrainQueueDuration;
}

public struct AudioClipDataComponent :IComponentData
{
    public int _ClipIndex;
    public BlobAssetReference<FloatBlobAsset> _ClipDataBlobAsset;
}

public struct WindowingDataComponent : IComponentData
{
    public BlobAssetReference<FloatBlobAsset> _WindowingArray;   
}

public struct GrainProcessorComponent : IComponentData
{
    public AudioClipDataComponent _AudioClipDataComponent;
    public int _StartTimeDSP;
    public int _SampleCount;
    public float _PlayheadNorm;
    public float _Pitch;
    public float _Volume;
    public int _SpeakerIndex;
    public bool _SamplePopulated;
    public int _EffectTailSampleLength;
}

public struct ActivationRadiusComponent : IComponentData
{
    public float3 _ListenerPos;
    public float _ListenerRadius;
    public float _AttachmentRadius;
}

public struct SpeakerComponent : IComponentData
{
    public int _SpeakerIndex;
}

public enum PooledState
{
    Pooled,
    Active
}
public struct PoolingComponent : IComponentData
{
    public PooledState _State;
    public int _AttachedHostCount;
}

public struct PlayingTag : IComponentData {}
public struct ConnectedTag : IComponentData {}
public struct PingPongTag : IComponentData {}
public struct DedicatedSpeakerTag : IComponentData {}
public struct InListenerRadiusTag : IComponentData {}

public struct EmitterHostComponent : IComponentData
{
    public int _HostIndex;
    public bool _InListenerRadius;
    public bool _HasDedicatedSpeaker;
    public bool _Connected;
    public int _SpeakerIndex;
}

public struct ModulationComponent : IComponentData
{
    public float _StartValue;
    public float _EndValue;
    public float _Noise;
    public bool _PerlinNoise;
    public bool _LockNoise;
    public float _PerlinValue;
    public float _Shape;
    public float _InteractionAmount;
    public float _Min;
    public float _Max;
    public bool _LockStartValue;
    public bool _LockEndValue;
    public float _InteractionInput;
}

public struct ContinuousComponent : IComponentData
{
    public int _HostIndex;
    public int _EmitterIndex;
    public int _SpeakerIndex;
    public int _AudioClipIndex;
    public bool _PingPong;
    public bool _IsPlaying;
    public int _OutputSampleRate;
    public float _AmplitudeOffsetFactor;
    public int _LastSampleIndex;
    public int _PreviousGrainDuration;
    public ModulationComponent _Playhead;
    public ModulationComponent _Density;
    public ModulationComponent _Duration;
    public ModulationComponent _Transpose;
    public ModulationComponent _Volume;
}

public struct BurstComponent : IComponentData
{
    public int _HostIndex;
    public int _EmitterIndex;
    public int _SpeakerIndex;
    public int _AudioClipIndex;
    public bool _PingPong;
    public bool _IsPlaying;
    public int _OutputSampleRate;
    public float _AmplitudeOffsetFactor;
    public ModulationComponent _BurstDuration;
    public ModulationComponent _Density;
    public ModulationComponent _Playhead;
    public ModulationComponent _GrainDuration;
    public ModulationComponent _Transpose;
    public ModulationComponent _Volume;
}

#endregion

#region ---------- BUFFER ELEMENTS
// Capacity set to a 1 second length by default
//[InternalBufferCapacity(44100)]
public struct GrainSampleBufferElement : IBufferElementData
{
    public float Value;
}

public struct DSPSampleBufferElement : IBufferElementData
{
    public float Value;
}

[System.Serializable]
public struct DSPParametersElement : IBufferElementData
{
    public DSPTypes _DSPType;
    public bool _DelayBasedEffect;
    public int _SampleRate;
    public int _SampleTail;
    public int _SampleStartTime;
    public float _Mix;
    public float _Value0;
    public float _Value1;
    public float _Value2;
    public float _Value3;
    public float _Value4;
    public float _Value5;
    public float _Value6;
    public float _Value7;
    public float _Value8;
    public float _Value9;
    public float _Value10;
}

public enum DSPTypes
{
    Bitcrush,
    Flange,
    Delay,
    Filter,
    Chopper
}

#endregion

#region ---------- BLOB ASSETS

public struct FloatBlobAsset
{
    public BlobArray<float> array;
}

#endregion


