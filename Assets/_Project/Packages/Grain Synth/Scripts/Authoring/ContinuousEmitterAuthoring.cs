using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Transforms;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using UnityEditor.UI;

[System.Serializable]
public class ContinuousParameters
{
    public ContinuousPlayhead _Playhead;
    public ContinuousDensity _Density;
    public ContinuousDuration _GrainDuration;
    public ContinuousTranspose _Transpose;
    public ContinuousVolume _Volume;
}

public class ContinuousEmitterAuthoring : BaseEmitterClass
{
    public ContinuousParameters _Parameters;

    public override void InitialiseTypeAndInteractions()
    {
        _EmitterType = EmitterType.Continuous;
        _Parameters._Playhead._Interaction.UpdateSource(_PrimaryObject, _SecondaryObject, _LatestCollision);
        _Parameters._Density._Interaction.UpdateSource(_PrimaryObject, _SecondaryObject, _LatestCollision);
        _Parameters._GrainDuration._Interaction.UpdateSource(_PrimaryObject, _SecondaryObject, _LatestCollision);
        _Parameters._Transpose._Interaction.UpdateSource(_PrimaryObject, _SecondaryObject, _LatestCollision);
        _Parameters._Volume._Interaction.UpdateSource(_PrimaryObject, _SecondaryObject, _LatestCollision);
    }

    public override void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        _EmitterEntity = entity;
        int linkedSpeakerIndex = int.MaxValue;

        if (_LinkedSpeaker == null && gameObject.GetComponent<GrainSpeakerAuthoring>() != null)
            _LinkedSpeaker = gameObject.GetComponent<GrainSpeakerAuthoring>();

        if (_LinkedSpeaker != null)
        {
            _StaticSpeakerLink = true;
            _LinkedSpeaker.AddEmitterLink(gameObject);
            linkedSpeakerIndex = _LinkedSpeaker.GetRegisterAndGetIndex();
            dstManager.AddComponentData(_EmitterEntity, new StaticLinkTag { });
        }
        _LinkedSpeakerIndex = linkedSpeakerIndex;

        int index = GrainSynth.Instance.RegisterEmitter(entity);

        #region ADD EMITTER COMPONENT DATA
        dstManager.AddComponentData(_EmitterEntity, new ContinuousEmitterComponent
        {
            _Playing = _IsPlaying,
            _LinkedToSpeaker = _StaticSpeakerLink,
            _StaticallyLinked = _StaticSpeakerLink,
            _PingPong = _PingPongAtEndOfClip,
            _LastGrainEmissionDSPIndex = GrainSynth.Instance._CurrentDSPSample,

            _Playhead = new ModulateParameterComponent
            {
                _StartValue = _Parameters._Playhead._Idle,
                _InteractionAmount = _Parameters._Playhead._InteractionAmount,
                _Shape = _Parameters._Playhead._InteractionShape,
                _Noise = _Parameters._Playhead._Noise._Amount,
                _PerlinNoise = _Parameters._Playhead._Noise._Perlin,
                _Min = _Parameters._Playhead._Min,
                _Max = _Parameters._Playhead._Max
            },
            _Density = new ModulateParameterComponent
            {
                _StartValue = _Parameters._Density._Idle,
                _InteractionAmount = _Parameters._Density._InteractionAmount,
                _Shape = _Parameters._Density._InteractionShape,
                _Noise = _Parameters._Density._Noise._Amount,
                _PerlinNoise = _Parameters._Density._Noise._Perlin,
                _Min = _Parameters._Density._Min,
                _Max = _Parameters._Density._Max
            },
            _Duration = new ModulateParameterComponent
            {
                _StartValue = _Parameters._GrainDuration._Idle * _SamplesPerMS,
                _InteractionAmount = _Parameters._GrainDuration._InteractionAmount * _SamplesPerMS,
                _Shape = _Parameters._GrainDuration._InteractionShape,
                _Noise = _Parameters._GrainDuration._Noise._Amount,
                _PerlinNoise = _Parameters._GrainDuration._Noise._Perlin,
                _Min = _Parameters._GrainDuration._Min * _SamplesPerMS,
                _Max = _Parameters._GrainDuration._Max * _SamplesPerMS
            },
            _Transpose = new ModulateParameterComponent
            {
                _StartValue = _Parameters._Transpose._Idle,
                _InteractionAmount = _Parameters._Transpose._InteractionAmount,
                _Shape = _Parameters._Transpose._InteractionShape,
                _Noise = _Parameters._Transpose._Noise._Amount,
                _PerlinNoise = _Parameters._Transpose._Noise._Perlin,
                _Min = _Parameters._Transpose._Min,
                _Max = _Parameters._Transpose._Max
            },
            _Volume = new ModulateParameterComponent
            {
                _StartValue = _Parameters._Volume._Idle,
                _InteractionAmount = _Parameters._Volume._InteractionAmount,
                _Shape = _Parameters._Volume._InteractionShape,
                _Noise = _Parameters._Volume._Noise._Amount,
                _PerlinNoise = _Parameters._Volume._Noise._Perlin,
                _Min = _Parameters._Volume._Min,
                _Max = _Parameters._Volume._Max
            },

            _DistanceAmplitude = 1,
            _AudioClipIndex = _ClipIndex,
            _SpeakerIndex = linkedSpeakerIndex,
            _EmitterIndex = index,
            _SampleRate = AudioSettings.outputSampleRate
        });

