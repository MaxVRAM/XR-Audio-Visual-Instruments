﻿using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Transforms;
using System.Collections.Generic;
using Random = UnityEngine.Random;

[System.Serializable]
public class EmissionProps
{
    public bool _Playing = true;
    public int _ClipIndex = 0;

    public EmitterPropPlayhead _Playhead;
    public EmitterPropDensity _Density;
    public EmitterPropDuration _GrainDuration;
    public EmitterPropTranspose _Transpose;
    public EmitterPropVolume _Volume;

}

public class GrainEmitterAuthoring : BaseEmitterClass
{
    public EmissionProps _EmissionProps;

    public override void Initialise()
    {
        _EmitterType = EmitterType.Grain;
    }

    public override void SetupTempEmitter(GameObject collidingGameObject, GrainSpeakerAuthoring speaker)
    {
        _ColldingObject = collidingGameObject;
        _EmitterSetup = EmitterSetup.Temp;
        _EmissionProps._Playing = true;
        _Colliding = true;
        _CollisionTriggered = true;
        _PairedSpeaker = speaker;
        _StaticallyPaired = true;

        _EmissionProps._Playhead._InteractionInput.UpdateSourceObject(this.transform.parent.gameObject);
        _EmissionProps._Density._InteractionInput.UpdateSourceObject(this.transform.parent.gameObject);
        _EmissionProps._GrainDuration._InteractionInput.UpdateSourceObject(this.transform.parent.gameObject);
        _EmissionProps._Transpose._InteractionInput.UpdateSourceObject(this.transform.parent.gameObject);
        _EmissionProps._Volume._InteractionInput.UpdateSourceObject(this.transform.parent.gameObject);
    }



    public override void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        _EmitterEntity = entity;

        // If this emitter has a speaker componenet then it is statically paired        
        if (_PairedSpeaker == null && gameObject.GetComponent<GrainSpeakerAuthoring>() != null)
        {
            _PairedSpeaker = gameObject.GetComponent<GrainSpeakerAuthoring>();          
        }

        int attachedSpeakerIndex = int.MaxValue;

        if(_PairedSpeaker != null)
        {
            _PairedSpeaker.AddPairedEmitter(gameObject);
            _StaticallyPaired = true;
            dstManager.AddComponentData(_EmitterEntity, new StaticallyPairedTag { });
            attachedSpeakerIndex =_PairedSpeaker.GetRegisterAndGetIndex();
        }

        int index = GrainSynth.Instance.RegisterEmitter(entity);
        int samplesPerMS = (int)(AudioSettings.outputSampleRate * .001f);

        #region ADD EMITTER COMPONENT
        dstManager.AddComponentData(_EmitterEntity, new EmitterComponent
        {
            _Playing = _EmissionProps._Playing,
            _AttachedToSpeaker = _StaticallyPaired,
            _StaticallyPaired = _StaticallyPaired,
            _PingPong = _PingPongAtEndOfClip,

            _Playhead = new ModulateParameterComponent
            {
                _StartValue = _EmissionProps._Playhead._Idle,
                _InteractionAmount = _EmissionProps._Playhead._InteractionAmount,
                _Shape = _EmissionProps._Playhead._InteractionShape,
                _Noise = _EmissionProps._Playhead._Noise,
                _PerlinNoise = _EmissionProps._Playhead._PerlinNoise,
                _Min = _EmissionProps._Playhead._Min,
                _Max = _EmissionProps._Playhead._Max
            },
            _Density = new ModulateParameterComponent
            {
                _StartValue = _EmissionProps._Density._Idle,
                _InteractionAmount = _EmissionProps._Density._InteractionAmount,
                _Shape = _EmissionProps._Density._InteractionShape,
                _Noise = _EmissionProps._Density._Noise,
                _PerlinNoise = _EmissionProps._Density._PerlinNoise,
                _Min = _EmissionProps._Density._Min,
                _Max = _EmissionProps._Density._Max
            },
            _Duration = new ModulateParameterComponent
            {
                _StartValue = _EmissionProps._GrainDuration._Idle * samplesPerMS,
                _InteractionAmount = _EmissionProps._GrainDuration._InteractionAmount * samplesPerMS,
                _Shape = _EmissionProps._GrainDuration._InteractionShape,
                _Noise = _EmissionProps._GrainDuration._Noise,
                _PerlinNoise = _EmissionProps._GrainDuration._PerlinNoise,
                _Min = _EmissionProps._GrainDuration._Min * samplesPerMS,
                _Max = _EmissionProps._GrainDuration._Max * samplesPerMS
            },
            _Transpose = new ModulateParameterComponent
            {
                _StartValue = _EmissionProps._Transpose._Idle,
                _InteractionAmount = _EmissionProps._Transpose._InteractionAmount,
                _Shape = _EmissionProps._Transpose._InteractionShape,
                _Noise = _EmissionProps._Transpose._Noise,
                _PerlinNoise = _EmissionProps._Transpose._PerlinNoise,
                _Min = _EmissionProps._Transpose._Min,
                _Max = _EmissionProps._Transpose._Max
            },
            _Volume = new ModulateParameterComponent
            {
                _StartValue = _EmissionProps._Volume._Idle,
                _InteractionAmount = _EmissionProps._Volume._InteractionAmount,
                _Shape = _EmissionProps._Volume._InteractionShape,
                _Noise = _EmissionProps._Volume._Noise,
                _PerlinNoise = _EmissionProps._Volume._PerlinNoise,
                _Min = _EmissionProps._Volume._Min,
                _Max = _EmissionProps._Volume._Max
            },

            _DistanceAmplitude = 1,
            _AudioClipIndex = _EmissionProps._ClipIndex,
            _SpeakerIndex = attachedSpeakerIndex,
            _EmitterIndex = index,
            _SampleRate = AudioSettings.outputSampleRate
        });

#if UNITY_EDITOR
        dstManager.SetName(entity, "Emitter");
#endif

#endregion


