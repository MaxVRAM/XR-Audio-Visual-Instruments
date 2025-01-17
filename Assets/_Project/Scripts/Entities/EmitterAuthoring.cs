﻿using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Random = UnityEngine.Random;

using MaxVRAM.Audio;
using NaughtyAttributes;
using System;

namespace PlaneWaver
{
    /// <summary>
    //  Abstract class for managing emitter entities
    /// <summary>
    public abstract class EmitterAuthoring : SynthEntity
    {
        #region FIELDS & PROPERTIES

        protected float[] _PerlinSeedArray;
        public enum Condition { Always, Colliding, NotColliding };
        public enum EmitterType { Continuous, Burst }

        public AudioAssetScriptable _AudioAsset;

        [AllowNesting]
        [BoxGroup("Emitter Setup")]
        [SerializeField] protected HostAuthoring _Host;
        [AllowNesting]
        [BoxGroup("Emitter Setup")]
        [SerializeField] protected EmitterType _EmitterType;
        [AllowNesting]
        [BoxGroup("Emitter Setup")]
        [SerializeField] protected Condition _PlaybackCondition = Condition.Always;

        public HostAuthoring Host { get => _Host; set => _Host = value; }

        [AllowNesting]
        [BoxGroup("Playback Setup")]
        [Range(0.01f, 2f)] public float _VolumeAdjust = 0.5f;
        [AllowNesting]
        [BoxGroup("Playback Setup")]
        [Tooltip("Scaling factor applied to the global listener radius value. The result defines the emitter's distance-volume attenuation.")]
        [Range(0.001f, 1f)] public float _DistanceAttenuationFactor = 1f;
        [AllowNesting]
        [BoxGroup("Playback Setup")]
        [Tooltip("Normalised age to begin fadeout of spawned emitter if a DestroyTimer component is attached.")]
        [Range(0, 1)] public float _AgeFadeout = .9f;
        [AllowNesting]
        [BoxGroup("Playback Setup")]
        [Tooltip("Reverses the playhead of an individual grain if it reaches the end of the clip during playback instead of outputting 0s.")]
        [SerializeField] protected bool _PingPongGrainPlayheads = true;
        [AllowNesting]
        [BoxGroup("Playback Setup")]
        [Tooltip("Multiplies the emitter's output by the rigidity value of the colliding surface.")]
        public bool _CollisionRigidityScaleVolume = false;

        public DSP_Class[] _DSPChainParams;

        protected int _SampleRate;
        protected float _SamplesPerMS = 0;

        [AllowNesting]
        [BoxGroup("Runtime Dynamics")]
        [SerializeField] protected bool _IsPlaying;
        [AllowNesting]
        [BoxGroup("Runtime Dynamics")]
        [SerializeField] protected float _AdjustedDistance = 0;
        [AllowNesting]
        [BoxGroup("Runtime Dynamics")]
        [SerializeField] protected float _DistanceAmplitude = 0;
        [AllowNesting]
        [BoxGroup("Runtime Dynamics")]
        [SerializeField] protected float _ColliderRigidityVolume = 1;
        [AllowNesting]
        [BoxGroup("Runtime Dynamics")]
        [SerializeField] protected float _LastTriggeredAt = 0;
        [AllowNesting]
        [BoxGroup("Runtime Dynamics")]
        [SerializeField] protected int _LastSampleIndex = 0;

        public bool IsPlaying => _IsPlaying;
        public float DistanceAmplitude => _DistanceAmplitude;

        #endregion

        #region ENTITY-SPECIFIC START CALL


        public void InitialiseByHost(HostAuthoring host)
        {
            if (_AudioAsset == null)
            {
                Debug.Log($"No audio asset attached to {name}. Killing object.");
                Destroy(gameObject);
            }
            _Host = host;
            _SampleRate = AudioSettings.outputSampleRate;
            _SamplesPerMS = _SampleRate * 0.001f;
            UpdateContactStatus(null);
            InitialisePerlinNoise();
            InitialiseModulationInputs();

            _EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            SetIndex(GrainSynth.Instance.RegisterEmitter(this));
        }

        public virtual ModulationInput[] GatherModulationInputs() { return new ModulationInput[0]; }

        public void InitialiseModulationInputs()
        { 
            foreach (var input in GatherModulationInputs())
                input.SetActors(Host._LocalActor, Host._RemoteActor);
        }

        #endregion

        #region ERRORNOUS EMITTER COMPONENT BUSINESS

        public void UpdateEntityTags()
        {
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

            if (IsPlaying != _EntityManager.HasComponent<PlayingTag>(_Entity))
            {
                if (IsPlaying)
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

        // TODO: Move this over to Actor struct
        public void NewCollision(Collision collision)
        {
            if (_EmitterType == EmitterType.Burst && _PlaybackCondition != Condition.NotColliding)
            {
                if (Time.fixedTime < _LastTriggeredAt + GrainSynth.Instance._BurstDebounceDurationMS * 0.001f)
                    return;

                if (ColliderMoreRigid(collision.collider, _Host.SurfaceRigidity, out float otherRigidity) && OnlyTriggerMostRigid)
                    return;

                _ColliderRigidityVolume = (_Host.SurfaceRigidity + otherRigidity) / 2;
                _LastTriggeredAt = Time.fixedTime;
                _IsPlaying = true;
            }
            else UpdateContactStatus(collision);
        }

        // TODO: Move this over to Actor struct
        public void UpdateContactStatus(Collision collision)
        {
            if (_EmitterType != EmitterType.Continuous)
                return;

            if (_PlaybackCondition == Condition.NotColliding)
            {
                _ColliderRigidityVolume = 1;
                _IsPlaying = collision == null;
                return;
            }

            if (collision == null)
            {
                _ColliderRigidityVolume = 1;
                _IsPlaying = _PlaybackCondition == Condition.Always;
                return;
            }

            if (ColliderMoreRigid(collision.collider, _Host.SurfaceRigidity, out float otherRigidity) && OnlyTriggerMostRigid)
            {
                _IsPlaying = false;
            }
            else
            {
                _ColliderRigidityVolume = (_Host.CollidingRigidity + otherRigidity) / 2;
                _IsPlaying = true;
            }
        }

        protected bool OnlyTriggerMostRigid => GrainSynth.Instance._OnlyTriggerMostRigidSurface;

        public static bool ColliderMoreRigid(Collider collider, float rigidity, out float otherRigidity)
        {
            otherRigidity = collider.TryGetComponent(out SurfaceProperties otherSurface) ? otherSurface._Rigidity : 0.5f;
            return otherSurface != null && otherSurface.IsEmitter && otherSurface._Rigidity >= rigidity;
        }

        public void InitialisePerlinNoise()
        {
            _PerlinSeedArray = new float[10];
            for (int i = 0; i < _PerlinSeedArray.Length; i++)
            {
                float offset = Random.Range(0, 1000);
                _PerlinSeedArray[i] = Mathf.PerlinNoise(offset, offset * 0.5f);
            }
        }

        public float GetPerlinValue(int parameterIndex, float speed = 1f)
        {
            float time = Time.time * speed;
            return Mathf.PerlinNoise(time + _PerlinSeedArray[parameterIndex], (time + _PerlinSeedArray[parameterIndex]) * 0.5f);
        }

        #endregion
    }
}