        #if UNITY_EDITOR
                dstManager.SetName(entity, "Grain Emitter:   " + transform.parent.name + " " + gameObject.name);
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
        if (_IsWithinEarshot & _IsPlaying)
        {
            ContinuousEmitterComponent continuousData = _EntityManager.GetComponentData<ContinuousEmitterComponent>(_EmitterEntity);

            #region UPDATE EMITTER COMPONENT DATA
            continuousData._Playing = _IsPlaying;
            continuousData._SpeakerIndex = _StaticSpeakerLink ? _LinkedSpeaker._SpeakerIndex : continuousData._SpeakerIndex;
            continuousData._AudioClipIndex = _ClipIndex;
            continuousData._PingPong = _PingPongAtEndOfClip;

            if (continuousData._SpeakerIndex < GrainSynth.Instance._GrainSpeakers.Count)
                continuousData._DistanceAmplitude = AudioUtils.EmitterFromSpeakerVolumeAdjust(_HeadPosition.position,
                    GrainSynth.Instance._GrainSpeakers[continuousData._SpeakerIndex].gameObject.transform.position,
                    transform.position) * _DistanceVolume;
            else
                continuousData._DistanceAmplitude = 0;

            continuousData._Playhead = new ModulateParameterComponent
            {
                _StartValue = _Parameters._Playhead._Idle,
                _InteractionAmount = _Parameters._Playhead._InteractionAmount,
                _Shape = _Parameters._Playhead._InteractionShape,
                _Noise = _Parameters._Playhead._Noise._Amount,
                _PerlinNoise = _Parameters._Playhead._Noise._Perlin,
                _PerlinValue = GeneratePerlinForParameter(0),
                _Min = _Parameters._Playhead._Min,
                _Max = _Parameters._Playhead._Max,
                _InteractionInput = _Parameters._Playhead._Interaction.GetValue()
            };
            continuousData._Density = new ModulateParameterComponent
            {
                _StartValue = _Parameters._Density._Idle,
                _InteractionAmount = _Parameters._Density._InteractionAmount,
                _Shape = _Parameters._Density._InteractionShape,
                _Noise = _Parameters._Density._Noise._Amount,
                _PerlinNoise = _Parameters._Density._Noise._Perlin,
                _PerlinValue = GeneratePerlinForParameter(1),
                _Min = _Parameters._Density._Min,
                _Max = _Parameters._Density._Max,
                _InteractionInput = _Parameters._Density._Interaction.GetValue()
            };
            continuousData._Duration = new ModulateParameterComponent
            {
                _StartValue = _Parameters._GrainDuration._Idle * _SamplesPerMS,
                _InteractionAmount = _Parameters._GrainDuration._InteractionAmount * _SamplesPerMS,
                _Shape = _Parameters._GrainDuration._InteractionShape,
                _Noise = _Parameters._GrainDuration._Noise._Amount,
                _PerlinNoise = _Parameters._GrainDuration._Noise._Perlin,
                _PerlinValue = GeneratePerlinForParameter(2),
                _Min = _Parameters._GrainDuration._Min * _SamplesPerMS,
                _Max = _Parameters._GrainDuration._Max * _SamplesPerMS,
                _InteractionInput = _Parameters._GrainDuration._Interaction.GetValue()
            };
            continuousData._Transpose = new ModulateParameterComponent
            {
                _StartValue = _Parameters._Transpose._Idle,
                _InteractionAmount = _Parameters._Transpose._InteractionAmount,
                _Shape = _Parameters._Transpose._InteractionShape,
                _Noise = _Parameters._Transpose._Noise._Amount,
                _PerlinNoise = _Parameters._Transpose._Noise._Perlin,
                _PerlinValue = GeneratePerlinForParameter(3),
                _Min = _Parameters._Transpose._Min,
                _Max = _Parameters._Transpose._Max,
                _InteractionInput = _Parameters._Transpose._Interaction.GetValue()
            };
            continuousData._Volume = new ModulateParameterComponent
            {
                _StartValue = _Parameters._Volume._Idle,
                _InteractionAmount = _Parameters._Volume._InteractionAmount,
                _Shape = _Parameters._Volume._InteractionShape,
                _Noise = _Parameters._Volume._Noise._Amount,
                _PerlinNoise = _Parameters._Volume._Noise._Perlin,
                _PerlinValue = GeneratePerlinForParameter(4),
                _Min = _Parameters._Volume._Min,
                _Max = _Parameters._Volume._Max,
                _InteractionInput = _Parameters._Volume._Interaction.GetValue() * _VolumeMultiply
            };

            _EntityManager.SetComponentData(_EmitterEntity, continuousData);
            #endregion

            _LinkedSpeakerIndex = continuousData._SpeakerIndex;
            _LinkedToSpeaker = continuousData._LinkedToSpeaker;
            _InSpeakerRange = continuousData._ListenerInRange;
        }
    }
}
