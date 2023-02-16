using System;
using Unity.Entities;

using NaughtyAttributes;

namespace PlaneWaver
{
    /// <summary>
    //  Emitter class for building and spawning bursts of audio grains.
    /// <summary>
    public class BurstAuthoring : EmitterAuthoring
    {
        [AllowNesting]
        [HorizontalLine(color: EColor.White)]
        public BurstParameters _Properties;

        #region EMBOLDENED BURST COMPONENT INIT

        public override void SetEntityType()
        {
            _EmitterType = EmitterType.Burst;
            _EntityType = SynthEntityType.Emitter;
            
            _Properties._Volume.SetModulationInput(_ModulationInputs[0]);
            _Properties._Playhead.SetModulationInput(_ModulationInputs[1]);
            _Properties._BurstDuration.SetModulationInput(_ModulationInputs[2]);
            _Properties._GrainDuration.SetModulationInput(_ModulationInputs[3]);
            _Properties._Density.SetModulationInput(_ModulationInputs[4]);
            _Properties._Transpose.SetModulationInput(_ModulationInputs[5]);

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

                _BurstDuration = new ModulationComponent
                {
                    _StartValue = _Properties._BurstDuration._Default * _SamplesPerMS,
                    _InteractionAmount = _Properties._BurstDuration._ModulationAmount * _SamplesPerMS,
                    _Shape = _Properties._BurstDuration._ModulationExponent,
                    _Noise = _Properties._BurstDuration._Noise._Amount,
                    _LockNoise = _Properties._BurstDuration._Noise._HoldForBurstDuration,
                    _Min = _Properties._BurstDuration._Min * _SamplesPerMS,
                    _Max = _Properties._BurstDuration._Max * _SamplesPerMS,
                    _LockStartValue = false,
                    _LockEndValue = false
                },
                _Playhead = new ModulationComponent
                {
                    _StartValue = _Properties._Playhead._Start,
                    _EndValue = _Properties._Playhead._End,
                    _InteractionAmount = _Properties._Playhead._ModulationAmount,
                    _Shape = _Properties._Playhead._ModulationExponent,
                    _Noise = _Properties._Playhead._Noise._Amount,
                    _LockNoise = _Properties._Playhead._Noise._HoldForBurstDuration,
                    _Min = _Properties._Playhead._Min,
                    _Max = _Properties._Playhead._Max,
                    _LockStartValue = _Properties._Playhead._StartIgnoresModulation,
                    _LockEndValue = false
                },
                _Density = new ModulationComponent
                {
                    _StartValue = _Properties._Density._Start,
                    _EndValue = _Properties._Density._End,
                    _InteractionAmount = _Properties._Density._ModulationAmount,
                    _Shape = _Properties._Density._ModulationExponent,
                    _Noise = _Properties._Density._Noise._Amount,
                    _LockNoise = _Properties._Density._Noise._HoldForBurstDuration,
                    _Min = _Properties._Density._Min,
                    _Max = _Properties._Density._Max,
                    _LockStartValue = false,
                    _LockEndValue = false
                },
                _GrainDuration = new ModulationComponent
                {
                    _StartValue = _Properties._GrainDuration._Start * _SamplesPerMS,
                    _EndValue = _Properties._GrainDuration._End * _SamplesPerMS,
                    _InteractionAmount = _Properties._GrainDuration._ModulationAmount * _SamplesPerMS,
                    _Shape = _Properties._GrainDuration._ModulationExponent,
                    _Noise = _Properties._GrainDuration._Noise._Amount,
                    _LockNoise = _Properties._GrainDuration._Noise._HoldForBurstDuration,
                    _Min = _Properties._GrainDuration._Min * _SamplesPerMS,
                    _Max = _Properties._GrainDuration._Max * _SamplesPerMS,
                    _LockStartValue = false,
                    _LockEndValue = false
                },
                _Transpose = new ModulationComponent
                {
                    _StartValue = _Properties._Transpose._Start,
                    _EndValue = _Properties._Transpose._End,
                    _InteractionAmount = _Properties._Transpose._ModulationAmount,
                    _Shape = _Properties._Transpose._ModulationExponent,
                    _Noise = _Properties._Transpose._Noise._Amount,
                    _LockNoise = _Properties._Transpose._Noise._HoldForBurstDuration,
                    _Min = _Properties._Transpose._Min,
                    _Max = _Properties._Transpose._Max,
                    _LockStartValue = false,
                    _LockEndValue = _Properties._Transpose._EndIgnoresModulation
                },
                _Volume = new ModulationComponent
                {
                    _StartValue = _Properties._Volume._Start,
                    _EndValue = _Properties._Volume._End,
                    _Shape = _Properties._Volume._ModulationExponent,
                    _InteractionAmount = _Properties._Volume._ModulationAmount,
                    _Noise = _Properties._Volume._Noise._Amount,
                    _LockNoise = _Properties._Volume._Noise._HoldForBurstDuration,
                    _Min = _Properties._Volume._Min,
                    _Max = _Properties._Volume._Max,
                    _LockStartValue = false,
                    _LockEndValue = _Properties._Volume._EndIgnoresModulation
                }
            });

