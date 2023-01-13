﻿using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

using UnityEngine.Profiling;


public class GrainData
{
    public bool _Pooled = true;
    public bool _IsPlaying = false;
    public float[] _SampleData;
    public int _PlayheadIndex = 0;
    public float _PlayheadNormalised = 0;
    public int _SizeInSamples = -1;
    public int _StartTimeDSP;

    public GrainData(int maxGrainSize)
    {
        // Instantiate the playback data with max grain samples
        _SampleData = new float[maxGrainSize];
    }
}

/// <summary>
//      Speaker components are the final stage of the EmitterSynth, bridging it to Unity's audio engine. 
//      Speakers are fed a queue of audio GrainPlaybackData, which is generated by GrainProcessors in the GrainSynth system.
//      At the SampleStartTime GrainPlaybackData samples are written to the AudioSource's buffer with OnAudioFilterRead.
/// <summary>
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(ConvertToEntity))]
public class SpeakerAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    // Event to populate 
    public delegate void GrainEmitted(GrainData data, int currentDSPSample);
    public event GrainEmitted OnGrainEmitted;

    #region -------------------------- VARIABLES  
    protected EntityManager _EntityManager;
    protected Entity _SpeakerEntity;
    protected SpeakerComponent _SpeakerComponent;
    protected MeshRenderer _MeshRenderer;
    protected GrainData[] _GrainDataArray;   
    private int _SampleRate;

    public GrainSynth _GrainSynth;
    public int _SpeakerIndex = int.MaxValue;

    public int _PooledGrainCount = 0;
    int ActiveGrainPlaybackDataCount { get { return _GrainDataArray.Length - _PooledGrainCount; } }

    readonly int _GrainPoolSize = 100;
    private int _DebugTotalGrainsCreated = 0;

    private AudioSource _AudioSource;
    readonly float _VolumeSmoothing = 4;
    private float _TargetVolume = 0;

    public bool _Registered = false;

    [HideInInspector]
    public bool DedicatedToHost { get { return _StaticallyPairedEmitters.Count > 0; } }
    public List<GameObject> _StaticallyPairedEmitters = new List<GameObject>();

    [SerializeField]
    private bool _IsActive = false;
    public bool _DebugLog = false;
    [SerializeField]
    private bool _Initialized = false;
    protected int _CurrentDSPSample;

    #endregion

    public void Awake()
    {
        GetComponent<ConvertToEntity>().ConversionMode = ConvertToEntity.Mode.ConvertAndInjectGameObject;
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        _EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _MeshRenderer = gameObject.GetComponentInChildren<MeshRenderer>();
        _AudioSource = gameObject.GetComponent<AudioSource>();
        _SampleRate = AudioSettings.outputSampleRate;

        _SpeakerEntity = entity;
        _GrainSynth = FindObjectOfType<GrainSynth>();
        _GrainSynth.RegisterSpeaker(this);

        #if UNITY_EDITOR
                dstManager.SetName(_SpeakerEntity, "Speaker " + _SpeakerIndex + " (" + (DedicatedToHost ? "Dedicated" : "Dynamic") + ") ");
        #endif

        dstManager.AddComponentData(entity, new SpeakerComponent { _SpeakerIndex = _SpeakerIndex });

        if (!DedicatedToHost)
        {
            dstManager.AddComponentData(entity, new PoolingComponent { _State = PooledState.Pooled });
        }

        //---   CREATE GRAIN DATA ARRAY - CURRENT MAXIMUM LENGTH SET TO ONE SECOND OF SAMPLES      
        _GrainDataArray = new GrainData[_GrainPoolSize];

        for (int i = 0; i < _GrainPoolSize; i++)
            _GrainDataArray[i] = CreateNewGrain();

        _PooledGrainCount = _GrainDataArray.Length;
        _AudioSource.rolloffMode = AudioRolloffMode.Custom;
        _AudioSource.maxDistance = 500;
        _Initialized = true;
    }

    public void AddEmitterLink(GameObject emitterGameObject)
    {
        _StaticallyPairedEmitters.Add(emitterGameObject);
    }
  
    public int RegisterAndGetIndex()
    {     
        GrainSynth.Instance.RegisterSpeaker(this);
        return _SpeakerIndex;
    }

    GrainData CreateNewGrain()
    {
        _DebugTotalGrainsCreated++;
        return new GrainData(_SampleRate);
    }

    public void Update()
    {
        if (!_Initialized)
            return;

        if (_DebugLog)
            ReportGrainsDebug("");

        //---   Pool playback finished playback data for re-use.
        for (int i = 0; i < _GrainDataArray.Length; i++)
        {
            if (_GrainDataArray[i]._Pooled == false && _GrainDataArray[i]._IsPlaying == true && (
                _GrainDataArray[i]._PlayheadIndex >= _GrainDataArray[i]._SizeInSamples ||
                _GrainDataArray[i]._StartTimeDSP < _CurrentDSPSample))
            {
                ClearGrainDataObject(i);
                _PooledGrainCount++;
            }
        }

        #region ---   DYNAMIC EMITTER HOST ATTACHMENT
        if (!DedicatedToHost)
        {
            transform.position = _EntityManager.GetComponentData<Translation>(_SpeakerEntity).Value;
            _SpeakerComponent = _EntityManager.GetComponentData<SpeakerComponent>(_SpeakerEntity);
            bool newActiveState = _EntityManager.GetComponentData<PoolingComponent>(_SpeakerEntity)._State == PooledState.Active;
            //---   Reset playback grain data pool when the speaker disconnects
            if (_IsActive && !newActiveState)
            {
                for (int i = 0; i < _GrainDataArray.Length; i++)
                    ClearGrainDataObject(i);
                _PooledGrainCount = _GrainDataArray.Length;
            }
            //---   SET MESH VISIBILITY AND VOLUME BASED ON CONNECTION TO EMITTER
            _TargetVolume = newActiveState ? 1 : 0;
            _AudioSource.volume = Mathf.Lerp(_AudioSource.volume, _TargetVolume, Time.deltaTime * _VolumeSmoothing);
            if (_TargetVolume == 0 && _AudioSource.volume < .005f)
                _AudioSource.volume = 0;
            if (_MeshRenderer != null)
                _MeshRenderer.enabled = newActiveState;
            _IsActive = newActiveState;
        }
        #endregion
    }

    public void ClearGrainDataObject(int index)
    {
        _GrainDataArray[index]._StartTimeDSP = int.MaxValue;
        _GrainDataArray[index]._IsPlaying = false;
        _GrainDataArray[index]._Pooled = true;
    }


    #region GRAIN PLAYBACK DATA POOLING
    public GrainData GetPooledGrainDataObject()
    {
        if (!_Initialized)
        {
            return null;            
        }
        if (_PooledGrainCount > 0)
            for (int i = 0; i < _GrainDataArray.Length; i++)
            {
                if (_GrainDataArray[i]._Pooled)
                {
                    _GrainDataArray[i]._Pooled = false;
                    _PooledGrainCount--;
                    return _GrainDataArray[i];
                }
            }
        return null;
    }

    //---   (FOR VISUALISATION?) ADDS GRAIN PLAYBACK DATA BACK TO THE POOL  
    public void AddGrainPlaybackDataToPool(GrainData playbackData)
    {
        if (!_Initialized)
            return;

        OnGrainEmitted?.Invoke(playbackData, _GrainSynth._CurrentDSPSample);
    }
    #endregion

    void ReportGrainsDebug(string action)
    {
        //if (!_DebugLog)
        //    return;
                
        print(name + "---------------------------  " + action + "       A: " + ActiveGrainPlaybackDataCount + "  P: " + _PooledGrainCount + "      T: " + _DebugTotalGrainsCreated);        
    }


    // AUDIO BUFFER CALLS
    // DSP Buffer size in audio settings
    // Best performance - 46.43991
    // Good latency - 23.21995
    // Best latency - 11.60998
    
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!_Initialized || _PooledGrainCount == _GrainPoolSize)
            return;
        
        _CurrentDSPSample = _GrainSynth._CurrentDSPSample;
        // Populate current output buffer with grain samples, maintaining grain playhead index over successive buffers
        for (int dataIndex = 0; dataIndex < data.Length; dataIndex += channels)
            for (int i = 0; i < _GrainDataArray.Length; i++) 
            {
                GrainData grain = _GrainDataArray[i];
                if (grain._IsPlaying && _CurrentDSPSample >= grain._StartTimeDSP)
                    if (grain._PlayheadIndex >= grain._SizeInSamples)
                        _GrainDataArray[i]._IsPlaying = false;
                    else
                        for (int chan = 0; chan < channels; chan++)
                            data[dataIndex + chan] += grain._SampleData[grain._PlayheadIndex];
                        _GrainDataArray[i]._PlayheadIndex++;
            }
    }

    void OnDrawGizmos()
    {
        if (_IsActive)
        {
            if(_SpeakerIndex == 0)
                Gizmos.color = Color.blue;
            else
                Gizmos.color = Color.yellow;

            Gizmos.DrawWireSphere(transform.position, _GrainSynth._SpeakerAttachRadius);
        }
    }

    private void OnDestroy()
    {
        GrainSynth.Instance.DeregisterSpeaker(this);
        DestroyEntity();
    }

    public void DestroyEntity()
    {
        //print("Speaker DestroyEntity");
        if (World.All.Count != 0 && _SpeakerEntity != null)
            _EntityManager.DestroyEntity(_SpeakerEntity);
    }
}
