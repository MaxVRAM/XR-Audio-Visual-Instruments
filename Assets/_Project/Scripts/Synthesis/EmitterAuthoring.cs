using Unity.Entities;
using UnityEngine;
using Random = UnityEngine.Random;

using MaxVRAM.Audio.Utils;
using PlaneWaver.Entities;
using PlaneWaver.Resources;
using PlaneWaver.Interaction;

namespace PlaneWaver.Synthesis
{
    /// <summary>
    //  Abstract class for managing emitter entities
    /// <summary>
    public abstract class EmitterAuthoring : SynthEntity
    {
        #region FIELDS & PROPERTIES

        public enum Condition { Always, Colliding, NotColliding };
        public enum EmitterType { Continuous, Burst }

        protected float[] _PerlinSeedArray;

        [Header("Runtime Dynamics")]
        public bool _IsPlaying = true;
        public float _TimeExisted = 0;
        public float _AdjustedDistance = 0;
        public float _DistanceAmplitude = 0;
        [SerializeField] protected float _ContactSurfaceAttenuation = 1;
        [SerializeField] protected float _LastTriggeredAt = 0;
        [SerializeField] protected int _LastSampleIndex = 0;

        [Header("Emitter Configuration")]
        [Tooltip("(generated) Host component managing this emitter.")]
        public HostAuthoring _Host;
        [Tooltip("(generated) This emitter's subtype. Current subtypes are either 'Continuous' or 'Burst'.")]
        public EmitterType _EmitterType;
        [Tooltip("Limit the emitter's playback to collision/contact states.")]
        public Condition _PlaybackCondition = Condition.Always;
        [Tooltip("Audio clip used as the emitter's content source.")]
        public int _ClipIndex = 0;
        [Tooltip("Audio clip used as the emitter's content source.")]
        public AudioAsset _AudioAsset;

        [Header("Playback Config")]
        [Tooltip("Scaling factor applied to the global listener radius value. The result defines the emitter's distance-volume attenuation.")]
        [Range(0.001f, 1f)] public float _DistanceAttenuationFactor = 1f;
        [Tooltip("Normalised age to begin fadeout of spawned emitter if a DestroyTimer component is attached.")]
        [Range(0, 1)] public float _AgeFadeout = .9f;  // TODO - not implemented yet
        [Tooltip("Reverses the playhead of an individual grain if it reaches the end of the clip during playback instead of outputting 0s.")]
        public bool _PingPongGrainPlayheads = true;
        [Tooltip("Multiplies the emitter's output by the rigidity value of the colliding surface.")]
        public bool _ColliderRigidityVolumeScale = false;
        public DSP_Class[] _DSPChainParams;
        protected int _SampleRate;
        protected float _SamplesPerMS = 0;

        #endregion

        #region ENTITY-SPECIFIC START CALL

        void Start()
        {
            _SampleRate = AudioSettings.outputSampleRate;
            _SamplesPerMS = _SampleRate * 0.001f;

            _PerlinSeedArray = new float[10];
            for (int i = 0; i < _PerlinSeedArray.Length; i++)
            {
                float offset = Random.Range(0, 1000);
                _PerlinSeedArray[i] = Mathf.PerlinNoise(offset, offset * 0.5f);
            }

            _EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            SetIndex(GrainSynth.Instance.RegisterEmitter(this));
            UpdateContactStatus(null);
        }

        #endregion

        #region ERRORNOUS EMITTER COMPONENT BUSINESS

