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

public class BurstEmitterAuthoring : BaseEmitterClass
{
    public BurstParameters _Parameters;

    public override void InitialiseTypeAndInteractions()
    {
        _EmitterType = EmitterType.Burst;
        _Parameters._Playhead._Interaction.UpdateSource(_PrimaryObject, _SecondaryObject, _LatestCollision);
        _Parameters._BurstDuration._Interaction.UpdateSource(_PrimaryObject, _SecondaryObject, _LatestCollision);
        _Parameters._Density._Interaction.UpdateSource(_PrimaryObject, _SecondaryObject, _LatestCollision);
        _Parameters._GrainDuration._Interaction.UpdateSource(_PrimaryObject, _SecondaryObject, _LatestCollision);
        _Parameters._Transpose._Interaction.UpdateSource(_PrimaryObject, _SecondaryObject, _LatestCollision);
        _Parameters._Volume._Interaction.UpdateSource(_PrimaryObject, _SecondaryObject, _LatestCollision);
    }

    public override void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        _EmitterEntity = entity;
        int index = GrainSynth.Instance.RegisterEmitter(entity);

        if (_LinkedSpeaker != null || gameObject.TryGetComponent(out _LinkedSpeaker))
        {
            _FixedSpeakerLink = true;
            _LinkedSpeaker.AddEmitterLink(gameObject);
            _LinkedSpeakerIndex = _LinkedSpeaker.GetRegisterAndGetIndex();
            dstManager.AddComponentData(_EmitterEntity, new FixedSpeakerLinkTag { });
        }
        else _LinkedSpeakerIndex = int.MaxValue;

        #region ADD EMITTER COMPONENT DATA
        dstManager.AddComponentData(_EmitterEntity, new BurstEmitterComponent
        {
            _IsPlaying = false,
            _EmitterIndex = index,
            _DistanceAmplitude = 1,
            _AudioClipIndex = _ClipIndex,
            _PingPong = _PingPongAtEndOfClip,
            _SpeakerIndex = _LinkedSpeakerIndex,
            _FixedSpeakerLink = _FixedSpeakerLink,
            _SpeakerAttached = _FixedSpeakerLink,
            _OutputSampleRate = AudioSettings.outputSampleRate,

            _BurstDuration = new ModulationComponent
            {
                _StartValue = _Parameters._BurstDuration._Default * _SamplesPerMS,
                _InteractionAmount = _Parameters._BurstDuration._InteractionAmount * _SamplesPerMS,
                _Shape = _Parameters._BurstDuration._InteractionShape,
                _Noise = _Parameters._BurstDuration._Noise._Amount,
                _LockNoise = _Parameters._BurstDuration._Noise._FreezeOnTrigger,
                _Min = _Parameters._BurstDuration._Min * _SamplesPerMS,
                _Max = _Parameters._BurstDuration._Max * _SamplesPerMS,
                _LockStartValue = false,
                _LockEndValue = false
            },
            _Playhead = new ModulationComponent
            {
                _StartValue = _Parameters._Playhead._Start,
                _EndValue = _Parameters._Playhead._End,                
                _InteractionAmount = _Parameters._Playhead._InteractionAmount,
                _Shape = _Parameters._Playhead._InteractionShape,
                _Noise = _Parameters._Playhead._Noise._Amount,
                _LockNoise = _Parameters._Playhead._Noise._FreezeOnTrigger,
                _Min = _Parameters._Playhead._Min,
                _Max = _Parameters._Playhead._Max,
                _LockStartValue = _Parameters._Playhead._LockStartValue,
                _LockEndValue = false
            },
            _Density = new ModulationComponent
            {
                _StartValue = _Parameters._Density._Start,
                _EndValue = _Parameters._Density._End,
                _InteractionAmount = _Parameters._Density._InteractionAmount,
                _Shape = _Parameters._Density._InteractionShape,
                _Noise = _Parameters._Density._Noise._Amount,
                _LockNoise = _Parameters._Density._Noise._FreezeOnTrigger,
                _Min = _Parameters._Density._Min,
                _Max = _Parameters._Density._Max,
                _LockStartValue = false,
                _LockEndValue = false
            },
            _GrainDuration = new ModulationComponent
            {
                _StartValue = _Parameters._GrainDuration._Start * _SamplesPerMS,
                _EndValue = _Parameters._GrainDuration._End * _SamplesPerMS,                
                _InteractionAmount = _Parameters._GrainDuration._InteractionAmount * _SamplesPerMS,
                _Shape = _Parameters._GrainDuration._InteractionShape,
                _Noise = _Parameters._GrainDuration._Noise._Amount,
                _LockNoise = _Parameters._GrainDuration._Noise._FreezeOnTrigger,
                _Min = _Parameters._GrainDuration._Min * _SamplesPerMS,
                _Max = _Parameters._GrainDuration._Max * _SamplesPerMS,
                _LockStartValue = false,
                _LockEndValue = false
            },
            _Transpose = new ModulationComponent
            {
                _StartValue = _Parameters._Transpose._Start,
                _EndValue = _Parameters._Transpose._End,
                _InteractionAmount = _Parameters._Transpose._InteractionAmount,
                _Shape = _Parameters._Transpose._InteractionShape,
                _Noise = _Parameters._Transpose._Noise._Amount,
                _LockNoise = _Parameters._Transpose._Noise._FreezeOnTrigger,
                _Min = _Parameters._Transpose._Min,
                _Max = _Parameters._Transpose._Max,
                _LockStartValue = false,
                _LockEndValue = _Parameters._Transpose._LockEndValue
            },
            _Volume = new ModulationComponent
            {
                _StartValue = _Parameters._Volume._Start,
                _EndValue = _Parameters._Volume._End,
                _Shape = _Parameters._Volume._InteractionShape,
                _InteractionAmount = _Parameters._Volume._InteractionAmount,
                _Noise = _Parameters._Volume._Noise._Amount,
                _LockNoise = _Parameters._Volume._Noise._FreezeOnTrigger,
                _Min = _Parameters._Volume._Min,
                _Max = _Parameters._Volume._Max,
                _LockStartValue = false,
                _LockEndValue = _Parameters._Volume._LockEndValue
            }
        });