            _EntityManager.AddBuffer<DSPParametersElement>(_Entity);
            DynamicBuffer<DSPParametersElement> dspParams = _EntityManager.GetBuffer<DSPParametersElement>(_Entity);

            for (int i = 0; i < _DSPChainParams.Length; i++)
                dspParams.Add(_DSPChainParams[i].GetDSPBufferElement());

            _EntityManager.AddComponentData(_Entity, new QuadEntityType { _Type = QuadEntityType.QuadEntityTypeEnum.Emitter });
        }

        #endregion

        #region EMBOLDENED BURST COMPONENT UPDATE

        public override void ProcessComponents()
        {
            UpdateEntityTags();

            if (_IsPlaying)
            {
                UpdateModulationValues();
                BurstComponent burstData = _EntityManager.GetComponentData<BurstComponent>(_Entity);

                burstData._IsPlaying = true;
                burstData._AudioClipIndex = _AudioAsset.ClipEntityIndex;
                burstData._SpeakerIndex = Host.AttachedSpeakerIndex;
                burstData._HostIndex = Host.EntityIndex;
                burstData._PingPong = _PingPongGrainPlayheads;
                burstData._VolumeAdjust = _VolumeAdjust;
                burstData._DistanceAmplitude = DistanceAmplitude;
                burstData._OutputSampleRate = _SampleRate;

                burstData._BurstDuration = new ModulationComponent
                {
                    _StartValue = _Properties._BurstDuration._Default * _SamplesPerMS,
                    _InteractionAmount = _Properties._BurstDuration._ModulationAmount * _SamplesPerMS,
                    _Shape = _Properties._BurstDuration._ModulationExponent,
                    _Noise = _Properties._BurstDuration._Noise._Amount,
                    _LockNoise = _Properties._BurstDuration._Noise._HoldForBurstDuration,
                    _Min = _Properties._BurstDuration._Min * _SamplesPerMS,
                    _Max = _Properties._BurstDuration._Max * _SamplesPerMS,
                    _LockStartValue = false,
                    _LockEndValue = false,
                    _InteractionInput = _Properties._BurstDuration.GetValue()
                };
                burstData._Playhead = new ModulationComponent
                {
                    _StartValue = _Properties._Playhead._Start,
                    _EndValue = _Properties._Playhead._End,
                    _InteractionAmount = _Properties._Playhead._ModulationAmount,
                    _Shape = _Properties._Playhead._ModulationExponent,
                    _Noise = _Properties._Playhead._Noise._Amount,
                    _LockNoise = _Properties._Playhead._Noise._HoldForBurstDuration,
                    _Min = _Properties._Playhead._Min,
                    _Max = _Properties._Playhead._Max,
                    _LockStartValue = _Properties._Playhead._StartIgnoresModulation,
                    _LockEndValue = false,
                    _InteractionInput = _Properties._Playhead.GetValue()
                };
                burstData._Density = new ModulationComponent
                {
                    _StartValue = _Properties._Density._Start,
                    _EndValue = _Properties._Density._End,
                    _InteractionAmount = _Properties._Density._ModulationAmount,
                    _Shape = _Properties._Density._ModulationExponent,
                    _Noise = _Properties._Density._Noise._Amount,
                    _LockNoise = _Properties._Density._Noise._HoldForBurstDuration,
                    _Min = _Properties._Density._Min,
                    _Max = _Properties._Density._Max,
                    _LockStartValue = false,
                    _LockEndValue = false,
                    _InteractionInput = _Properties._Density.GetValue()
                };
                burstData._GrainDuration = new ModulationComponent
                {
                    _StartValue = _Properties._GrainDuration._Start * _SamplesPerMS,
                    _EndValue = _Properties._GrainDuration._End * _SamplesPerMS,
                    _InteractionAmount = _Properties._GrainDuration._ModulationAmount * _SamplesPerMS,
                    _Shape = _Properties._GrainDuration._ModulationExponent,
                    _Noise = _Properties._GrainDuration._Noise._Amount,
                    _LockNoise = _Properties._GrainDuration._Noise._HoldForBurstDuration,
                    _Min = _Properties._GrainDuration._Min * _SamplesPerMS,
                    _Max = _Properties._GrainDuration._Max * _SamplesPerMS,
                    _LockStartValue = false,
                    _LockEndValue = false,
                    _InteractionInput = _Properties._GrainDuration.GetValue()
                };
                burstData._Transpose = new ModulationComponent
                {
                    _StartValue = _Properties._Transpose._Start,
                    _EndValue = _Properties._Transpose._End,
                    _InteractionAmount = _Properties._Transpose._ModulationAmount,
                    _Shape = _Properties._Transpose._ModulationExponent,
                    _Noise = _Properties._Transpose._Noise._Amount,
                    _LockNoise = _Properties._Transpose._Noise._HoldForBurstDuration,
                    _Min = _Properties._Transpose._Min,
                    _Max = _Properties._Transpose._Max,
                    _LockStartValue = false,
                    _LockEndValue = _Properties._Transpose._EndIgnoresModulation,
                    _InteractionInput = _Properties._Transpose.GetValue()
                };
                burstData._Volume = new ModulationComponent
                {
                    _StartValue = _Properties._Volume._Start,
                    _EndValue = _Properties._Volume._End,
                    _InteractionAmount = _Properties._Volume._ModulationAmount,
                    _Shape = _Properties._Volume._ModulationExponent,
                    _Noise = _Properties._Volume._Noise._Amount,
                    _LockNoise = _Properties._Volume._Noise._HoldForBurstDuration,
                    _Min = _Properties._Volume._Min,
                    _Max = _Properties._Volume._Max,
                    _LockStartValue = false,
                    _LockEndValue = _Properties._Volume._EndIgnoresModulation,
                    _InteractionInput = _Properties._Volume.GetValue() * _ContactSurfaceAttenuation
                };
                _EntityManager.SetComponentData(_Entity, burstData);

                UpdateDSPEffectsBuffer();
                // Burst emitters generate their entire output in one pass, so switching off
                _IsPlaying = false;
            }
        }

        #endregion
    }

    #region BURST PARAMETERS

    [Serializable]
    public class BurstParameters
    {
        [AllowNesting]
        [HorizontalLine(color: EColor.Gray)]
        public BurstVolume _Volume;
        [AllowNesting]
        [HorizontalLine(color: EColor.Gray)]
        public BurstPlayhead _Playhead;
        [AllowNesting]
        [HorizontalLine(color: EColor.Gray)]
        public BurstDuration _BurstDuration;
        [AllowNesting]
        [HorizontalLine(color: EColor.Gray)]
        public BurstGrainDuration _GrainDuration;
        [AllowNesting]
        [HorizontalLine(color: EColor.Gray)]
        public BurstDensity _Density;
        [AllowNesting]
        [HorizontalLine(color: EColor.Gray)]
        public BurstTranspose _Transpose;
    }

    #endregion
}
