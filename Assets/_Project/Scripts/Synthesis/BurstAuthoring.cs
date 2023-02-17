using System;
using Unity.Entities;

using NaughtyAttributes;
using UnityEngine;

namespace PlaneWaver
{
    /// <summary>
    //  Emitter class for building and spawning bursts of audio grains.
    /// <summary>
    public class BurstAuthoring : EmitterAuthoring
    {
        #region MODULATION PARAMETERS

        [Serializable]
        public class NoiseModule
        {
            public float Influence => _Influence * _Multiplier;
            [Range(-1f, 1f)] public float _Influence = 0f;
            public float _Multiplier = 0.1f;
            public bool _HoldForBurstDuration = false;
        }

        Vector2 _LengthRange = new(10f, 1000f);
        Vector2 _VolumeRange = new(0f, 2f);
        Vector2 _PlayheadRange = new(0f, 1f);
        Vector2 _DurationRange = new(10f, 500f);
        Vector2 _DensityRange = new(1f, 10f);
        Vector2 _TransposeRange = new(-3f, 3f);

        [AllowNesting]
        [HorizontalLine(color: EColor.Gray)]
        [Range(10f, 1000f)] public float _LengthDefault = 200f;
        [Range(-1, 1f)] public float _LengthModulation = 0f;
        private bool _LengthFixedStart = false;
        private bool _LengthFixedEnd = false;
        public ModulationInput _LengthInput;
        public NoiseModule _LengthNoise;
        [AllowNesting]
        [HorizontalLine(color: EColor.Gray)]
        private Vector2 _VolumePath = new(0f, 0f);
        [Range(0f, 1f)] public float _VolumeModulation = 0.5f;
        private bool _VolumeFixedStart = false;
        private bool _VolumeFixedEnd = true;
        public ModulationInput _VolumeInput;
        public NoiseModule _VolumeNoise;
        [AllowNesting]
        [HorizontalLine(color: EColor.Gray)]
        [MinMaxSlider(0f, 1f)] public Vector2 _PlayheadPath = new (0f, 0.5f);
        public bool _PlayheadFixedStart = true;
        public bool _PlayheadFixedEnd = false;
        [Range(-1f, 1f)] public float _PlayheadModulation = 0f;
        public ModulationInput _PlayheadInput;
        public NoiseModule _PlayheadNoise;
        [AllowNesting]
        [HorizontalLine(color: EColor.Gray)]
        [MinMaxSlider(10f, 500f)] public Vector2 _DurationPath = new (80f, 120f);
        public bool _DurationFixedStart = false;
        public bool _DurationFixedEnd = true;
        [Range(-1f, 1f)] public float _DurationModulation = -0.1f;
        public ModulationInput _DurationInput;
        public NoiseModule _DurationNoise;
        [AllowNesting]
        [HorizontalLine(color: EColor.Gray)]
        [MinMaxSlider(1f, 10f)] public Vector2 _DensityPath = new (3f, 2f);
        [Range(-1f, 1f)] public float _DensityModulation = 0.3f;
        public bool _DensityFixedStart = false;
        public bool _DensityFixedEnd = false;
        public ModulationInput _DensityInput;
        public NoiseModule _DensityNoise;
        [AllowNesting]
        [HorizontalLine(color: EColor.Gray)]
        [MinMaxSlider(-3f, 3f)] public Vector2 _TransposePath = new(0f, 0f);
        public bool _TransposeFixedStart = false;
        public bool _TransposeFixedEnd = false;
        [Range(-1f, 1f)] public float _TransposeModulation = 0f;
        public ModulationInput _TransposeInput;
        public NoiseModule _TransposeNoise;

        public override ModulationInput[] GatherModulationInputs()
        {
            ModulationInput[] modulationInputs = new ModulationInput[6];
            modulationInputs[0] = _LengthInput;
            modulationInputs[1] = _VolumeInput;
            modulationInputs[2] = _PlayheadInput;
            modulationInputs[3] = _DurationInput;
            modulationInputs[4] = _DensityInput;
            modulationInputs[5] = _TransposeInput;
            return modulationInputs;
        }

        #endregion

        #region BANGIN BURST COMPONENT INIT

        public override void SetEntityType()
        {
            _EmitterType = EmitterType.Burst;
            _EntityType = SynthEntityType.Emitter;
            _Archetype = _EntityManager.CreateArchetype(typeof(BurstComponent));
            _IsPlaying = false;
        }

