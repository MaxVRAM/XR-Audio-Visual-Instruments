﻿using Unity.Entities;
using UnityEngine;

[System.Serializable]
public class ContinuousProperties
{
    public ContinuousPlayhead _Playhead;
    public ContinuousDensity _Density;
    public ContinuousDuration _GrainDuration;
    public ContinuousTranspose _Transpose;
    public ContinuousVolume _Volume;
}

public class ContinuousAuthoring : EmitterAuthoring
{
    public ContinuousProperties _Properties;

    public override void Initialise()
    {
        _EmitterType = EmitterType.Continuous;
    }

    void Update()
    {

    }

    public override void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        Debug.Log(name + "    is being converted to entity.");
        _EmitterEntity = entity;
        _EmitterType = EmitterType.Continuous;
        int index = GrainSynth.Instance.RegisterEmitter(entity);

        #region ADD EMITTER COMPONENT DATA

        dstManager.AddComponentData(_EmitterEntity, new ContinuousComponent
        {
            _IsPlaying = !_ContactEmitter,
            _EmitterIndex = index,
            _DistanceAmplitude = 1,
            _AudioClipIndex = _ClipIndex,
            _PingPong = _PingPongGrainPlayheads,
            _SpeakerIndex = _SpeakerIndex,
            _LastSampleIndex = GrainSynth.Instance._CurrentDSPSample,
            _OutputSampleRate = AudioSettings.outputSampleRate,

            _Playhead = new ModulationComponent
            {
                _StartValue = _Properties._Playhead._Idle,
                _InteractionAmount = _Properties._Playhead._InteractionAmount,
                _Shape = _Properties._Playhead._InteractionShape,
                _Noise = _Properties._Playhead._Noise._Amount,
                _PerlinNoise = _Properties._Playhead._Noise._Perlin,
                _Min = _Properties._Playhead._Min,
                _Max = _Properties._Playhead._Max
            },
            _Density = new ModulationComponent
            {
                _StartValue = _Properties._Density._Idle,
                _InteractionAmount = _Properties._Density._InteractionAmount,
                _Shape = _Properties._Density._InteractionShape,
                _Noise = _Properties._Density._Noise._Amount,
                _PerlinNoise = _Properties._Density._Noise._Perlin,
                _Min = _Properties._Density._Min,
                _Max = _Properties._Density._Max
            },
            _Duration = new ModulationComponent
            {
                _StartValue = _Properties._GrainDuration._Idle * _SamplesPerMS,
                _InteractionAmount = _Properties._GrainDuration._InteractionAmount * _SamplesPerMS,
                _Shape = _Properties._GrainDuration._InteractionShape,
                _Noise = _Properties._GrainDuration._Noise._Amount,
                _PerlinNoise = _Properties._GrainDuration._Noise._Perlin,
                _Min = _Properties._GrainDuration._Min * _SamplesPerMS,
                _Max = _Properties._GrainDuration._Max * _SamplesPerMS
            },
            _Transpose = new ModulationComponent
            {
                _StartValue = _Properties._Transpose._Idle,
                _InteractionAmount = _Properties._Transpose._InteractionAmount,
                _Shape = _Properties._Transpose._InteractionShape,
                _Noise = _Properties._Transpose._Noise._Amount,
                _PerlinNoise = _Properties._Transpose._Noise._Perlin,
                _Min = _Properties._Transpose._Min,
                _Max = _Properties._Transpose._Max
            },
            _Volume = new ModulationComponent
            {
                _StartValue = _Properties._Volume._Idle,
                _InteractionAmount = _Properties._Volume._InteractionAmount,
                _Shape = _Properties._Volume._InteractionShape,
                _Noise = _Properties._Volume._Noise._Amount,
                _PerlinNoise = _Properties._Volume._Noise._Perlin,
                _Min = _Properties._Volume._Min,
                _Max = _Properties._Volume._Max
            }
        });

        #if UNITY_EDITOR
                dstManager.SetName(entity, "Grain Emitter:   " + transform.parent.name + " " + gameObject.name);
        #endif

        #endregion


        #region ADD DSP EFFECT COMPONENT DATA

        dstManager.AddBuffer<DSPParametersElement>(_EmitterEntity);
        DynamicBuffer<DSPParametersElement> dspParams = dstManager.GetBuffer<DSPParametersElement>(_EmitterEntity);
        for (int i = 0; i < _DSPChainParams.Length; i++)
            dspParams.Add(_DSPChainParams[i].GetDSPBufferElement());