        #if UNITY_EDITOR
                dstManager.SetName(entity, "Burst Emitter:   " + transform.parent.name + " " + gameObject.name);
        #endif

        #endregion

        #region ADD DSP EFFECT COMPONENT DATA
        dstManager.AddBuffer<DSPParametersElement>(_EmitterEntity);
        DynamicBuffer<DSPParametersElement> dspParams = dstManager.GetBuffer<DSPParametersElement>(_EmitterEntity);
        for (int i = 0; i < _DSPChainParams.Length; i++)
        {
            dspParams.Add(_DSPChainParams[i].GetDSPBufferElement());
        }
        dstManager.AddComponentData(entity, new QuadEntityType { _Type = QuadEntityType.QuadEntityTypeEnum.Emitter });
        #endregion

        _Initialised = true;
    }

    protected override void UpdateProperties()
    {
        if (_IsWithinEarshot && _IsPlaying)
        {
            BurstEmitterComponent burstData = _EntityManager.GetComponentData<BurstEmitterComponent>(_EmitterEntity);

            #region UPDATE EMITTER COMPONENT DATA
            burstData._IsPlaying = true;
            burstData._AudioClipIndex = _ClipIndex;
            burstData._PingPong = _PingPongAtEndOfClip;
            burstData._SpeakerIndex = _FixedSpeakerLink ? _LinkedSpeaker._SpeakerIndex : burstData._SpeakerIndex;
            _LinkedSpeakerIndex = burstData._SpeakerIndex;
            _LinkedToSpeaker = burstData._SpeakerAttached;
            _InListenerRadius = burstData._InListenerRadius;
            
            if (burstData._SpeakerIndex < GrainSynth.Instance._GrainSpeakers.Count)
                burstData._DistanceAmplitude = AudioUtils.EmitterFromSpeakerVolumeAdjust(_HeadPosition.position,
                    GrainSynth.Instance._GrainSpeakers[burstData._SpeakerIndex].gameObject.transform.position,
                    transform.position) * _DistanceVolume;
            else
                burstData._DistanceAmplitude = 0;
                
            burstData._BurstDuration = new ModulationComponent
            {
                _StartValue = _Parameters._BurstDuration._Default * _SamplesPerMS,
                _InteractionAmount = _Parameters._BurstDuration._InteractionAmount * _SamplesPerMS,
                _Shape = _Parameters._BurstDuration._InteractionShape,
                _Noise = _Parameters._BurstDuration._Noise._Amount,
                _LockNoise = _Parameters._BurstDuration._Noise._FreezeOnTrigger,
                _Min = _Parameters._BurstDuration._Min * _SamplesPerMS,
                _Max = _Parameters._BurstDuration._Max * _SamplesPerMS,
                _LockStartValue = false,
                _LockEndValue = false,
                _InteractionInput = _Parameters._BurstDuration._Interaction.GetValue()
            };
            burstData._Playhead = new ModulationComponent
            {
                _StartValue = _Parameters._Playhead._Start,
                _EndValue = _Parameters._Playhead._End,
                _InteractionAmount = _Parameters._Playhead._InteractionAmount,
                _Shape = _Parameters._Playhead._InteractionShape,
                _Noise = _Parameters._Playhead._Noise._Amount,
                _LockNoise = _Parameters._Playhead._Noise._FreezeOnTrigger,
                _Min = _Parameters._Playhead._Min,
                _Max = _Parameters._Playhead._Max,
                _LockStartValue = _Parameters._Playhead._LockStartValue,
                _LockEndValue = false,
                _InteractionInput = _Parameters._Playhead._Interaction.GetValue()
            };
            burstData._Density = new ModulationComponent
            {
                _StartValue = _Parameters._Density._Start,
                _EndValue = _Parameters._Density._End,
                _InteractionAmount = _Parameters._Density._InteractionAmount,
                _Shape = _Parameters._Density._InteractionShape,
                _Noise = _Parameters._Density._Noise._Amount,
                _LockNoise = _Parameters._Density._Noise._FreezeOnTrigger,
                _Min = _Parameters._Density._Min,
                _Max = _Parameters._Density._Max,
                _LockStartValue = false,
                _LockEndValue = false,
                _InteractionInput = _Parameters._Density._Interaction.GetValue()
            };
            burstData._GrainDuration = new ModulationComponent
            {
                _StartValue = _Parameters._GrainDuration._Start * _SamplesPerMS,
                _EndValue = _Parameters._GrainDuration._End * _SamplesPerMS,
                _InteractionAmount = _Parameters._GrainDuration._InteractionAmount * _SamplesPerMS,
                _Shape = _Parameters._GrainDuration._InteractionShape,
                _Noise = _Parameters._GrainDuration._Noise._Amount,
                _LockNoise = _Parameters._GrainDuration._Noise._FreezeOnTrigger,
                _Min = _Parameters._GrainDuration._Min * _SamplesPerMS,
                _Max = _Parameters._GrainDuration._Max * _SamplesPerMS,
                _LockStartValue = false,
                _LockEndValue = false,
                _InteractionInput = _Parameters._GrainDuration._Interaction.GetValue()
            };
            burstData._Transpose = new ModulationComponent
            {
                _StartValue = _Parameters._Transpose._Start,
                _EndValue = _Parameters._Transpose._End,
                _InteractionAmount = _Parameters._Transpose._InteractionAmount,
                _Shape = _Parameters._Transpose._InteractionShape,
                _Noise = _Parameters._Transpose._Noise._Amount,
                _LockNoise = _Parameters._Transpose._Noise._FreezeOnTrigger,
                _Min = _Parameters._Transpose._Min,
                _Max = _Parameters._Transpose._Max,
                _LockStartValue = false,
                _LockEndValue = _Parameters._Transpose._LockEndValue,
                _InteractionInput = _Parameters._Transpose._Interaction.GetValue()
            };
            burstData._Volume = new ModulationComponent
            {
                _StartValue = _Parameters._Volume._Start,
                _EndValue = _Parameters._Volume._End,
                _InteractionAmount = _Parameters._Volume._InteractionAmount,
                _Shape = _Parameters._Volume._InteractionShape,
                _Noise = _Parameters._Volume._Noise._Amount,
                _LockNoise = _Parameters._Volume._Noise._FreezeOnTrigger,
                _Min = _Parameters._Volume._Min,
                _Max = _Parameters._Volume._Max,
                _LockStartValue = false,
                _LockEndValue = _Parameters._Volume._LockEndValue,
                _InteractionInput = _Parameters._Volume._Interaction.GetValue() * _VolumeMultiply
            };
            _EntityManager.SetComponentData(_EmitterEntity, burstData);
            #endregion
        }
        // TODO hacky solution based on previous "dummy emitter" paradigm. May cause issues and require an overhaul.
        if (_ContactEmitter && _EmitterType == EmitterType.Burst)
        {
            if (_TimeExisted > 3)
                Destroy(gameObject);
        }
    }
}
