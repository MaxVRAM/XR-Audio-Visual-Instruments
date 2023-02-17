using UnityEngine;
using Unity.Entities;
using Serializable = System.SerializableAttribute;

using NaughtyAttributes;

namespace PlaneWaver
{
    /// <summary>
    //  Emitter class for building and spawning a continuous stream of audio grain playback.
    /// <summary>
    public class ContinuousAuthoring : EmitterAuthoring
    {
        #region MODULATION PARAMETERS
     
        [Serializable]
        public class NoiseModule
        {
            public float Influence => _Influence * _Multiplier;
            [Range(-1f, 1f)] public float _Influence = 0f;
            public float _Multiplier = 0.1f;
            public float _Speed = 1f;
            public bool _Perlin = false;
        }

        Vector2 _VolumeRange = new (0f, 2f);
        Vector2 _PlayheadRange = new (0f, 1f);
        Vector2 _DurationRange = new (10f, 500f);
        Vector2 _DensityRange = new (1f, 10f);
        Vector2 _TransposeRange = new(-3f, 3f);

        [AllowNesting]
        [HorizontalLine(color: EColor.Gray)]
        [Range(0f, 2f)] public float _VolumeIdle = 0f;
        [Range(-1f, 1f)] public float _VolumeModulation = 1f;
        public ModulationInput _VolumeInput;
        public NoiseModule _VolumeNoise;
        [AllowNesting]
        [HorizontalLine(color: EColor.Gray)]
        [Range(0f, 1f)] public float _PlayheadIdle = 0f;
        [Range(-1f, 1f)] public float _PlayheadModulation = 1f;
        public ModulationInput _PlayheadInput;
        public NoiseModule _PlayheadNoise;
        [AllowNesting]
        [HorizontalLine(color: EColor.Gray)]
        [Range(10f, 500f)] public float _DurationIdle = 80f;
        [Range(-1f, 1f)] public float _DurationModulation = 0f;
        public ModulationInput _DurationInput;
        public NoiseModule _DurationNoise;
        [AllowNesting]
        [HorizontalLine(color: EColor.Gray)]
        [Range(1f, 10f)] public float _DensityIdle = 3f;
        [Range(-1f, 1f)] public float _DensityModulation = 0f;
        public ModulationInput _DensityInput;
        public NoiseModule _DensityNoise;
        [AllowNesting]
        [HorizontalLine(color: EColor.Gray)]
        [Range(-3f, 3f)] public float _TransposeIdle = 0f;
        [Range(-1f, 1f)] public float _TransposeModulation = 0f;
        public ModulationInput _TransposeInput;
        public NoiseModule _TransposeNoise;

        public override ModulationInput[] GatherModulationInputs()
        {
            ModulationInput[] modulationInputs = new ModulationInput[5];
            modulationInputs[0] = _VolumeInput;
            modulationInputs[1] = _PlayheadInput;
            modulationInputs[2] = _DurationInput;
            modulationInputs[3] = _DensityInput;
            modulationInputs[4] = _TransposeInput;
            return modulationInputs;
        }

        #endregion

        #region CONCISE CONTINUOUS COMPONENT INIT