        public override void InitialiseComponents()
        {
            _EntityManager.AddComponentData(_Entity, new BurstComponent
            {
                _IsPlaying = false,
                _EmitterIndex = _EntityIndex,
                _AudioClipIndex = _AudioAsset.ClipEntityIndex,
                _SpeakerIndex = Host.AttachedSpeakerIndex,
                _HostIndex = Host.EntityIndex,
                _VolumeAdjust = _VolumeAdjust,
                _DistanceAmplitude = 1,
                _PingPong = _PingPongGrainPlayheads,
                _OutputSampleRate = _SampleRate,

                _Length = new ModulationComponent
                {
                    _StartValue = _LengthDefault * _SamplesPerMS,
                    _Modulation = _LengthModulation,
                    _Exponent = _LengthInput.Exponent,
                    _Noise = _LengthNoise.Influence,
                    _LockNoise = true,
                    _Min = _LengthRange.x * _SamplesPerMS,
                    _Max = _LengthRange.y * _SamplesPerMS,
                    _LockStartValue = _LengthFixedStart,
                    _LockEndValue = _LengthFixedStart
                },
                _Volume = new ModulationComponent
                {
                    _StartValue = _VolumePath.x,
                    _EndValue = _VolumePath.y,
                    _Modulation = _VolumeModulation,
                    _Exponent = _VolumeInput.Exponent,
                    _Noise = _VolumeNoise.Influence,
                    _LockNoise = _VolumeNoise._HoldForBurstDuration,
                    _Min = _VolumeRange.x,
                    _Max = _VolumeRange.y,
                    _LockStartValue = _VolumeFixedStart,
                    _LockEndValue = _VolumeFixedEnd
                },
                _Playhead = new ModulationComponent
                {
                    _StartValue = _PlayheadPath.x,
                    _EndValue = _PlayheadPath.y,
                    _Modulation = _PlayheadModulation,
                    _Exponent = _PlayheadInput.Exponent,
                    _Noise = _PlayheadNoise.Influence,
                    _LockNoise = _PlayheadNoise._HoldForBurstDuration,
                    _Min = _PlayheadRange.x,
                    _Max = _PlayheadRange.y,
                    _LockStartValue = _PlayheadFixedStart,
                    _LockEndValue = _PlayheadFixedEnd
                },
                _Duration = new ModulationComponent
                {
                    _StartValue = _DurationPath.x * _SamplesPerMS,
                    _EndValue = _DurationPath.y * _SamplesPerMS,
                    _Modulation = _DurationModulation,
                    _Exponent = _DurationInput.Exponent,
                    _Noise = _DurationNoise.Influence,
                    _LockNoise = _DurationNoise._HoldForBurstDuration,
                    _Min = _DurationRange.x * _SamplesPerMS,
                    _Max = _DurationRange.y * _SamplesPerMS,
                    _LockStartValue = _DurationFixedStart,
                    _LockEndValue = _DurationFixedEnd
                },
                _Density = new ModulationComponent
                {
                    _StartValue = _DensityPath.x,
                    _EndValue = _DensityPath.y,
                    _Modulation = _DensityModulation,
                    _Exponent = _DensityInput.Exponent,
                    _Noise = _DensityNoise.Influence,
                    _Min = _DensityRange.x,
                    _Max = _DensityRange.y,
                    _LockStartValue = _DensityFixedStart,
                    _LockEndValue = _DensityFixedEnd
                },
                _Transpose = new ModulationComponent
                {
                    _StartValue = _TransposePath.x,
                    _EndValue = _TransposePath.y,
                    _Modulation = _TransposeModulation,
                    _Exponent = _TransposeInput.Exponent,
                    _Noise = _TransposeNoise.Influence,
                    _Min = _TransposeRange.x,
                    _Max = _TransposeRange.y,
                    _LockStartValue = _TransposeFixedStart,
                    _LockEndValue = _TransposeFixedEnd
                }
            });

            _EntityManager.AddBuffer<DSPParametersElement>(_Entity);
            DynamicBuffer<DSPParametersElement> dspParams = _EntityManager.GetBuffer<DSPParametersElement>(_Entity);

            for (int i = 0; i < _DSPChainParams.Length; i++)
                dspParams.Add(_DSPChainParams[i].GetDSPBufferElement());

            _EntityManager.AddComponentData(_Entity, new QuadEntityType { _Type = QuadEntityType.QuadEntityTypeEnum.Emitter });
        }

        #endregion

        #region BUOYANT BURST COMPONENT UPDATE

