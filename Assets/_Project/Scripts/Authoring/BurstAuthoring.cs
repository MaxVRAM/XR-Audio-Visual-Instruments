using Unity.Entities;
using UnityEngine;

[System.Serializable]
public class BurstParameters
{
    public BurstPlayhead _Playhead;
    public BurstDuration _BurstDuration;
    public BurstDensity _Density;
    public BurstGrainDuration _GrainDuration;
    public BurstTranspose _Transpose;
    public BurstVolume _Volume;
}

public class BurstAuthoring : EmitterAuthoring
{
    public BurstParameters _Properties;

    public override void Initialise()
    {
        _IsPlaying = false;
        _EmitterType = EmitterType.Burst;
    }

    public override void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        _EmitterEntity = entity;
        int index = GrainSynth.Instance.RegisterEmitter(this);

        #region ADD EMITTER COMPONENT DATA

        dstManager.AddComponentData(_EmitterEntity, new BurstComponent
        {
            _IsPlaying = false,
            _EmitterIndex = index,
            _AudioClipIndex = _ClipIndex,
            _SpeakerIndex = _Host._SpeakerIndex,
            _HostIndex = _Host.EntityIndex,
            _AmplitudeOffsetFactor = 1,
            _PingPong = _PingPongGrainPlayheads,
            _OutputSampleRate = AudioSettings.outputSampleRate,

            _BurstDuration = new ModulationComponent
            {
                _StartValue = _Properties._BurstDuration._Default * _SamplesPerMS,
                _InteractionAmount = _Properties._BurstDuration._InteractionAmount * _SamplesPerMS,
                _Shape = _Properties._BurstDuration._InteractionShape,
                _Noise = _Properties._BurstDuration._Noise._Amount,
                _LockNoise = _Properties._BurstDuration._Noise._FreezeOnTrigger,
                _Min = _Properties._BurstDuration._Min * _SamplesPerMS,
                _Max = _Properties._BurstDuration._Max * _SamplesPerMS,
                _LockStartValue = false,
                _LockEndValue = false
            },
            _Playhead = new ModulationComponent
            {
                _StartValue = _Properties._Playhead._Start,
                _EndValue = _Properties._Playhead._End,                
                _InteractionAmount = _Properties._Playhead._InteractionAmount,
                _Shape = _Properties._Playhead._InteractionShape,
                _Noise = _Properties._Playhead._Noise._Amount,
                _LockNoise = _Properties._Playhead._Noise._FreezeOnTrigger,
                _Min = _Properties._Playhead._Min,
                _Max = _Properties._Playhead._Max,
                _LockStartValue = _Properties._Playhead._LockStartValue,
                _LockEndValue = false
            },
            _Density = new ModulationComponent
            {
                _StartValue = _Properties._Density._Start,
                _EndValue = _Properties._Density._End,
                _InteractionAmount = _Properties._Density._InteractionAmount,
                _Shape = _Properties._Density._InteractionShape,
                _Noise = _Properties._Density._Noise._Amount,
                _LockNoise = _Properties._Density._Noise._FreezeOnTrigger,
                _Min = _Properties._Density._Min,
                _Max = _Properties._Density._Max,
                _LockStartValue = false,
                _LockEndValue = false
            },
            _GrainDuration = new ModulationComponent
            {
                _StartValue = _Properties._GrainDuration._Start * _SamplesPerMS,
                _EndValue = _Properties._GrainDuration._End * _SamplesPerMS,                
                _InteractionAmount = _Properties._GrainDuration._InteractionAmount * _SamplesPerMS,
                _Shape = _Properties._GrainDuration._InteractionShape,
                _Noise = _Properties._GrainDuration._Noise._Amount,
                _LockNoise = _Properties._GrainDuration._Noise._FreezeOnTrigger,
                _Min = _Properties._GrainDuration._Min * _SamplesPerMS,
                _Max = _Properties._GrainDuration._Max * _SamplesPerMS,
                _LockStartValue = false,
                _LockEndValue = false
            },
            _Transpose = new ModulationComponent
            {
                _StartValue = _Properties._Transpose._Start,
                _EndValue = _Properties._Transpose._End,
                _InteractionAmount = _Properties._Transpose._InteractionAmount,
                _Shape = _Properties._Transpose._InteractionShape,
                _Noise = _Properties._Transpose._Noise._Amount,
                _LockNoise = _Properties._Transpose._Noise._FreezeOnTrigger,
                _Min = _Properties._Transpose._Min,
                _Max = _Properties._Transpose._Max,
                _LockStartValue = false,
                _LockEndValue = _Properties._Transpose._LockEndValue
            },
            _Volume = new ModulationComponent
            {
                _StartValue = _Properties._Volume._Start,
                _EndValue = _Properties._Volume._End,
                _Shape = _Properties._Volume._InteractionShape,
                _InteractionAmount = _Properties._Volume._InteractionAmount,
                _Noise = _Properties._Volume._Noise._Amount,
                _LockNoise = _Properties._Volume._Noise._FreezeOnTrigger,
                _Min = _Properties._Volume._Min,
                _Max = _Properties._Volume._Max,
                _LockStartValue = false,
                _LockEndValue = _Properties._Volume._LockEndValue
            }
        });

        #if UNITY_EDITOR
                dstManager.SetName(entity, "Emitter " + index + " (Burst): " + name + "     Parent: " + transform.parent.name);
        #endif

        #endregion