        public override void SetEntityType()
        {
            _EmitterType = EmitterType.Continuous;
            _EntityType = SynthEntityType.Emitter;
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


                _Volume = new ModulationComponent
                {
                    _StartValue = _VolumeIdle,
                    _Modulation = _VolumeModulation,
                    _Exponent = _VolumeInput.Exponent,
                    _Noise = _VolumeNoise.Influence,
                    _PerlinNoise = _VolumeNoise._Perlin,
                    _Min = _VolumeRange.x,
                    _Max = _VolumeRange.y
                },
                _Playhead = new ModulationComponent
                {
                    _StartValue = _VolumeIdle,
                    _Modulation = _PlayheadModulation,
                    _Exponent = _PlayheadInput.Exponent,
                    _Noise = _PlayheadNoise.Influence,
                    _PerlinNoise = _PlayheadNoise._Perlin,
                    _Min = _PlayheadRange.x,
                    _Max = _PlayheadRange.y
                },
                _Duration = new ModulationComponent
                {
                    _StartValue = _DurationIdle * _SamplesPerMS,
                    _Modulation = _DurationModulation,
                    _Exponent = _DurationInput.Exponent,
                    _Noise = _DurationNoise.Influence,
                    _PerlinNoise = _DurationNoise._Perlin,
                    _Min = _DurationRange.x * _SamplesPerMS,
                    _Max = _DurationRange.y * _SamplesPerMS
                },
                _Density = new ModulationComponent
                {
                    _StartValue = _DensityIdle,
                    _Modulation = _DensityModulation,
                    _Exponent = _DensityInput.Exponent,
                    _Noise = _DensityNoise.Influence,
                    _PerlinNoise = _DensityNoise._Perlin,
                    _Min = _DensityRange.x,
                    _Max = _DensityRange.y
                },
                _Transpose = new ModulationComponent
                {
                    _StartValue = _TransposeIdle,
                    _Modulation = _TransposeModulation,
                    _Exponent = _TransposeInput.Exponent,
                    _Noise = _TransposeNoise.Influence,
                    _PerlinNoise = _TransposeNoise._Perlin,
                    _Min = _TransposeRange.x,
                    _Max = _TransposeRange.y
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
                ContinuousComponent entity = _EntityManager.GetComponentData<ContinuousComponent>(_Entity);

                // Reset grain offset if attached to a new speaker
                if (Host.AttachedSpeakerIndex != entity._SpeakerIndex)
                {
                    _LastSampleIndex = -1;
                    entity._PreviousGrainDuration = -1;
                }

                _LastSampleIndex = entity._LastSampleIndex;

                entity._IsPlaying = IsPlaying;
                entity._AudioClipIndex = _AudioAsset.ClipEntityIndex;
                entity._SpeakerIndex = Host.AttachedSpeakerIndex;
                entity._HostIndex = Host.EntityIndex;
                entity._LastSampleIndex = _LastSampleIndex;
                entity._PingPong = _PingPongGrainPlayheads;
                entity._SamplesUntilFade = Host._SpawnLife.GetSamplesUntilFade(_AgeFadeout);
                entity._SamplesUntilDeath = Host._SpawnLife.GetSamplesUntilDeath();
                entity._VolumeAdjust = _VolumeAdjust;
                entity._DistanceAmplitude = DistanceAmplitude;
                entity._OutputSampleRate = _SampleRate;

                entity._Volume = new ModulationComponent
                {
                    _StartValue = _VolumeIdle,
                    _Modulation = _VolumeModulation,
                    _Exponent = _VolumeInput.Exponent,
                    _Noise = _VolumeNoise.Influence,
                    _PerlinNoise = _VolumeNoise._Perlin,
                    _PerlinValue = GeneratePerlinForParameter(0, _VolumeNoise._Speed),
                    _Min = _VolumeRange.x,
                    _Max = _VolumeRange.y,
                    _Input = _VolumeInput.Result * _ContactSurfaceAttenuation
                };
                entity._Playhead = new ModulationComponent
                {
                    _StartValue = _PlayheadIdle,
                    _Modulation = _PlayheadModulation,
                    _Exponent = _PlayheadInput.Exponent,
                    _Noise = _PlayheadNoise.Influence,
                    _PerlinNoise = _PlayheadNoise._Perlin,
                    _PerlinValue = GeneratePerlinForParameter(1, _PlayheadNoise._Speed),
                    _Min = _PlayheadRange.x,
                    _Max = _PlayheadRange.y,
                    _Input = _PlayheadInput.Result
                };
                entity._Duration = new ModulationComponent
                {
                    _StartValue = _DurationIdle * _SamplesPerMS,
                    _Modulation = _DurationModulation,
                    _Exponent = _DurationInput.Exponent,
                    _Noise = _DurationNoise.Influence,
                    _PerlinNoise = _DurationNoise._Perlin,
                    _PerlinValue = GeneratePerlinForParameter(2, _DurationNoise._Speed),
                    _Min = _DurationRange.x * _SamplesPerMS,
                    _Max = _DurationRange.y * _SamplesPerMS,
                    _Input = _DurationInput.Result
                };
                entity._Density = new ModulationComponent
                {
                    _StartValue = _DensityIdle,
                    _Modulation = _DensityModulation,
                    _Exponent = _DensityInput.Exponent,
                    _Noise = _DensityNoise.Influence,
                    _PerlinNoise = _DensityNoise._Perlin,
                    _PerlinValue = GeneratePerlinForParameter(3, _DensityNoise._Speed),
                    _Min = _DensityRange.x,
                    _Max = _DensityRange.y,
                    _Input = _DensityInput.Result
                };
                entity._Transpose = new ModulationComponent
                {
                    _StartValue = _TransposeIdle,
                    _Modulation = _TransposeModulation,
                    _Exponent = _TransposeInput.Exponent,
                    _Noise = _TransposeNoise.Influence,
                    _PerlinNoise = _TransposeNoise._Perlin,
                    _PerlinValue = GeneratePerlinForParameter(4, _TransposeNoise._Speed),
                    _Min = _TransposeRange.x,
                    _Max = _TransposeRange.y,
                    _Input = _TransposeInput.Result
                };
                _EntityManager.SetComponentData(_Entity, entity);

                UpdateDSPEffectsBuffer();
            }
        }

        #endregion
    }
}