using System;
using Unity.Entities;

using NaughtyAttributes;

namespace PlaneWaver
{
    /// <summary>
    //  Emitter class for building and spawning a continuous stream of audio grain playback.
    /// <summary>
    public class ContinuousAuthoring : EmitterAuthoring
    {
        [AllowNesting]
        [HorizontalLine(color: EColor.White)]
        public ContinuousProperties _Properties;

        #region CRAZY CONTINUOUS COMPONENT INIT

        public override void SetEntityType()
        {
            _EmitterType = EmitterType.Continuous;
            _EntityType = SynthEntityType.Emitter;

            _Properties._Volume.SetModulationInput(_ModulationInputs[0]);
            _Properties._Playhead.SetModulationInput(_ModulationInputs[1]);
            _Properties._GrainDuration.SetModulationInput(_ModulationInputs[2]);
            _Properties._Density.SetModulationInput(_ModulationInputs[3]);
            _Properties._Transpose.SetModulationInput(_ModulationInputs[4]);

            _Archetype = _EntityManager.CreateArchetype(typeof(ContinuousComponent));
            _IsPlaying = _PlaybackCondition != Condition.NotColliding;
        }

        public override void InitialiseComponents()
        {
            _EntityManager.AddComponentData(_Entity, new ContinuousComponent
            {
                _IsPlaying = _PlaybackCondition != Condition.NotColliding,
                _EmitterIndex = _EntityIndex,
                _AudioClipIndex = _AudioAsset.ClipEntityIndex,
                _SpeakerIndex = Host.AttachedSpeakerIndex,
                _HostIndex = Host.EntityIndex,
                _VolumeAdjust = _VolumeAdjust,
                _DistanceAmplitude = 1,
                _PingPong = _PingPongGrainPlayheads,
                _SamplesUntilFade = Host._SpawnLife.GetSamplesUntilFade(_AgeFadeout),
                _SamplesUntilDeath = Host._SpawnLife.GetSamplesUntilDeath(),
                _LastSampleIndex = -1,
                _OutputSampleRate = _SampleRate,

                _Playhead = new ModulationComponent
                {
                    _StartValue = _Properties._Playhead._Idle,
                    _InteractionAmount = _Properties._Playhead._ModulationAmount,
                    _Shape = _Properties._Playhead._ModulationExponent,
                    _Noise = _Properties._Playhead._Noise._Amount,
                    _PerlinNoise = _Properties._Playhead._Noise._Perlin,
                    _Min = _Properties._Playhead._Min,
                    _Max = _Properties._Playhead._Max
                },
                _Density = new ModulationComponent
                {
                    _StartValue = _Properties._Density._Idle,
                    _InteractionAmount = _Properties._Density._ModulationAmount,
                    _Shape = _Properties._Density._ModulationExponent,
                    _Noise = _Properties._Density._Noise._Amount,
                    _PerlinNoise = _Properties._Density._Noise._Perlin,
                    _Min = _Properties._Density._Min,
                    _Max = _Properties._Density._Max
                },
                _Duration = new ModulationComponent
                {
                    _StartValue = _Properties._GrainDuration._Idle * _SamplesPerMS,
                    _InteractionAmount = _Properties._GrainDuration._ModulationAmount * _SamplesPerMS,
                    _Shape = _Properties._GrainDuration._ModulationExponent,
                    _Noise = _Properties._GrainDuration._Noise._Amount,
                    _PerlinNoise = _Properties._GrainDuration._Noise._Perlin,
                    _Min = _Properties._GrainDuration._Min * _SamplesPerMS,
                    _Max = _Properties._GrainDuration._Max * _SamplesPerMS
                },
                _Transpose = new ModulationComponent
                {
                    _StartValue = _Properties._Transpose._Idle,
                    _InteractionAmount = _Properties._Transpose._ModulationAmount,
                    _Shape = _Properties._Transpose._ModulationExponent,
                    _Noise = _Properties._Transpose._Noise._Amount,
                    _PerlinNoise = _Properties._Transpose._Noise._Perlin,
                    _Min = _Properties._Transpose._Min,
                    _Max = _Properties._Transpose._Max
                },
                _Volume = new ModulationComponent
                {
                    _StartValue = _Properties._Volume._Idle,
                    _InteractionAmount = _Properties._Volume._ModulationAmount,
                    _Shape = _Properties._Volume._ModulationExponent,
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
            _IsPlaying = _PlaybackCondition == Condition.Always ? true : _IsPlaying;

            UpdateEntityTags();

            if (IsPlaying)
            {
                UpdateModulationValues();
                ContinuousComponent continuousData = _EntityManager.GetComponentData<ContinuousComponent>(_Entity);

                // Reset grain offset if attached to a new speaker
                if (Host.AttachedSpeakerIndex != continuousData._SpeakerIndex)
                {
                    _LastSampleIndex = -1;
                    continuousData._PreviousGrainDuration = -1;
                }

                _LastSampleIndex = continuousData._LastSampleIndex;

                continuousData._IsPlaying = IsPlaying;
                continuousData._AudioClipIndex = _AudioAsset.ClipEntityIndex;
                continuousData._SpeakerIndex = Host.AttachedSpeakerIndex;
                continuousData._HostIndex = Host.EntityIndex;
                continuousData._LastSampleIndex = _LastSampleIndex;
                continuousData._PingPong = _PingPongGrainPlayheads;
                continuousData._SamplesUntilFade = Host._SpawnLife.GetSamplesUntilFade(_AgeFadeout);
                continuousData._SamplesUntilDeath = Host._SpawnLife.GetSamplesUntilDeath();
                continuousData._VolumeAdjust = _VolumeAdjust;
                continuousData._DistanceAmplitude = DistanceAmplitude;
                continuousData._OutputSampleRate = _SampleRate;

                continuousData._Playhead = new ModulationComponent
                {
                    _StartValue = _Properties._Playhead._Idle,
                    _InteractionAmount = _Properties._Playhead._ModulationAmount,
                    _Shape = _Properties._Playhead._ModulationExponent,
                    _Noise = _Properties._Playhead._Noise._Amount,
                    _PerlinNoise = _Properties._Playhead._Noise._Perlin,
                    _PerlinValue = GeneratePerlinForParameter(0, _Properties._Playhead._Noise._Speed),
                    _Min = _Properties._Playhead._Min,
                    _Max = _Properties._Playhead._Max,
                    _InteractionInput = _Properties._Playhead.GetValue()
                };
                continuousData._Density = new ModulationComponent
                {
                    _StartValue = _Properties._Density._Idle,
                    _InteractionAmount = _Properties._Density._ModulationAmount,
                    _Shape = _Properties._Density._ModulationExponent,
                    _Noise = _Properties._Density._Noise._Amount,
                    _PerlinNoise = _Properties._Density._Noise._Perlin,
                    _PerlinValue = GeneratePerlinForParameter(1, _Properties._Density._Noise._Speed),
                    _Min = _Properties._Density._Min,
                    _Max = _Properties._Density._Max,
                    _InteractionInput = _Properties._Density.GetValue()
                };
                continuousData._Duration = new ModulationComponent
                {
                    _StartValue = _Properties._GrainDuration._Idle * _SamplesPerMS,
                    _InteractionAmount = _Properties._GrainDuration._ModulationAmount * _SamplesPerMS,
                    _Shape = _Properties._GrainDuration._ModulationExponent,
                    _Noise = _Properties._GrainDuration._Noise._Amount,
                    _PerlinNoise = _Properties._GrainDuration._Noise._Perlin,
                    _PerlinValue = GeneratePerlinForParameter(2, _Properties._GrainDuration._Noise._Speed),
                    _Min = _Properties._GrainDuration._Min * _SamplesPerMS,
                    _Max = _Properties._GrainDuration._Max * _SamplesPerMS,
                    _InteractionInput = _Properties._GrainDuration.GetValue()
                };
                continuousData._Transpose = new ModulationComponent
                {
                    _StartValue = _Properties._Transpose._Idle,
                    _InteractionAmount = _Properties._Transpose._ModulationAmount,
                    _Shape = _Properties._Transpose._ModulationExponent,
                    _Noise = _Properties._Transpose._Noise._Amount,
                    _PerlinNoise = _Properties._Transpose._Noise._Perlin,
                    _PerlinValue = GeneratePerlinForParameter(3, _Properties._Transpose._Noise._Speed),
                    _Min = _Properties._Transpose._Min,
                    _Max = _Properties._Transpose._Max,
                    _InteractionInput = _Properties._Transpose.GetValue()
                };
                continuousData._Volume = new ModulationComponent
                {
                    _StartValue = _Properties._Volume._Idle,
                    _InteractionAmount = _Properties._Volume._ModulationAmount,
                    _Shape = _Properties._Volume._ModulationExponent,
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

    #region CONTINUOUS PARAMETERS

    [Serializable]
    public class ContinuousProperties
    {
        [AllowNesting]
        [HorizontalLine(color: EColor.Clear)]
        public ContinuousVolume _Volume;
        [AllowNesting]
        [HorizontalLine(color: EColor.Clear)]
        public ContinuousPlayhead _Playhead;
        [AllowNesting]
        [HorizontalLine(color: EColor.Clear)]
        public ContinuousDuration _GrainDuration;
        [AllowNesting]
        [HorizontalLine(color: EColor.Clear)]
        public ContinuousDensity _Density;
        [AllowNesting]
        [HorizontalLine(color: EColor.Clear)]
        public ContinuousTranspose _Transpose;
    }

    #endregion
}