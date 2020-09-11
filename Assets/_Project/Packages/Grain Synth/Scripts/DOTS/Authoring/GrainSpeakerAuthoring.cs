﻿using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;


public class GrainPlaybackData
{
    public bool _IsPlaying = true;
    public float[] _GrainSamples;
    public float[] _TempSampleBuffer;
    public int _PlaybackIndex = 0;
    public int _PlaybackSampleCount;

    // The DSP sample that the grain starts at
    public int _DSPStartIndex;

    public GrainPlaybackData()
    {
        // instantiate the grain samples at the max length of a grain of 1 second worth of samples
        _GrainSamples = new float[44 * 1000];
        _TempSampleBuffer = new float[44 * 1000];
    }
}

public class GrainSpeakerAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public delegate void GrainEmitted(GrainPlaybackData data, int currentDSPSample);
    public event GrainEmitted OnGrainEmitted;

    #region -------------------------- VARIABLES
    EntityManager _EntityManager;
    Entity _Entity;
    GrainSpeakerComponent _SpeakerComponenet;

    MeshRenderer _MeshRenderer;

    GrainSynth _GrainSynth;
    public int _SpeakerIndex = 0;

    List<GrainPlaybackData> _ActiveGrainPlaybackData = new List<GrainPlaybackData>();
    List<GrainPlaybackData> _PooledGrainPlaybackData = new List<GrainPlaybackData>();

    int _GrainPlaybackDataToPool = 100;
    int _MaxGrainPlaybackDataCout = 500;

    int GrainDataCount { get { return _ActiveGrainPlaybackData.Count + _PooledGrainPlaybackData.Count; } }

    //DebugGUI_Granulator _DebugGUI;
    int prevStartSample = 0;
    bool _Initialized = false;

    AudioSource _AudioSource;
    float _VolumeSmoothing = 4;

    float _TargetVolume = 0;


    public bool _Registered = false;

    [HideInInspector]
    public bool _StaticallyPaired = false;

    public bool _DebugLog = false;
    #endregion


    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        _Entity = entity;
        _GrainSynth = FindObjectOfType<GrainSynth>();
        _GrainSynth.RegisterSpeaker(this);

        name = "Speaker " + _SpeakerIndex;

        // Register the speaker and get the index
        dstManager.AddComponentData(entity, new GrainSpeakerComponent { _Index = _SpeakerIndex, _InRange = false });

        _Initialized = true;
    }

    public void Start()
    {
        _EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        _MeshRenderer = gameObject.GetComponentInChildren<MeshRenderer>();
        _AudioSource = gameObject.GetComponent<AudioSource>();

        // Pool grain playback data
        for (int i = 0; i < _GrainPlaybackDataToPool; i++)        
            _PooledGrainPlaybackData.Add(new GrainPlaybackData());        
    }

    public void Update()
    {
        if (!_Initialized)
            return;

        transform.position = _EntityManager.GetComponentData<Translation>(_Entity).Value;

        if (!_StaticallyPaired)
        {
            // Clear playback data if not connected too emitters
            _SpeakerComponenet = _EntityManager.GetComponentData<GrainSpeakerComponent>(_Entity);
            _TargetVolume = _SpeakerComponenet._ConnectedToEmitter ? 1 : 0;
            _AudioSource.volume = Mathf.Lerp(_AudioSource.volume, _TargetVolume, Time.deltaTime * _VolumeSmoothing);
            _MeshRenderer.enabled = _SpeakerComponenet._ConnectedToEmitter;
        }
    }

    public void AddGrainPlaybackData(GrainPlaybackData playbackData)
    {
        _ActiveGrainPlaybackData.Add(playbackData);

        int samplesBetweenGrains = playbackData._DSPStartIndex - prevStartSample;
        float msBetweenGrains = (samplesBetweenGrains / (float)AudioSettings.outputSampleRate) * 1000;
        float DSPSampleDiff = playbackData._DSPStartIndex - _GrainSynth._CurrentDSPSample;
        int DSPMSDiff = (int)((DSPSampleDiff / (float)AudioSettings.outputSampleRate) * 1000);

        // Fire event if hooked up
        OnGrainEmitted?.Invoke(playbackData, _GrainSynth._CurrentDSPSample);

        if (_DebugLog)
        {
            print
            (
                "Grain added. Start sample: " + playbackData._DSPStartIndex +
                " Cadence samples: " + samplesBetweenGrains +
                " Cadence m/s:   " + msBetweenGrains +
                " DSP sample diff:   " + DSPSampleDiff +
                " DSP m/s diff:   " + DSPMSDiff
            );
        }

        //_DebugGUI.LogLatency(DSPMSDiff);

        prevStartSample = playbackData._DSPStartIndex;

        //print("Grain added. Sample 1000: " + playbackData._GrainSamples[1000] + "  playbackData duration: " + playbackData._GrainSamples.Length);
    }

    public void Deactivate()
    {       
        // Move all active grain playback data to the pool
        for (int i = _ActiveGrainPlaybackData.Count - 1; i >= 0; i--)
        {
            _PooledGrainPlaybackData.Add(_ActiveGrainPlaybackData[i]);
        }
        _ActiveGrainPlaybackData.Clear();

        gameObject.SetActive(false);
    }

    public GrainPlaybackData GetGrainPlaybackDataFromPool()
    {
        if (_PooledGrainPlaybackData.Count >= 1)
        {
            GrainPlaybackData grainPlaybackData = _PooledGrainPlaybackData[0];
            _PooledGrainPlaybackData.Remove(grainPlaybackData);

            return grainPlaybackData;
        }
        else if (GrainDataCount < _MaxGrainPlaybackDataCout)
        {
            GrainPlaybackData grainPlaybackData = new GrainPlaybackData();
            return grainPlaybackData;
        }
        else
        {
            print(name + "------  Audio output already using max grains. " + GrainDataCount + "/" + _MaxGrainPlaybackDataCout);
            return null;
        }
    }

    // AUDIO BUFFER CALLS
    // DSP Buffer size in audio settings
    // Best performance - 46.43991
    // Good latency - 23.21995
    // Best latency - 11.60998

    float _SamplesPerRead = 0;
    float _SamplesPerSecond = 0;
    float prevTime = 0;

    void OnAudioFilterRead(float[] data, int channels)
    {
        _SamplesPerRead = 0;
        // For length of audio buffer, populate with grain samples, maintaining index over successive buffers
        for (int dataIndex = 0; dataIndex < data.Length; dataIndex += channels)
        {
            for (int i = 0; i < _ActiveGrainPlaybackData.Count; i++)
            {
                GrainPlaybackData grainData = _ActiveGrainPlaybackData[i];

                if (grainData == null)
                    continue;

                if (_GrainSynth._CurrentDSPSample >= grainData._DSPStartIndex)
                {
                    if (grainData._PlaybackIndex >= grainData._PlaybackSampleCount)
                    {
                        grainData._IsPlaying = false;
                    }
                    else
                    {
                        _SamplesPerRead++;
                        data[dataIndex] += grainData._GrainSamples[grainData._PlaybackIndex];
                        grainData._PlaybackIndex++;
                    }
                }
            }
        }

        // Removed finished playback data from pool
        for (int i = _ActiveGrainPlaybackData.Count - 1; i >= 0; i--)
        {
            if (_ActiveGrainPlaybackData[i] == null)
                continue;

            if (!_ActiveGrainPlaybackData[i]._IsPlaying)
            {
                // Add to pool
                _PooledGrainPlaybackData.Add(_ActiveGrainPlaybackData[i]);
                // Remove from active pist
                _ActiveGrainPlaybackData.RemoveAt(i);
            }
        }


        // ----------------------DEBUG
        float dt = (float)AudioSettings.dspTime - prevTime;
        prevTime = (float)AudioSettings.dspTime;
        float newSamplesPerSecond = _SamplesPerRead * (1f / dt);
        float concurrentSamples = newSamplesPerSecond / 44100;
        _SamplesPerSecond = newSamplesPerSecond;
    }

    void OnDrawGizmos()
    {
        if (_SpeakerComponenet._ConnectedToEmitter)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _GrainSynth._EmitterToSpeakerAttachRadius);
        }
    }
}