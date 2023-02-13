using System;
using Unity.Entities;

using PlaneWaver.Entities;
using PlaneWaver.Modulation;

namespace PlaneWaver.Synthesis
{
    /// <summary>
    //  Emitter class for building and spawning bursts of audio grains.
    /// <summary>
    public class BurstAuthoring : EmitterAuthoring
    {
        public BurstParameters _Properties;

        #region EMBOLDENED BURST COMPONENT INIT

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
                _SpeakerIndex = _Host._AttachedSpeakerIndex,
                _HostIndex = _Host.EntityIndex,
                _DistanceAmplitude = 1,
                _PingPong = _PingPongGrainPlayheads,
                _OutputSampleRate = _SampleRate,

                _BurstDuration = new ModulationComponent
                {
                    _StartValue = _Properties._BurstDuration._Default * _SamplesPerMS,
                    _InteractionAmount = _Properties._BurstDuration._InteractionAmount * _SamplesPerMS,
                    _Shape = _Properties._BurstDuration._InteractionShape,
                    _Noise = _Properties._BurstDuration._Noise._Amount,
                    _LockNoise = _Properties._BurstDuration._Noise._FreezeOnTrigger,
                    _Min = _Properties._BurstDuration._Min * _SamplesPerMS,
                    _Max = _Properties._BurstDuration._Max * _SamplesPerMS,
                    _LockStartValue = false,
                    _LockEndValue = false
                },
                _Playhead = new ModulationComponent
                {
                    _StartValue = _Properties._Playhead._Start,
                    _EndValue = _Properties._Playhead._End,
                    _InteractionAmount = _Properties._Playhead._InteractionAmount,
                    _Shape = _Properties._Playhead._InteractionShape,
                    _Noise = _Properties._Playhead._Noise._Amount,
                    _LockNoise = _Properties._Playhead._Noise._FreezeOnTrigger,
                    _Min = _Properties._Playhead._Min,
                    _Max = _Properties._Playhead._Max,
                    _LockStartValue = _Properties._Playhead._LockStartValue,
                    _LockEndValue = false
                },
                _Density = new ModulationComponent
                {
                    _StartValue = _Properties._Density._Start,
                    _EndValue = _Properties._Density._End,
                    _InteractionAmount = _Properties._Density._InteractionAmount,
                    _Shape = _Properties._Density._InteractionShape,
                    _Noise = _Properties._Density._Noise._Amount,
                    _LockNoise = _Properties._Density._Noise._FreezeOnTrigger,
                    _Min = _Properties._Density._Min,
                    _Max = _Properties._Density._Max,
                    _LockStartValue = false,
                    _LockEndValue = false
                },
                _GrainDuration = new ModulationComponent
                {
                    _StartValue = _Properties._GrainDuration._Start * _SamplesPerMS,
                    _EndValue = _Properties._GrainDuration._End * _SamplesPerMS,
                    _InteractionAmount = _Properties._GrainDuration._InteractionAmount * _SamplesPerMS,
                    _Shape = _Properties._GrainDuration._InteractionShape,
                    _Noise = _Properties._GrainDuration._Noise._Amount,
                    _LockNoise = _Properties._GrainDuration._Noise._FreezeOnTrigger,
                    _Min = _Properties._GrainDuration._Min * _SamplesPerMS,
                    _Max = _Properties._GrainDuration._Max * _SamplesPerMS,
                    _LockStartValue = false,
                    _LockEndValue = false
                },
                _Transpose = new ModulationComponent
                {
                    _StartValue = _Properties._Transpose._Start,
                    _EndValue = _Properties._Transpose._End,
                    _InteractionAmount = _Properties._Transpose._InteractionAmount,
                    _Shape = _Properties._Transpose._InteractionShape,
                    _Noise = _Properties._Transpose._Noise._Amount,
                    _LockNoise = _Properties._Transpose._Noise._FreezeOnTrigger,
                    _Min = _Properties._Transpose._Min,
                    _Max = _Properties._Transpose._Max,
                    _LockStartValue = false,
                    _LockEndValue = _Properties._Transpose._LockEndValue
                },
                _Volume = new ModulationComponent
                {
                    _StartValue = _Properties._Volume._Start,
                    _EndValue = _Properties._Volume._End,
                    _Shape = _Properties._Volume._InteractionShape,
                    _InteractionAmount = _Properties._Volume._InteractionAmount,
                    _Noise = _Properties._Volume._Noise._Amount,
                    _LockNoise = _Properties._Volume._Noise._FreezeOnTrigger,
                    _Min = _Properties._Volume._Min,
                    _Max = _Properties._Volume._Max,
                    _LockStartValue = false,
                    _LockEndValue = _Properties._Volume._LockEndValue
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
                BurstComponent burstData = _EntityManager.GetComponentData<BurstComponent>(_Entity);

                burstData._IsPlaying = true;
                burstData._AudioClipIndex = _AudioAsset.ClipEntityIndex;
                burstData._SpeakerIndex = _Host._AttachedSpeakerIndex;
                burstData._HostIndex = _Host.EntityIndex;
                burstData._PingPong = _PingPongGrainPlayheads;
                burstData._DistanceAmplitude = _DistanceAmplitude;
                burstData._OutputSampleRate = _SampleRate;

                burstData._BurstDuration = new ModulationComponent
                {
                    _StartValue = _Properties._BurstDuration._Default * _SamplesPerMS,
                    _InteractionAmount = _Properties._BurstDuration._InteractionAmount * _SamplesPerMS,
                    _Shape = _Properties._BurstDuration._InteractionShape,
                    _Noise = _Properties._BurstDuration._Noise._Amount,
                    _LockNoise = _Properties._BurstDuration._Noise._FreezeOnTrigger,
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
                    _InteractionAmount = _Properties._Playhead._InteractionAmount,
                    _Shape = _Properties._Playhead._InteractionShape,
                    _Noise = _Properties._Playhead._Noise._Amount,
                    _LockNoise = _Properties._Playhead._Noise._FreezeOnTrigger,
                    _Min = _Properties._Playhead._Min,
                    _Max = _Properties._Playhead._Max,
                    _LockStartValue = _Properties._Playhead._LockStartValue,
                    _LockEndValue = false,
                    _InteractionInput = _Properties._Playhead.GetValue()
                };
                burstData._Density = new ModulationComponent
                {
                    _StartValue = _Properties._Density._Start,
                    _EndValue = _Properties._Density._End,
                    _InteractionAmount = _Properties._Density._InteractionAmount,
                    _Shape = _Properties._Density._InteractionShape,
                    _Noise = _Properties._Density._Noise._Amount,
                    _LockNoise = _Properties._Density._Noise._FreezeOnTrigger,
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
                    _InteractionAmount = _Properties._GrainDuration._InteractionAmount * _SamplesPerMS,
                    _Shape = _Properties._GrainDuration._InteractionShape,
                    _Noise = _Properties._GrainDuration._Noise._Amount,
                    _LockNoise = _Properties._GrainDuration._Noise._FreezeOnTrigger,
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
                    _InteractionAmount = _Properties._Transpose._InteractionAmount,
                    _Shape = _Properties._Transpose._InteractionShape,
                    _Noise = _Properties._Transpose._Noise._Amount,
                    _LockNoise = _Properties._Transpose._Noise._FreezeOnTrigger,
                    _Min = _Properties._Transpose._Min,
                    _Max = _Properties._Transpose._Max,
                    _LockStartValue = false,
                    _LockEndValue = _Properties._Transpose._LockEndValue,
                    _InteractionInput = _Properties._Transpose.GetValue()
                };
                burstData._Volume = new ModulationComponent
                {
                    _StartValue = _Properties._Volume._Start,
                    _EndValue = _Properties._Volume._End,
                    _InteractionAmount = _Properties._Volume._InteractionAmount,
                    _Shape = _Properties._Volume._InteractionShape,
                    _Noise = _Properties._Volume._Noise._Amount,
                    _LockNoise = _Properties._Volume._Noise._FreezeOnTrigger,
                    _Min = _Properties._Volume._Min,
                    _Max = _Properties._Volume._Max,
                    _LockStartValue = false,
                    _LockEndValue = _Properties._Volume._LockEndValue,
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
}

namespace PlaneWaver.Modulation
{
    #region BURST PARAMETERS

    [Serializable]
    public class BurstParameters
    {
        public BurstPlayhead _Playhead;
        public BurstDuration _BurstDuration;
        public BurstDensity _Density;
        public BurstGrainDuration _GrainDuration;
        public BurstTranspose _Transpose;
        public BurstVolume _Volume;
    }

    #endregion
}