        public override void ProcessComponents()
        {
            UpdateEntityTags();

            if (_IsPlaying)
            {
                UpdateModulationValues();
                BurstComponent entity = _EntityManager.GetComponentData<BurstComponent>(_Entity);

                entity._IsPlaying = true;
                entity._AudioClipIndex = _AudioAsset.ClipEntityIndex;
                entity._SpeakerIndex = Host.AttachedSpeakerIndex;
                entity._HostIndex = Host.EntityIndex;
                entity._PingPong = _PingPongGrainPlayheads;
                entity._VolumeAdjust = _VolumeAdjust;
                entity._DistanceAmplitude = DistanceAmplitude;
                entity._OutputSampleRate = _SampleRate;

                entity._Length = new ModulationComponent
                {
                    _StartValue = _LengthDefault * _SamplesPerMS,
                    _Modulation = _LengthModulation,
                    _Exponent = _LengthInput.Exponent,
                    _Noise = _LengthNoise.Influence,
                    _LockNoise = true,
                    _Min = _LengthRange.x * _SamplesPerMS,
                    _Max = _LengthRange.x * _SamplesPerMS,
                    _LockStartValue = _LengthFixedStart,
                    _LockEndValue = _LengthFixedEnd,
                    _Input = _LengthInput.Result
                };
                entity._Volume = new ModulationComponent
                {
                    _StartValue = _VolumePath.x,
                    _EndValue = _VolumePath.y,
                    _Modulation = _VolumeModulation,
                    _Exponent = _VolumeInput.Exponent,
                    _Noise = _VolumeNoise.Influence,
                    _LockNoise = _VolumeNoise._HoldForBurstDuration,
                    _Min = _VolumeRange.x,
                    _Max = _VolumeRange.y,
                    _LockStartValue = _VolumeFixedStart,
                    _LockEndValue = _VolumeFixedEnd,
                    _Input = _VolumeInput.Result * _ContactSurfaceAttenuation
                };
                entity._Playhead = new ModulationComponent
                {
                    _StartValue = _PlayheadPath.x,
                    _EndValue = _PlayheadPath.y,
                    _Modulation = _PlayheadModulation,
                    _Exponent = _PlayheadInput.Exponent,
                    _Noise = _PlayheadNoise.Influence,
                    _LockNoise = _PlayheadNoise._HoldForBurstDuration,
                    _Min = _PlayheadRange.x,
                    _Max = _PlayheadRange.y,
                    _LockStartValue = _PlayheadFixedStart,
                    _LockEndValue = _PlayheadFixedEnd,
                    _Input = _PlayheadInput.Result
                };
                entity._Duration = new ModulationComponent
                {
                    _StartValue = _DurationPath.x * _SamplesPerMS,
                    _EndValue = _DurationPath.y * _SamplesPerMS,
                    _Modulation = _DurationModulation,
                    _Exponent = _DurationInput.Exponent,
                    _Noise = _DurationNoise.Influence,
                    _LockNoise = _DurationNoise._HoldForBurstDuration,
                    _Min = _DurationRange.x * _SamplesPerMS,
                    _Max = _DurationRange.y * _SamplesPerMS,
                    _LockStartValue = _DurationFixedStart,
                    _LockEndValue = _DurationFixedEnd,
                    _Input = _DurationInput.Result
                };
                entity._Density = new ModulationComponent
                {
                    _StartValue = _DensityPath.x,
                    _EndValue = _DensityPath.y,
                    _Modulation = _DensityModulation,
                    _Exponent = _DensityInput.Exponent,
                    _Noise = _DensityNoise.Influence,
                    _LockNoise = _DensityNoise._HoldForBurstDuration,
                    _Min = _DensityRange.x,
                    _Max = _DensityRange.y,
                    _LockStartValue = _DensityFixedStart,
                    _LockEndValue = _DensityFixedEnd,
                    _Input = _DensityInput.Result
                };
                entity._Transpose = new ModulationComponent
                {
                    _StartValue = _TransposePath.x,
                    _EndValue = _TransposePath.y,
                    _Modulation = _TransposeModulation,
                    _Exponent = _TransposeInput.Exponent,
                    _Noise = _TransposeNoise.Influence,
                    _LockNoise = _TransposeNoise._HoldForBurstDuration,
                    _Min = _TransposeRange.x,
                    _Max = _TransposeRange.y,
                    _LockStartValue = _TransposeFixedStart,
                    _LockEndValue = _TransposeFixedEnd,
                    _Input = _TransposeInput.Result
                };
                _EntityManager.SetComponentData(_Entity, entity);

                UpdateDSPEffectsBuffer();
                // Burst emitters generate their entire output in one pass, so switching off
                _IsPlaying = false;
            }
        }

        #endregion
    }
}
