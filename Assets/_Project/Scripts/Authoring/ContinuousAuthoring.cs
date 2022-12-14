using Unity.Entities;
using UnityEngine;

[System.Serializable]
public class ContinuousParameters
{
    public ContinuousPlayhead _Playhead;
    public ContinuousDensity _Density;
    public ContinuousDuration _GrainDuration;
    public ContinuousTranspose _Transpose;
    public ContinuousVolume _Volume;
}

public class ContinuousAuthoring : EmitterAuthoring
{
    public ContinuousParameters _Parameters;

    public override void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        _EmitterEntity = entity;
        _EmitterType = EmitterType.Continuous;
        int index = GrainSynth.Instance.RegisterEmitter(entity);

        #region ADD EMITTER COMPONENT DATA

        dstManager.AddComponentData(_EmitterEntity, new ContinuousComponent
        {
            _IsPlaying = false,
            _EmitterIndex = index,
            _DistanceAmplitude = 1,
            _AudioClipIndex = _ClipIndex,
            _PingPong = _PingPongAtEndOfClip,
            _SpeakerIndex = _SpeakerIndex,
            _SpeakerAttached = _Speaker != null,
            _OutputSampleRate = AudioSettings.outputSampleRate,
            _LastSampleIndex = GrainSynth.Instance._CurrentDSPSample,

            _Playhead = new ModulationComponent
            {
                _StartValue = _Parameters._Playhead._Idle,
                _InteractionAmount = _Parameters._Playhead._InteractionAmount,
                _Shape = _Parameters._Playhead._InteractionShape,
                _Noise = _Parameters._Playhead._Noise._Amount,
                _PerlinNoise = _Parameters._Playhead._Noise._Perlin,
                _Min = _Parameters._Playhead._Min,
                _Max = _Parameters._Playhead._Max
            },
            _Density = new ModulationComponent
            {
                _StartValue = _Parameters._Density._Idle,
                _InteractionAmount = _Parameters._Density._InteractionAmount,
                _Shape = _Parameters._Density._InteractionShape,
                _Noise = _Parameters._Density._Noise._Amount,
                _PerlinNoise = _Parameters._Density._Noise._Perlin,
                _Min = _Parameters._Density._Min,
                _Max = _Parameters._Density._Max
            },
            _Duration = new ModulationComponent
            {
                _StartValue = _Parameters._GrainDuration._Idle * _SamplesPerMS,
                _InteractionAmount = _Parameters._GrainDuration._InteractionAmount * _SamplesPerMS,
                _Shape = _Parameters._GrainDuration._InteractionShape,
                _Noise = _Parameters._GrainDuration._Noise._Amount,
                _PerlinNoise = _Parameters._GrainDuration._Noise._Perlin,
                _Min = _Parameters._GrainDuration._Min * _SamplesPerMS,
                _Max = _Parameters._GrainDuration._Max * _SamplesPerMS
            },
            _Transpose = new ModulationComponent
            {
                _StartValue = _Parameters._Transpose._Idle,
                _InteractionAmount = _Parameters._Transpose._InteractionAmount,
                _Shape = _Parameters._Transpose._InteractionShape,
                _Noise = _Parameters._Transpose._Noise._Amount,
                _PerlinNoise = _Parameters._Transpose._Noise._Perlin,
                _Min = _Parameters._Transpose._Min,
                _Max = _Parameters._Transpose._Max
            },
            _Volume = new ModulationComponent
            {
                _StartValue = _Parameters._Volume._Idle,
                _InteractionAmount = _Parameters._Volume._InteractionAmount,
                _Shape = _Parameters._Volume._InteractionShape,
                _Noise = _Parameters._Volume._Noise._Amount,
                _PerlinNoise = _Parameters._Volume._Noise._Perlin,
                _Min = _Parameters._Volume._Min,
                _Max = _Parameters._Volume._Max
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
        {
            dspParams.Add(_DSPChainParams[i].GetDSPBufferElement());
        }
        dstManager.AddComponentData(entity, new QuadEntityType { _Type = QuadEntityType.QuadEntityTypeEnum.Emitter });

        #endregion

        _Initialised = true;
    }

    protected override void UpdateEntity()
    {
        if (_InListenerRadius & _IsPlaying)
        {
            ContinuousComponent continuousData = _EntityManager.GetComponentData<ContinuousComponent>(_EmitterEntity);

            #region UPDATE EMITTER COMPONENT DATA
            continuousData._IsPlaying = _IsPlaying;
            continuousData._AudioClipIndex = _ClipIndex;
            continuousData._PingPong = _PingPongAtEndOfClip;
            continuousData._DistanceAmplitude = _DistanceAmplitude;
            continuousData._SpeakerIndex = _SpeakerIndex;

            // if (continuousData._SpeakerIndex < GrainSynth.Instance._GrainSpeakers.Count)
            //     continuousData._DistanceAmplitude = AudioUtils.EmitterFromSpeakerVolumeAdjust(_HeadPosition.position,
            //         GrainSynth.Instance._GrainSpeakers[continuousData._SpeakerIndex].gameObject.transform.position,
            //         transform.position) * _DistanceAmplitude;
            // else
            //     continuousData._DistanceAmplitude = 0;

            continuousData._Playhead = new ModulationComponent
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
            continuousData._Density = new ModulationComponent
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
            continuousData._Duration = new ModulationComponent
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
            continuousData._Transpose = new ModulationComponent
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
            continuousData._Volume = new ModulationComponent
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
        }
    }
}
