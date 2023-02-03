using System.Collections;
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;


#region GRAIN CLASS

public class Grain
{
    public bool _Pooled = true;
    public bool _IsPlaying = false;
    public float[] _SampleData;
    public int _PlayheadIndex = 0;
    public float _PlayheadNormalised = 0;
    public int _SizeInSamples = -1;
    public int _DSPStartTime;

    public Grain(int maxGrainSize)
    {
        _SampleData = new float[maxGrainSize];
    }
}

#endregion

/// <summary>
//      Speaker components are the final stage of the EmitterSynth, bridging it to Unity's audio engine. 
//      Grains are passed to speakers by the Synth Manager, which are then written to the AudioSource's output buffer.
/// <summary>

[RequireComponent(typeof(AudioSource))]
public class SpeakerAuthoring : SynthEntityBase
{
    #region FIELDS & PROPERTIES

    [SerializeField] private ConnectionState _State = ConnectionState.Disconnected;
    [SerializeField] private bool _GrainArrayReady = false;
    [SerializeField] private int _GrainArraySize = 100;
    [SerializeField] private int _NumGrainsFree = 0;
    [SerializeField] private float _TargetVolume = 0;
    [SerializeField] private float _VolumeSmoothing = 4;
    [SerializeField] private float _AttachmentRadius = 1;
    private int _SampleRate;

    private MeshRenderer _MeshRenderer;
    private AudioSource _AudioSource;
    private Grain[] _GrainArray;

    public delegate void GrainEmitted(Grain data, int currentDSPSample);
    public event GrainEmitted OnGrainEmitted;

    #endregion

    #region ENTITY-SPECIFIC START CALL

    private void Start()
    {
        _SampleRate = AudioSettings.outputSampleRate;
        _MeshRenderer = gameObject.GetComponentInChildren<MeshRenderer>();
        _AudioSource = gameObject.GetComponent<AudioSource>();
        _AudioSource.rolloffMode = AudioRolloffMode.Custom;
        _AudioSource.maxDistance = 500;
        InitialiseGrainArray();

        _EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _Archetype = _EntityManager.CreateArchetype(
            typeof(SpeakerComponent),
            typeof(SpeakerIndex),
            typeof(Translation));
    }

    #endregion

    #region SECRET SPEAKER COMPONENT BUSINESS

    public override void SetEntityType()
    {
        _EntityType = SynthEntityType.Speaker;
    }

    public override void InitialiseComponents()
    {
        _EntityManager.SetComponentData(_Entity, new SpeakerIndex { Value = EntityIndex });
        _EntityManager.SetComponentData(_Entity, new Translation { Value = transform.position });
        _EntityManager.SetComponentData(_Entity, new SpeakerComponent
        {
            _State = ConnectionState.Disconnected,
            _AttachmentRadius = _AttachmentRadius,
            _AttachedHostCount = 0
        });
    }

    public override void ProcessComponents()
    {
        ProcessIndex(_EntityManager.GetComponentData<SpeakerIndex>(_Entity));
        ProcessPooling(_EntityManager.GetComponentData<SpeakerComponent>(_Entity));
        ProcessTranslation(_EntityManager.GetComponentData<Translation>(_Entity));
    }

    public void ProcessIndex(SpeakerIndex index)
    {
        if (EntityIndex != index.Value)
            _EntityManager.SetComponentData(_Entity, new SpeakerIndex { Value = EntityIndex });
    }

    public void ProcessPooling(SpeakerComponent pooling)
    {
        _AttachmentRadius = pooling._AttachmentRadius;
        transform.localScale = Vector3.one * _AttachmentRadius;

        bool updatedState = _State != pooling._State;
        _State = pooling._State;

        if (updatedState)
            ResetGrainPool();
        else
            UpdateGrainPool();

        _TargetVolume = _State != ConnectionState.Disconnected ? 1 : 0;

        if (_TargetVolume == 0 && _AudioSource.volume < .005f)
            _AudioSource.volume = 0;
        else if (_TargetVolume == 1 && _AudioSource.volume > .995f)
            _AudioSource.volume = 1;
        else
            _AudioSource.volume = Mathf.Lerp(_AudioSource.volume, _TargetVolume, Time.deltaTime * _VolumeSmoothing);

        if (_MeshRenderer != null)
            _MeshRenderer.enabled = _State != ConnectionState.Disconnected;
    }

