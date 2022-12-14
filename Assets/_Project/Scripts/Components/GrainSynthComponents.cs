using Unity.Entities;
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
    public int _StartSampleIndex;
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
    public float _EmitterToListenerRadius;
    public float _SpeakerAttachRadius;
}

public struct PlayingTag : IComponentData {}
public struct PingPongTag : IComponentData {}

public struct SpeakerComponent : IComponentData
{
    public int _SpeakerIndex;
}

public struct PoolingComponent : IComponentData
{
    public PooledState _State;
}

public enum PooledState
{
    Pooled,
    Active
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

public struct FixedSpeakerLinkTag : IComponentData {}
public struct DedicatedSpeakerTag : IComponentData {}
public struct InListenerRadiusTag : IComponentData {}

public struct EmitterHostComponent : IComponentData
{
    public int _HostIndex;
    public bool _InListenerRadius;
    public float _DistanceAttenuation;
    public bool _DedicatedSpeaker;
    public bool _SpeakerAttached;
    public int _SpeakerIndex;
    public bool _NewSpeaker;
}

public struct ContinuousEmitterComponent : IComponentData
{
    public int _EmitterIndex;
    public int _AudioClipIndex;
    public bool _IsPlaying;
    public bool _PingPong;
    public bool _InListenerRadius;
    public float _DistanceAmplitude;
    public bool _FixedSpeakerLink;
    public bool _SpeakerAttached;
    public int _SpeakerIndex;
    public int _LastSampleIndex;
    public int _PreviousGrainDuration;
    public int _OutputSampleRate;
    public ModulationComponent _Playhead;
    public ModulationComponent _Density;
    public ModulationComponent _Duration;
    public ModulationComponent _Transpose;
    public ModulationComponent _Volume;
}

public struct BurstEmitterComponent : IComponentData
{
    public int _EmitterIndex;
    public int _AudioClipIndex;
    public bool _IsPlaying;
    public bool _PingPong;
    public bool _InListenerRadius;
    public float _DistanceAmplitude;
    public bool _FixedSpeakerLink;
    public bool _SpeakerAttached;
    public int _SpeakerIndex;
    public int _OutputSampleRate;
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