        dstManager.AddBuffer<DSPParametersElement>(_EmitterEntity);
        DynamicBuffer<DSPParametersElement> dspParams = dstManager.GetBuffer<DSPParametersElement>(_EmitterEntity);
        for (int i = 0; i < _DSPChainParams.Length; i++)
        {
            dspParams.Add(_DSPChainParams[i].GetDSPBufferElement());
        }
            
        dstManager.AddComponentData(entity, new QuadEntityType { _Type = QuadEntityType.QuadEntityTypeEnum.Emitter });

        _Initialized = true;
    }

    void Update()
    {
        if (!_Initialized)
            return;

        if ((_EmitterSetup == EmitterSetup.Temp && !_Colliding) || _EmitterSetup == EmitterSetup.Dummy)
            _EmissionProps._Playing = false;

        _CurrentDistance = Mathf.Abs((_HeadPosition.position - transform.position).magnitude);

        if (_CurrentDistance < _MaxAudibleDistance)
        {
            _WithinEarshot = true;
            _EntityManager.AddComponent<WithinEarshot>(_EmitterEntity);
        }
        else
        {
            _WithinEarshot = false;
            _EntityManager.RemoveComponent<WithinEarshot>(_EmitterEntity);
        }

        float samplesPerMS = AudioSettings.outputSampleRate * 0.001f;

        EmitterComponent emitterData = _EntityManager.GetComponentData<EmitterComponent>(_EmitterEntity);

        int attachedSpeakerIndex = _StaticallyPaired ? _PairedSpeaker._SpeakerIndex : emitterData._SpeakerIndex;

        _DistanceVolume = AudioUtils.EmitterFromListenerVolumeAdjust(_HeadPosition.position, transform.position, _MaxAudibleDistance);

        float volumeDistanceAdjust = 0;
        if (attachedSpeakerIndex >= GrainSynth.Instance._GrainSpeakers.Count)
            print(gameObject.name + " - Speaker index out of range ERROR. " + attachedSpeakerIndex + " / " + GrainSynth.Instance._GrainSpeakers.Count);
        else
        {
            volumeDistanceAdjust = AudioUtils.EmitterFromSpeakerVolumeAdjust(_HeadPosition.position,
                    GrainSynth.Instance._GrainSpeakers[attachedSpeakerIndex].gameObject.transform.position,
                    transform.position) * _DistanceVolume;
        }

        EmitterComponent emitter = _EntityManager.GetComponentData<EmitterComponent>(_EmitterEntity);

        emitterData._Playing = _EmissionProps._Playing;
        emitterData._SpeakerIndex = attachedSpeakerIndex;
        emitterData._AudioClipIndex = _EmissionProps._ClipIndex;
        emitterData._PingPong = _PingPongAtEndOfClip;

        #region UPDATE EMITTER COMPONENT
        emitterData._Playhead = new ModulateParameterComponent
        {
            _StartValue = _EmissionProps._Playhead._Idle,
            _InteractionAmount = _EmissionProps._Playhead._InteractionAmount,
            _Shape = _EmissionProps._Playhead._InteractionShape,
            _Noise = _EmissionProps._Playhead._Noise,
            _PerlinNoise = _EmissionProps._Playhead._PerlinNoise,
            _PerlinValue = GeneratePerlinForParameter(0),
            _Min = _EmissionProps._Playhead._Min,
            _Max = _EmissionProps._Playhead._Max,
            _InteractionInput = _EmissionProps._Playhead.GetInteractionValue()
        };
        emitterData._Density = new ModulateParameterComponent
        {
            _StartValue = _EmissionProps._Density._Idle,
            _InteractionAmount = _EmissionProps._Density._InteractionAmount,
            _Shape = _EmissionProps._Density._InteractionShape,
            _Noise = _EmissionProps._Density._Noise,
            _PerlinNoise = _EmissionProps._Density._PerlinNoise,
            _PerlinValue = GeneratePerlinForParameter(1),
            _Min = _EmissionProps._Density._Min,
            _Max = _EmissionProps._Density._Max,
            _InteractionInput = _EmissionProps._Density.GetInteractionValue()
        };
        emitterData._Duration = new ModulateParameterComponent
        {
            _StartValue = _EmissionProps._GrainDuration._Idle * samplesPerMS,
            _InteractionAmount = _EmissionProps._GrainDuration._InteractionAmount * samplesPerMS,
            _Shape = _EmissionProps._GrainDuration._InteractionShape,
            _Noise = _EmissionProps._GrainDuration._Noise,
            _PerlinNoise = _EmissionProps._GrainDuration._PerlinNoise,
            _PerlinValue = GeneratePerlinForParameter(2),
            _Min = _EmissionProps._GrainDuration._Min * samplesPerMS,
            _Max = _EmissionProps._GrainDuration._Max * samplesPerMS,
            _InteractionInput = _EmissionProps._GrainDuration.GetInteractionValue()
        };
        emitterData._Transpose = new ModulateParameterComponent
        {
            _StartValue = _EmissionProps._Transpose._Idle,
            _InteractionAmount = _EmissionProps._Transpose._InteractionAmount,
            _Shape = _EmissionProps._Transpose._InteractionShape,
            _Noise = _EmissionProps._Transpose._Noise,
            _PerlinNoise = _EmissionProps._Transpose._PerlinNoise,
            _PerlinValue = GeneratePerlinForParameter(3),
            _Min = _EmissionProps._Transpose._Min,
            _Max = _EmissionProps._Transpose._Max,
            _InteractionInput = _EmissionProps._Transpose.GetInteractionValue()
        };
        emitterData._Volume = new ModulateParameterComponent
        {
            _StartValue = _EmissionProps._Volume._Idle,
            _InteractionAmount = _EmissionProps._Volume._InteractionAmount,
            _Shape = _EmissionProps._Volume._InteractionShape,
            _Noise = _EmissionProps._Volume._Noise,
            _PerlinNoise = _EmissionProps._Volume._PerlinNoise,
            _PerlinValue = GeneratePerlinForParameter(4),
            _Min = _EmissionProps._Volume._Min,
            _Max = _EmissionProps._Volume._Max,
            _InteractionInput = _EmissionProps._Volume.GetInteractionValue() * _VolumeMultiply
        };

        emitterData._DistanceAmplitude = volumeDistanceAdjust;
        _EntityManager.SetComponentData(_EmitterEntity, emitterData);
        #endregion

        //---   DSP CHAIN        
        UpdateDSPBuffer();

        _InRangeTemp = emitterData._InRange;

        _AttachedSpeakerIndex = emitterData._SpeakerIndex;
        _AttachedToSpeaker = emitterData._AttachedToSpeaker;

        Translation trans = _EntityManager.GetComponentData<Translation>(_EmitterEntity);
        _EntityManager.SetComponentData(_EmitterEntity, new Translation
        {
            Value = transform.position
        });
    }
}