    public void ProcessTranslation(Translation translation)
    {
        transform.position = translation.Value;
    }

    public override void Deregister()
    {
        GrainSynth.Instance.DeregisterSpeaker(this);
    }

    #endregion

    #region GRAIN POOLING MANAGEMENT

    Grain CreateNewGrain(int? numSamples = null)
    {
        int samples = numSamples.HasValue ? numSamples.Value : _SampleRate;
        return new Grain(samples);
    }

    public void InitialiseGrainArray()
    {
        _GrainArray = new Grain[_GrainArraySize];
        for (int i = 0; i < _GrainArraySize; i++)
            _GrainArray[i] = CreateNewGrain();

        _NumGrainsFree = _GrainArray.Length;
        _GrainArrayReady = true;
    }

    public void ResetGrainPool()
    {
        for (int i = 0; i < _GrainArray.Length; i++)
        {
            _GrainArray[i]._Pooled = true;
            _GrainArray[i]._IsPlaying = false;
        }
        _NumGrainsFree = _GrainArray.Length;
    }
 
    public void UpdateGrainPool()
    {
        _NumGrainsFree = 0;
        for (int i = 0; i < _GrainArray.Length; i++)
            if (!_GrainArray[i]._IsPlaying)
            {
                _GrainArray[i]._Pooled = true;
                _NumGrainsFree++;
            }
        _GrainArrayReady = true;
    }

    public void GrainAdded(Grain grainData)
    {
        if (!_EntityInitialised)
            return;
        _NumGrainsFree--;
        OnGrainEmitted?.Invoke(grainData, GrainSynth.Instance._CurrentDSPSample);
    }

    public Grain GetEmptyGrain(out Grain grain)
    {
        grain = null;
        if (_EntityInitialised)
        {
            // If we're desperate, go through the GrainPool to check if any grains have finished
            if (_NumGrainsFree == 0 && !_GrainArrayReady)
                UpdateGrainPool();
            // Get first pooled grain data object
            if (_NumGrainsFree > 0)
                for (int i = 0; i < _GrainArray.Length; i++)
                    if (_GrainArray[i]._Pooled)
                    {
                        grain = _GrainArray[i];
                        return grain;
                    }
        }
        return grain;
    }

    #endregion

    #region AUDIO OUTPUT BUFFER POPULATION

    // TODO -  ADD SAMPLE BUFFER RMS ANALYSIS FOR SPEAKER VISUAL MODULATION
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!_EntityInitialised || _GrainArray == null || _NumGrainsFree == _GrainArraySize)
            return;
        
        Grain grainData;
        int _CurrentDSPSample = GrainSynth.Instance._CurrentDSPSample;

        // Populate audio buffer with grain samples and maintain sample index for successive buffers
        for (int dataIndex = 0; dataIndex < data.Length; dataIndex += channels)
            for (int i = 0; i < _GrainArray.Length; i++) 
            {
                if (!_GrainArray[i]._IsPlaying)
                    continue;

                grainData = _GrainArray[i];
                //---   GRAIN DSP START INDEX REACHED = ADD SAMPLE
                if (_CurrentDSPSample >= grainData._DSPStartTime)
                    //--- GRAIN HAS REACHED THE END OF ITS PLAYHEAD = STOP PLAYBACK
                    if (grainData._PlayheadIndex >= grainData._SizeInSamples)
                    {
                        grainData._IsPlaying = false;
                    }
                    else
                    {
                        //--- SHOULD ACCEPT MULTIPLE CHANNEL GRAINS/SPEAKERS. FRAMEWORK CURRENTLY MONO ONLY
                        for (int chan = 0; chan < channels; chan++)
                            data[dataIndex + chan] += grainData._SampleData[grainData._PlayheadIndex];
                        grainData._PlayheadIndex++;
                    }
            }
    }

    #endregion
}