        public void UpdateEntityTags()
        {
            _TimeExisted += Time.deltaTime;

            if (_Host.InListenerRadius != _EntityManager.HasComponent<InListenerRadiusTag>(_Entity))
            {
                if (_Host.InListenerRadius)
                    _EntityManager.AddComponent<InListenerRadiusTag>(_Entity);
                else
                    _EntityManager.RemoveComponent<InListenerRadiusTag>(_Entity);
            }

            if (_Host.IsConnected != _EntityManager.HasComponent<ConnectedTag>(_Entity))
            {
                if (_Host.IsConnected)
                    _EntityManager.AddComponent<ConnectedTag>(_Entity);
                else
                    _EntityManager.RemoveComponent<ConnectedTag>(_Entity);
            }

            if (_IsPlaying != _EntityManager.HasComponent<PlayingTag>(_Entity))
            {
                if (_IsPlaying)
                    _EntityManager.AddComponent<PlayingTag>(_Entity);
                else
                    _EntityManager.RemoveComponent<PlayingTag>(_Entity);
            }
        }

        protected void UpdateDSPEffectsBuffer(bool clear = true)
        {
            //--- TODO not sure if clearing and adding again is the best way to do this.
            DynamicBuffer<DSPParametersElement> dspBuffer = _EntityManager.GetBuffer<DSPParametersElement>(_Entity);
            if (clear) dspBuffer.Clear();
            for (int i = 0; i < _DSPChainParams.Length; i++)
                dspBuffer.Add(_DSPChainParams[i].GetDSPBufferElement());
        }

        public override void Deregister()
        {
            GrainSynth.Instance.DeregisterEmitter(this);
        }

        #endregion

        #region EMITTER CONTACT PROCESSING

        public void UpdateDistanceAmplitude(float distance, float speakerFactor)
        {
            _AdjustedDistance = distance / _DistanceAttenuationFactor;
            _DistanceAmplitude = ScaleAmplitude.ListenerDistanceVolume(_AdjustedDistance) * speakerFactor;
        }

        public void NewCollision(Collision collision)
        {
            if (_EmitterType == EmitterType.Burst && _PlaybackCondition != Condition.NotColliding)
            {
                if (Time.fixedTime < _LastTriggeredAt + GrainSynth.Instance._BurstDebounceDurationMS * 0.001f)
                    return;
                if (ColliderMoreRigid(collision.collider, _Host.SurfaceRigidity, out float otherRigidity) && OnlyTriggerMostRigid)
                    return;

                _ContactSurfaceAttenuation = _ColliderRigidityVolumeScale ? (_Host.SurfaceRigidity + otherRigidity) / 2 : 1;
                _LastTriggeredAt = Time.fixedTime;
                _IsPlaying = true;
            }
            else UpdateContactStatus(collision);
        }

        public void UpdateContactStatus(Collision collision)
        {
            if (_EmitterType != EmitterType.Continuous)
                return;

            if (_PlaybackCondition == Condition.NotColliding || collision == null)
            {
                _ContactSurfaceAttenuation = 1;
                _IsPlaying = _PlaybackCondition == Condition.NotColliding ? collision == null : false;
                return;
            }

            if (ColliderMoreRigid(collision.collider, _Host._CurrentCollidingRigidity, out float otherRigidity) && OnlyTriggerMostRigid)
            {
                _IsPlaying = false;
            }
            else
            {
                _ContactSurfaceAttenuation = _ColliderRigidityVolumeScale ? (_Host._CurrentCollidingRigidity + otherRigidity) / 2 : 0;
                _IsPlaying = true;
            }
        }

        protected bool OnlyTriggerMostRigid { get { return GrainSynth.Instance._OnlyTriggerMostRigidSurface; } }

        public static bool ColliderMoreRigid(Collider collider, float rigidity, out float otherRigidity)
        {
            otherRigidity = collider.TryGetComponent(out SurfaceProperties otherSurface) ? otherSurface._Rigidity : 0.5f;
            return otherSurface != null && otherSurface.IsEmitter && otherSurface._Rigidity >= rigidity;
        }

        public float GeneratePerlinForParameter(int parameterIndex)
        {
            return Mathf.PerlinNoise(
                Time.time + _PerlinSeedArray[parameterIndex],
                (Time.time + _PerlinSeedArray[parameterIndex]) * 0.5f);
        }

        #endregion
    }
}
