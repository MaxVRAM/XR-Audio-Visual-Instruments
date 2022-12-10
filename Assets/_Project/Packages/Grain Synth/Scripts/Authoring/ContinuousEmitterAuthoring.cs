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

    public override void Initialise()
    {
        _EmitterType = EmitterType.Continuous;
        //_Parameters._Playhead.CheckInteractionInput();
        //_Parameters._Density.CheckInteractionInput();
        //_Parameters._GrainDuration.CheckInteractionInput();
        //_Parameters._Transpose.CheckInteractionInput();
        //_Parameters._Volume.CheckInteractionInput();
    }

    public override void SetupContactEmitter(Collision collision, GrainSpeakerAuthoring speaker)
    {
        ResetEmitter(collision.collider.gameObject, speaker);

        _Parameters._Playhead.UpdateInteractionInput(_PrimaryObject);
        _Parameters._Density.UpdateInteractionInput(_PrimaryObject);
        _Parameters._GrainDuration.UpdateInteractionInput(_PrimaryObject);
        _Parameters._Transpose.UpdateInteractionInput(_PrimaryObject);
        _Parameters._Volume.UpdateInteractionInput(_PrimaryObject);
    }
    public override void SetupAttachedEmitter(GameObject primaryObject, GameObject secondaryObject, GrainSpeakerAuthoring speaker)
    {   
        _PrimaryObject = primaryObject;
        ResetEmitter(secondaryObject, speaker);

        _Parameters._Playhead.UpdateInteractionInput(_PrimaryObject, _SecondaryObject);
        _Parameters._Density.UpdateInteractionInput(_PrimaryObject, _SecondaryObject);
        _Parameters._GrainDuration.UpdateInteractionInput(_PrimaryObject, _SecondaryObject);
        _Parameters._Transpose.UpdateInteractionInput(_PrimaryObject, _SecondaryObject);
        _Parameters._Volume.UpdateInteractionInput(_PrimaryObject, _SecondaryObject);
    }

    public override void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        _EmitterEntity = entity;
        int attachedSpeakerIndex = int.MaxValue;

        // Statically link this emitter to speaker component if one is attached to this object
        if (_LinkedSpeaker == null && gameObject.GetComponent<GrainSpeakerAuthoring>() != null)
        {
            _LinkedSpeaker = gameObject.GetComponent<GrainSpeakerAuthoring>();          
        }

        if (_LinkedSpeaker != null)
        {
            _StaticallyLinked = true;
            _LinkedSpeaker.AddPairedEmitter(gameObject);
            attachedSpeakerIndex =_LinkedSpeaker.GetRegisterAndGetIndex();
            dstManager.AddComponentData(_EmitterEntity, new StaticallyPairedTag { });
        }
        else
        {
            Debug.Log("WARNING: " + name + " could not speaker link.");
        }

        _LinkedSpeakerIndex = attachedSpeakerIndex;

        int index = GrainSynth.Instance.RegisterEmitter(entity);

        #region ADD EMITTER COMPONENT DATA
        dstManager.AddComponentData(_EmitterEntity, new ContinuousEmitterComponent
        {
            _Playing = _IsPlaying,
            _LinkedToSpeaker = _StaticallyLinked,
            _StaticallyLinked = _StaticallyLinked,
            _PingPong = _PingPongAtEndOfClip,
            _LastGrainEmissionDSPIndex = GrainSynth.Instance._CurrentDSPSample,

            _Playhead = new ModulateParameterComponent
            {
                _StartValue = _Parameters._Playhead._Idle,
                _InteractionAmount = _Parameters._Playhead._InteractionAmount,
                _Shape = _Parameters._Playhead._InteractionShape,
                _Noise = _Parameters._Playhead._Noise,
                _PerlinNoise = _Parameters._Playhead._PerlinNoise,
                _Min = _Parameters._Playhead._Min,
                _Max = _Parameters._Playhead._Max
            },
            _Density = new ModulateParameterComponent
            {
                _StartValue = _Parameters._Density._Idle,
                _InteractionAmount = _Parameters._Density._InteractionAmount,
                _Shape = _Parameters._Density._InteractionShape,
                _Noise = _Parameters._Density._Noise,
                _PerlinNoise = _Parameters._Density._PerlinNoise,
                _Min = _Parameters._Density._Min,
                _Max = _Parameters._Density._Max
            },
            _Duration = new ModulateParameterComponent
            {
                _StartValue = _Parameters._GrainDuration._Idle * _SamplesPerMS,
                _InteractionAmount = _Parameters._GrainDuration._InteractionAmount * _SamplesPerMS,
                _Shape = _Parameters._GrainDuration._InteractionShape,
                _Noise = _Parameters._GrainDuration._Noise,
                _PerlinNoise = _Parameters._GrainDuration._PerlinNoise,
                _Min = _Parameters._GrainDuration._Min * _SamplesPerMS,
                _Max = _Parameters._GrainDuration._Max * _SamplesPerMS
            },
            _Transpose = new ModulateParameterComponent
            {
                _StartValue = _Parameters._Transpose._Idle,
                _InteractionAmount = _Parameters._Transpose._InteractionAmount,
                _Shape = _Parameters._Transpose._InteractionShape,
                _Noise = _Parameters._Transpose._Noise,
                _PerlinNoise = _Parameters._Transpose._PerlinNoise,
                _Min = _Parameters._Transpose._Min,
                _Max = _Parameters._Transpose._Max
            },
            _Volume = new ModulateParameterComponent
            {
                _StartValue = _Parameters._Volume._Idle,
                _InteractionAmount = _Parameters._Volume._InteractionAmount,
                _Shape = _Parameters._Volume._InteractionShape,
                _Noise = _Parameters._Volume._Noise,
                _PerlinNoise = _Parameters._Volume._PerlinNoise,
                _Min = _Parameters._Volume._Min,
                _Max = _Parameters._Volume._Max
            },

            _DistanceAmplitude = 1,
            _AudioClipIndex = _ClipIndex,
            _SpeakerIndex = attachedSpeakerIndex,
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
            ContinuousEmitterComponent emitterData = _EntityManager.GetComponentData<ContinuousEmitterComponent>(_EmitterEntity);

            #region UPDATE EMITTER COMPONENT DATA
            emitterData._Playing = _IsPlaying;
            emitterData._SpeakerIndex = _StaticallyLinked ? _LinkedSpeaker._SpeakerIndex : emitterData._SpeakerIndex;
            emitterData._AudioClipIndex = _ClipIndex;
            emitterData._PingPong = _PingPongAtEndOfClip;

            if (emitterData._SpeakerIndex >= GrainSynth.Instance._GrainSpeakers.Count)
            {
                emitterData._DistanceAmplitude = AudioUtils.EmitterFromSpeakerVolumeAdjust(_HeadPosition.position,
                    GrainSynth.Instance._GrainSpeakers[emitterData._SpeakerIndex].gameObject.transform.position,
                    transform.position) * _DistanceVolume;
            }
            else
            {
                //print(gameObject.name + " - Speaker index out of range ERROR. " + emitterData._SpeakerIndex + " / " + GrainSynth.Instance._GrainSpeakers.Count);
                emitterData._DistanceAmplitude = 0;
            }

            emitterData._Playhead = new ModulateParameterComponent
            {
                _StartValue = _Parameters._Playhead._Idle,
                _InteractionAmount = _Parameters._Playhead._InteractionAmount,
                _Shape = _Parameters._Playhead._InteractionShape,
                _Noise = _Parameters._Playhead._Noise,
                _PerlinNoise = _Parameters._Playhead._PerlinNoise,
                _PerlinValue = GeneratePerlinForParameter(0),
                _Min = _Parameters._Playhead._Min,
                _Max = _Parameters._Playhead._Max,
                _InteractionInput = _Parameters._Playhead.GetInteractionValue()
            };
            emitterData._Density = new ModulateParameterComponent
            {
                _StartValue = _Parameters._Density._Idle,
                _InteractionAmount = _Parameters._Density._InteractionAmount,
                _Shape = _Parameters._Density._InteractionShape,
                _Noise = _Parameters._Density._Noise,
                _PerlinNoise = _Parameters._Density._PerlinNoise,
                _PerlinValue = GeneratePerlinForParameter(1),
                _Min = _Parameters._Density._Min,
                _Max = _Parameters._Density._Max,
                _InteractionInput = _Parameters._Density.GetInteractionValue()
            };
            emitterData._Duration = new ModulateParameterComponent
            {
                _StartValue = _Parameters._GrainDuration._Idle * _SamplesPerMS,
                _InteractionAmount = _Parameters._GrainDuration._InteractionAmount * _SamplesPerMS,
                _Shape = _Parameters._GrainDuration._InteractionShape,
                _Noise = _Parameters._GrainDuration._Noise,
                _PerlinNoise = _Parameters._GrainDuration._PerlinNoise,
                _PerlinValue = GeneratePerlinForParameter(2),
                _Min = _Parameters._GrainDuration._Min * _SamplesPerMS,
                _Max = _Parameters._GrainDuration._Max * _SamplesPerMS,
                _InteractionInput = _Parameters._GrainDuration.GetInteractionValue()
            };
            emitterData._Transpose = new ModulateParameterComponent
            {
                _StartValue = _Parameters._Transpose._Idle,
                _InteractionAmount = _Parameters._Transpose._InteractionAmount,
                _Shape = _Parameters._Transpose._InteractionShape,
                _Noise = _Parameters._Transpose._Noise,
                _PerlinNoise = _Parameters._Transpose._PerlinNoise,
                _PerlinValue = GeneratePerlinForParameter(3),
                _Min = _Parameters._Transpose._Min,
                _Max = _Parameters._Transpose._Max,
                _InteractionInput = _Parameters._Transpose.GetInteractionValue()
            };
            emitterData._Volume = new ModulateParameterComponent
            {
                _StartValue = _Parameters._Volume._Idle,
                _InteractionAmount = _Parameters._Volume._InteractionAmount,
                _Shape = _Parameters._Volume._InteractionShape,
                _Noise = _Parameters._Volume._Noise,
                _PerlinNoise = _Parameters._Volume._PerlinNoise,
                _PerlinValue = GeneratePerlinForParameter(4),
                _Min = _Parameters._Volume._Min,
                _Max = _Parameters._Volume._Max,
                _InteractionInput = _Parameters._Volume.GetInteractionValue() * _VolumeMultiply
            };

            _EntityManager.SetComponentData(_EmitterEntity, emitterData);
            #endregion

            _LinkedSpeakerIndex = emitterData._SpeakerIndex;
            _LinkedToSpeaker = emitterData._LinkedToSpeaker;
            _InSpeakerRange = emitterData._ListenerInRange;
        }
    }
}