        dstManager.AddBuffer<DSPParametersElement>(_EmitterEntity);
        DynamicBuffer<DSPParametersElement> dspParams = dstManager.GetBuffer<DSPParametersElement>(_EmitterEntity);
        for (int i = 0; i < _DSPChainParams.Length; i++)
        {
            dspParams.Add(_DSPChainParams[i].GetDSPBufferElement());
        }
        dstManager.AddComponentData(entity, new QuadEntityType { _Type = QuadEntityType.QuadEntityTypeEnum.Emitter });

        _Initialised = true;
    }

    public override void UpdateEmitterComponents()
    {
        if (_IsPlaying && _Initialised)
        {
            BurstComponent burstData = _EntityManager.GetComponentData<BurstComponent>(_EmitterEntity);

            #region UPDATE EMITTER COMPONENT DATA
            burstData._IsPlaying = true;
            burstData._AudioClipIndex = _ClipIndex;
            burstData._SpeakerIndex = _Host._SpeakerIndex;
            burstData._HostIndex = _Host.EntityIndex;
            burstData._PingPong = _PingPongGrainPlayheads;
            burstData._AmplitudeOffsetFactor = _AmplitudeOffsetFactor;
            burstData._OutputSampleRate = AudioSettings.outputSampleRate;
                
            burstData._BurstDuration = new ModulationComponent
            {
                _StartValue = _Properties._BurstDuration._Default * _SamplesPerMS,
                _InteractionAmount = _Properties._BurstDuration._InteractionAmount * _SamplesPerMS,
                _Shape = _Properties._BurstDuration._InteractionShape,
                _Noise = _Properties._BurstDuration._Noise._Amount,
                _LockNoise = _Properties._BurstDuration._Noise._FreezeOnTrigger,
                _Min = _Properties._BurstDuration._Min * _SamplesPerMS,
                _Max = _Properties._BurstDuration._Max * _SamplesPerMS,
                _LockStartValue = false,
                _LockEndValue = false,
                _InteractionInput = _Properties._BurstDuration.GetValue()
            };
            burstData._Playhead = new ModulationComponent
            {
                _StartValue = _Properties._Playhead._Start,
                _EndValue = _Properties._Playhead._End,
                _InteractionAmount = _Properties._Playhead._InteractionAmount,
                _Shape = _Properties._Playhead._InteractionShape,
                _Noise = _Properties._Playhead._Noise._Amount,
                _LockNoise = _Properties._Playhead._Noise._FreezeOnTrigger,
                _Min = _Properties._Playhead._Min,
                _Max = _Properties._Playhead._Max,
                _LockStartValue = _Properties._Playhead._LockStartValue,
                _LockEndValue = false,
                _InteractionInput = _Properties._Playhead.GetValue()
            };
            burstData._Density = new ModulationComponent
            {
                _StartValue = _Properties._Density._Start,
                _EndValue = _Properties._Density._End,
                _InteractionAmount = _Properties._Density._InteractionAmount,
                _Shape = _Properties._Density._InteractionShape,
                _Noise = _Properties._Density._Noise._Amount,
                _LockNoise = _Properties._Density._Noise._FreezeOnTrigger,
                _Min = _Properties._Density._Min,
                _Max = _Properties._Density._Max,
                _LockStartValue = false,
                _LockEndValue = false,
                _InteractionInput = _Properties._Density.GetValue()
            };
            burstData._GrainDuration = new ModulationComponent
            {
                _StartValue = _Properties._GrainDuration._Start * _SamplesPerMS,
                _EndValue = _Properties._GrainDuration._End * _SamplesPerMS,
                _InteractionAmount = _Properties._GrainDuration._InteractionAmount * _SamplesPerMS,
                _Shape = _Properties._GrainDuration._InteractionShape,
                _Noise = _Properties._GrainDuration._Noise._Amount,
                _LockNoise = _Properties._GrainDuration._Noise._FreezeOnTrigger,
                _Min = _Properties._GrainDuration._Min * _SamplesPerMS,
                _Max = _Properties._GrainDuration._Max * _SamplesPerMS,
                _LockStartValue = false,
                _LockEndValue = false,
                _InteractionInput = _Properties._GrainDuration.GetValue()
            };
            burstData._Transpose = new ModulationComponent
            {
                _StartValue = _Properties._Transpose._Start,
                _EndValue = _Properties._Transpose._End,
                _InteractionAmount = _Properties._Transpose._InteractionAmount,
                _Shape = _Properties._Transpose._InteractionShape,
                _Noise = _Properties._Transpose._Noise._Amount,
                _LockNoise = _Properties._Transpose._Noise._FreezeOnTrigger,
                _Min = _Properties._Transpose._Min,
                _Max = _Properties._Transpose._Max,
                _LockStartValue = false,
                _LockEndValue = _Properties._Transpose._LockEndValue,
                _InteractionInput = _Properties._Transpose.GetValue()
            };
            burstData._Volume = new ModulationComponent
            {
                _StartValue = _Properties._Volume._Start,
                _EndValue = _Properties._Volume._End,
                _InteractionAmount = _Properties._Volume._InteractionAmount,
                _Shape = _Properties._Volume._InteractionShape,
                _Noise = _Properties._Volume._Noise._Amount,
                _LockNoise = _Properties._Volume._Noise._FreezeOnTrigger,
                _Min = _Properties._Volume._Min,
                _Max = _Properties._Volume._Max,
                _LockStartValue = false,
                _LockEndValue = _Properties._Volume._LockEndValue,
                _InteractionInput = _Properties._Volume.GetValue() * _ContactSurfaceAttenuation
            };
            _EntityManager.SetComponentData(_EmitterEntity, burstData);

            #endregion
            
            UpdateDSPEffectsBuffer();

            // Burst emitters only need a single pass to generate grain data for its duration.
            _IsPlaying = false;
        }
    }
}