        dstManager.AddComponentData(entity, new QuadEntityType { _Type = QuadEntityType.QuadEntityTypeEnum.Emitter });

        #endregion

        _Initialised = true;

        Debug.Log(name + "    end of entity creation with initialised status:   " + _Initialised);

    }

    protected override void UpdateComponents()
    {
        Debug.Log("Emitter volume input source: " + _Properties._Volume._InputSource.name);
        Debug.Log("Emitter volume input value: " + _Properties._Volume._InputValue);
        Debug.Log("Emitter volume input get value: " + _Properties._Volume.GetValue());
        Debug.Log("");

        ContinuousComponent entityData = _EntityManager.GetComponentData<ContinuousComponent>(_EmitterEntity);

        #region UPDATE EMITTER COMPONENT DATA
        entityData._IsPlaying = _IsPlaying;
        entityData._AudioClipIndex = _ClipIndex;
        entityData._SpeakerIndex = _SpeakerIndex;
        entityData._PingPong = _PingPongGrainPlayheads;
        entityData._LastSampleIndex = _LastSampleIndex;
        entityData._DistanceAmplitude = _DistanceAmplitude;
        entityData._OutputSampleRate = AudioSettings.outputSampleRate;

        entityData._Playhead = new ModulationComponent
        {
            _StartValue = _Properties._Playhead._Idle,
            _InteractionAmount = _Properties._Playhead._InteractionAmount,
            _Shape = _Properties._Playhead._InteractionShape,
            _Noise = _Properties._Playhead._Noise._Amount,
            _PerlinNoise = _Properties._Playhead._Noise._Perlin,
            _PerlinValue = GeneratePerlinForParameter(0),
            _Min = _Properties._Playhead._Min,
            _Max = _Properties._Playhead._Max,
            _InteractionInput = _Properties._Playhead.GetValue()
        };
        entityData._Density = new ModulationComponent
        {
            _StartValue = _Properties._Density._Idle,
            _InteractionAmount = _Properties._Density._InteractionAmount,
            _Shape = _Properties._Density._InteractionShape,
            _Noise = _Properties._Density._Noise._Amount,
            _PerlinNoise = _Properties._Density._Noise._Perlin,
            _PerlinValue = GeneratePerlinForParameter(1),
            _Min = _Properties._Density._Min,
            _Max = _Properties._Density._Max,
            _InteractionInput = _Properties._Density.GetValue()
        };
        entityData._Duration = new ModulationComponent
        {
            _StartValue = _Properties._GrainDuration._Idle * _SamplesPerMS,
            _InteractionAmount = _Properties._GrainDuration._InteractionAmount * _SamplesPerMS,
            _Shape = _Properties._GrainDuration._InteractionShape,
            _Noise = _Properties._GrainDuration._Noise._Amount,
            _PerlinNoise = _Properties._GrainDuration._Noise._Perlin,
            _PerlinValue = GeneratePerlinForParameter(2),
            _Min = _Properties._GrainDuration._Min * _SamplesPerMS,
            _Max = _Properties._GrainDuration._Max * _SamplesPerMS,
            _InteractionInput = _Properties._GrainDuration.GetValue()
        };
        entityData._Transpose = new ModulationComponent
        {
            _StartValue = _Properties._Transpose._Idle,
            _InteractionAmount = _Properties._Transpose._InteractionAmount,
            _Shape = _Properties._Transpose._InteractionShape,
            _Noise = _Properties._Transpose._Noise._Amount,
            _PerlinNoise = _Properties._Transpose._Noise._Perlin,
            _PerlinValue = GeneratePerlinForParameter(3),
            _Min = _Properties._Transpose._Min,
            _Max = _Properties._Transpose._Max,
            _InteractionInput = _Properties._Transpose.GetValue()
        };
        entityData._Volume = new ModulationComponent
        {
            _StartValue = _Properties._Volume._Idle,
            _InteractionAmount = _Properties._Volume._InteractionAmount,
            _Shape = _Properties._Volume._InteractionShape,
            _Noise = _Properties._Volume._Noise._Amount,
            _PerlinNoise = _Properties._Volume._Noise._Perlin,
            _PerlinValue = GeneratePerlinForParameter(4),
            _Min = _Properties._Volume._Min,
            _Max = _Properties._Volume._Max,
            _InteractionInput = _Properties._Volume.GetValue() * _ContactSurfaceAttenuation
        };
        _EntityManager.SetComponentData(_EmitterEntity, entityData);

        #endregion

        UpdateDSPEffectsBuffer();
    }
}
