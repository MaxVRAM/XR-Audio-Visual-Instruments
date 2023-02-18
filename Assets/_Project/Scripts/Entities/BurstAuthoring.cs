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
            public float Amount => _Influence * _Multiplier;
            [Range(-1f, 1f)] public float _Influence = 0f;
            public float _Multiplier = 0.1f;
            public bool _HoldForBurstDuration = false;
        }

        [HorizontalLine(color: EColor.Gray)]
        private readonly Vector2 _VolumePath = new(0f, 0f);
        private readonly Vector2 _VolumeRange = new(0f, 2f);
        private readonly bool _VolumeFixedStart = false;
        private readonly bool _VolumeFixedEnd = true;
        public ModulationStruct _VolumeModulation;
        public NoiseModule _VolumeNoise;

        [HorizontalLine(color: EColor.Gray)]
        [Range(10f, 1000f)] public float _LengthDefault = 200f;
        private readonly Vector2 _LengthRange = new(10f, 1000f);
        private readonly bool _LengthFixedStart = false;
        private readonly bool _LengthFixedEnd = false;
        public ModulationStruct _LengthModulation;
        public NoiseModule _LengthNoise;
        public float LengthDefault => _LengthDefault * _SamplesPerMS;
        public Vector2 LengthRange => new(_LengthRange.x * _SamplesPerMS, _LengthRange.y * _SamplesPerMS);

        [HorizontalLine(color: EColor.Gray)]
        [MinMaxSlider(0f, 1f)] public Vector2 _PlayheadPath = new (0f, 0.5f);
        private readonly Vector2 _PlayheadRange = new(0f, 1f);
        public bool _PlayheadFixedStart = true;
        public bool _PlayheadFixedEnd = false;
        public ModulationStruct _PlayheadModulation;
        public NoiseModule _PlayheadNoise;

        [HorizontalLine(color: EColor.Gray)]
        [MinMaxSlider(10f, 500f)] public Vector2 _DurationPath = new (80f, 120f);
        private readonly Vector2 _DurationRange = new(10f, 500f);
        public bool _DurationFixedStart = false;
        public bool _DurationFixedEnd = true;
        public ModulationStruct _DurationModulation;
        public NoiseModule _DurationNoise;
        public Vector2 DurationPath => new(_DurationPath.x * _SamplesPerMS, _DurationPath.y * _SamplesPerMS);
        public Vector2 DurationRange => new(_DurationRange.x * _SamplesPerMS, _DurationRange.y * _SamplesPerMS);

        [HorizontalLine(color: EColor.Gray)]
        [MinMaxSlider(1f, 10f)] public Vector2 _DensityPath = new (2f, 3f);
        private readonly Vector2 _DensityRange = new(1f, 10f);
        public bool _DensityFixedStart = false;
        public bool _DensityFixedEnd = false;
        public ModulationStruct _DensityModulation;
        public NoiseModule _DensityNoise;

        [HorizontalLine(color: EColor.Gray)]
        [MinMaxSlider(-3f, 3f)] public Vector2 _TransposePath = new(0f, 0f);
        private readonly Vector2 _TransposeRange = new(-3f, 3f);
        public bool _TransposeFixedStart = false;
        public bool _TransposeFixedEnd = false;
        public ModulationStruct _TransposeModulation;
        public NoiseModule _TransposeNoise;

        public override ModulationStruct[] GatherModulationInputs()
        {
            ModulationStruct[] modulationInputs = new ModulationStruct[6];
            modulationInputs[0] = _LengthModulation;
            modulationInputs[1] = _VolumeModulation;
            modulationInputs[2] = _PlayheadModulation;
            modulationInputs[3] = _DurationModulation;
            modulationInputs[4] = _DensityModulation;
            modulationInputs[5] = _TransposeModulation;
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

                _Volume = new ModulationComponent
                {
                    _StartValue = _VolumePath.x,
                    _EndValue = _VolumePath.y,
                    _Modulation = _VolumeModulation.Amount,
                    _Exponent = _VolumeModulation.Exponent,
                    _Noise = _VolumeNoise.Amount,
                    _LockNoise = _VolumeNoise._HoldForBurstDuration,
                    _Min = _VolumeRange.x,
                    _Max = _VolumeRange.y,
                    _LockStartValue = _VolumeFixedStart,
                    _LockEndValue = _VolumeFixedEnd
                },
                _Length = new ModulationComponent
                {
                    _StartValue = LengthDefault,
                    _Modulation = _LengthModulation.Amount,
                    _Exponent = _LengthModulation.Exponent,
                    _Noise = _LengthNoise.Amount,
                    _LockNoise = true,
                    _Min = LengthRange.x,
                    _Max = LengthRange.y,
                    _LockStartValue = _LengthFixedStart,
                    _LockEndValue = _LengthFixedStart
                },
                _Playhead = new ModulationComponent
                {
                    _StartValue = _PlayheadPath.x,
                    _EndValue = _PlayheadPath.y,
                    _Modulation = _PlayheadModulation.Amount,
                    _Exponent = _PlayheadModulation.Exponent,
                    _Noise = _PlayheadNoise.Amount,
                    _LockNoise = _PlayheadNoise._HoldForBurstDuration,
                    _Min = _PlayheadRange.x,
                    _Max = _PlayheadRange.y,
                    _LockStartValue = _PlayheadFixedStart,
                    _LockEndValue = _PlayheadFixedEnd
                },
                _Duration = new ModulationComponent
                {
                    _StartValue = DurationPath.x,
                    _EndValue = DurationPath.y,
                    _Modulation = _DurationModulation.Amount,
                    _Exponent = _DurationModulation.Exponent,
                    _Noise = _DurationNoise.Amount,
                    _LockNoise = _DurationNoise._HoldForBurstDuration,
                    _Min = DurationRange.x,
                    _Max = DurationRange.y,
                    _LockStartValue = _DurationFixedStart,
                    _LockEndValue = _DurationFixedEnd
                },
                _Density = new ModulationComponent
                {
                    _StartValue = _DensityPath.x,
                    _EndValue = _DensityPath.y,
                    _Modulation = _DensityModulation.Amount,
                    _Exponent = _DensityModulation.Exponent,
                    _Noise = _DensityNoise.Amount,
                    _Min = _DensityRange.x,
                    _Max = _DensityRange.y,
                    _LockStartValue = _DensityFixedStart,
                    _LockEndValue = _DensityFixedEnd
                },
                _Transpose = new ModulationComponent
                {
                    _StartValue = _TransposePath.x,
                    _EndValue = _TransposePath.y,
                    _Modulation = _TransposeModulation.Amount,
                    _Exponent = _TransposeModulation.Exponent,
                    _Noise = _TransposeNoise.Amount,
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
                BurstComponent entity = _EntityManager.GetComponentData<BurstComponent>(_Entity);

                entity._IsPlaying = true;
                entity._AudioClipIndex = _AudioAsset.ClipEntityIndex;
                entity._SpeakerIndex = Host.AttachedSpeakerIndex;
                entity._HostIndex = Host.EntityIndex;
                entity._PingPong = _PingPongGrainPlayheads;
                entity._VolumeAdjust = _VolumeAdjust;
                entity._DistanceAmplitude = DistanceAmplitude;

                entity._Volume = new ModulationComponent
                {
                    _StartValue = _VolumePath.x * _ColliderRigidityVolume,
                    _EndValue = _VolumePath.y * _ColliderRigidityVolume,
                    _Modulation = _VolumeModulation.Amount,
                    _Exponent = _VolumeModulation.Exponent,
                    _Noise = _VolumeNoise.Amount,
                    _LockNoise = _VolumeNoise._HoldForBurstDuration,
                    _Min = _VolumeRange.x,
                    _Max = _VolumeRange.y,
                    _LockStartValue = _VolumeFixedStart,
                    _LockEndValue = _VolumeFixedEnd,
                    _Input = _VolumeModulation.GetProcessedValue()
                };
                entity._Length = new ModulationComponent
                {
                    _StartValue = LengthDefault,
                    _Modulation = _LengthModulation.Amount,
                    _Exponent = _LengthModulation.Exponent,
                    _Noise = _LengthNoise.Amount,
                    _LockNoise = true,
                    _Min = LengthRange.x,
                    _Max = LengthRange.y,
                    _LockStartValue = _LengthFixedStart,
                    _LockEndValue = _LengthFixedEnd,
                    _Input = _LengthModulation.GetProcessedValue()
                };
                entity._Playhead = new ModulationComponent
                {
                    _StartValue = _PlayheadPath.x,
                    _EndValue = _PlayheadPath.y,
                    _Modulation = _PlayheadModulation.Amount,
                    _Exponent = _PlayheadModulation.Exponent,
                    _Noise = _PlayheadNoise.Amount,
                    _LockNoise = _PlayheadNoise._HoldForBurstDuration,
                    _Min = _PlayheadRange.x,
                    _Max = _PlayheadRange.y,
                    _LockStartValue = _PlayheadFixedStart,
                    _LockEndValue = _PlayheadFixedEnd,
                    _Input = _PlayheadModulation.GetProcessedValue()
                };
                entity._Duration = new ModulationComponent
                {
                    _StartValue = DurationPath.x,
                    _EndValue = DurationPath.y,
                    _Modulation = _DurationModulation.Amount,
                    _Exponent = _DurationModulation.Exponent,
                    _Noise = _DurationNoise.Amount,
                    _LockNoise = _DurationNoise._HoldForBurstDuration,
                    _Min = DurationRange.x,
                    _Max = DurationRange.y,
                    _LockStartValue = _DurationFixedStart,
                    _LockEndValue = _DurationFixedEnd,
                    _Input = _DurationModulation.GetProcessedValue()
                };
                entity._Density = new ModulationComponent
                {
                    _StartValue = _DensityPath.x,
                    _EndValue = _DensityPath.y,
                    _Modulation = _DensityModulation.Amount,
                    _Exponent = _DensityModulation.Exponent,
                    _Noise = _DensityNoise.Amount,
                    _LockNoise = _DensityNoise._HoldForBurstDuration,
                    _Min = _DensityRange.x,
                    _Max = _DensityRange.y,
                    _LockStartValue = _DensityFixedStart,
                    _LockEndValue = _DensityFixedEnd,
                    _Input = _DensityModulation.GetProcessedValue()
                };
                entity._Transpose = new ModulationComponent
                {
                    _StartValue = _TransposePath.x,
                    _EndValue = _TransposePath.y,
                    _Modulation = _TransposeModulation.Amount,
                    _Exponent = _TransposeModulation.Exponent,
                    _Noise = _TransposeNoise.Amount,
                    _LockNoise = _TransposeNoise._HoldForBurstDuration,
                    _Min = _TransposeRange.x,
                    _Max = _TransposeRange.y,
                    _LockStartValue = _TransposeFixedStart,
                    _LockEndValue = _TransposeFixedEnd,
                    _Input = _TransposeModulation.GetProcessedValue()
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
