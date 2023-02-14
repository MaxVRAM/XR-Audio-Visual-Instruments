using System;
using Unity.Entities;

using PlaneWaver.Entities;
using PlaneWaver.Modulation;

namespace PlaneWaver.Synthesis
{
    /// <summary>
    //  Emitter class for building and spawning a continuous stream of audio grain playback.
    /// <summary>
    public class ContinuousAuthoring : EmitterAuthoring
    {
        public ContinuousProperties _Properties;

        #region CRAZY CONTINUOUS COMPONENT INIT

        public override void SetEntityType()
        {
            _EmitterType = EmitterType.Continuous;
            _EntityType = SynthEntityType.Emitter;
            _Archetype = _EntityManager.CreateArchetype(typeof(ContinuousComponent));
            _IsPlaying = false;
        }

        public override void InitialiseComponents()
        {
            _EntityManager.AddComponentData(_Entity, new ContinuousComponent
            {
                _IsPlaying = _PlaybackCondition != Condition.NotColliding,
                _EmitterIndex = _EntityIndex,
                _AudioClipIndex = _AudioAsset.ClipEntityIndex,
                _SpeakerIndex = _Host._AttachedSpeakerIndex,
                _HostIndex = _Host.EntityIndex,
                _VolumeAdjust = _VolumeAdjust,
                _DistanceAmplitude = 1,
                _PingPong = _PingPongGrainPlayheads,
                _SamplesUntilFade = _Host._SpawnLife.GetSamplesUntilFade(_AgeFadeout),
                _SamplesUntilDeath = _Host._SpawnLife.GetSamplesUntilDeath(),
                _LastSampleIndex = -1,
                _OutputSampleRate = _SampleRate,

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

            _EntityManager.AddBuffer<DSPParametersElement>(_Entity);
            DynamicBuffer<DSPParametersElement> dspParams = _EntityManager.GetBuffer<DSPParametersElement>(_Entity);

            for (int i = 0; i < _DSPChainParams.Length; i++)
                dspParams.Add(_DSPChainParams[i].GetDSPBufferElement());

            _EntityManager.AddComponentData(_Entity, new QuadEntityType { _Type = QuadEntityType.QuadEntityTypeEnum.Emitter });
        }

        #endregion

        #region CRAZY CONTINUOUS COMPONENT UPDATE

        public override void ProcessComponents()
        {
            UpdateEntityTags();

            if (_IsPlaying)
            {
                ContinuousComponent continuousData = _EntityManager.GetComponentData<ContinuousComponent>(_Entity);

                // Reset grain offset if attached to a new speaker
                if (_Host._AttachedSpeakerIndex != continuousData._SpeakerIndex)
                {
                    _LastSampleIndex = -1;
                    continuousData._PreviousGrainDuration = -1;
                }

                _LastSampleIndex = continuousData._LastSampleIndex;

                continuousData._IsPlaying = _IsPlaying;
                continuousData._AudioClipIndex = _AudioAsset.ClipEntityIndex;
                continuousData._SpeakerIndex = _Host._AttachedSpeakerIndex;
                continuousData._HostIndex = _Host.EntityIndex;
                continuousData._LastSampleIndex = _LastSampleIndex;
                continuousData._PingPong = _PingPongGrainPlayheads;
                continuousData._SamplesUntilFade = _Host._SpawnLife.GetSamplesUntilFade(_AgeFadeout);
                continuousData._SamplesUntilDeath = _Host._SpawnLife.GetSamplesUntilDeath();
                continuousData._VolumeAdjust = _VolumeAdjust;
                continuousData._DistanceAmplitude = _DistanceAmplitude;
                continuousData._OutputSampleRate = _SampleRate;

                continuousData._Playhead = new ModulationComponent
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
                continuousData._Density = new ModulationComponent
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
                continuousData._Duration = new ModulationComponent
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
                continuousData._Transpose = new ModulationComponent
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
                continuousData._Volume = new ModulationComponent
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
                _EntityManager.SetComponentData(_Entity, continuousData);

                UpdateDSPEffectsBuffer();
            }
        }

        #endregion
    }

}

namespace PlaneWaver.Modulation
{
    #region CONTINUOUS PARAMETERS

    [Serializable]
    public class ContinuousProperties
    {
        public ContinuousPlayhead _Playhead;
        public ContinuousDensity _Density;
        public ContinuousDuration _GrainDuration;
        public ContinuousTranspose _Transpose;
        public ContinuousVolume _Volume;
    }

    #endregion
}