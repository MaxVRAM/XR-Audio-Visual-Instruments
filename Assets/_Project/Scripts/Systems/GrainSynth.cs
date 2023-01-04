﻿using Unity.Burst;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Profiling;
using System;
using System.Linq;
using Unity.Entities.UniversalDelegates;
using Unity.Entities.CodeGeneratedJobForEach;
using Random = UnityEngine.Random;
using System.Runtime.InteropServices;

[RequireComponent(typeof(AudioSource))]
public class GrainSynth :  MonoBehaviour
{
    public static GrainSynth Instance;
    protected EntityManager _EntityManager;
    protected EntityQuery _GrainQuery;
    protected Entity _DSPTimerEntity;
    protected Entity _SpeakerManagerEntity;
    protected AudioListener _Listener;


    [Header("Audio Clip Library")]
    public AudioClip[] _AudioClips;


    [Header("Emitter Config")]
    public float _ListenerRadius = 10;
    public float _SpeakerAttachRadius = 1;
    public bool _DrawAttachmentLines = false;
    public Material _AttachmentLineMat;

    [Header("Speakers")]
    public int _MaxDynamicSpeakers = 50;
    public SpeakerAuthoring _DynamicSpeakerPrefab;

    // TODO: Implement system to reduce dynamic speakers if additional dedicated speakers are spawned at runtime.
    // Important to avoid audio voice limit.
    int _MaxSpeakers;

    [Header("DSP Config")]
    [Range(0, 100)]
    [Tooltip(@"Additional time to buffer processed grains. Set at 0, the grain buffer has a duration of previous frame, 
        and will almost certainly create underrun (dead-spots). Additional time adds latency, but will help produce consistent playback.")]
    public float _QueueDurationMS = 50;
    public int QueueDurationSamples { get { return (int)(_QueueDurationMS * _SampleRate * .001f); } }

    [Header("Registered Components")]
    public List<Entity> _AllEmitters = new List<Entity>();
    public List<SpeakerAuthoring> _Speakers = new List<SpeakerAuthoring>();
    protected List<AudioClip> _AudioClipList = new List<AudioClip>();

    [Header("Runtime Dynamics")]
    [SerializeField]
    public int _SampleRate;
    [SerializeField]
    public int _CurrentDSPSample;
    [SerializeField]
    protected int _GrainProcessorCount = 0;
    [SerializeField]
    protected int _UnplayedProcessorsDestroyed = 0;


    private void Awake()
    {
        Instance = this;
        _MaxSpeakers = AudioSettings.GetConfiguration().numRealVoices;
    }

    public int RegisterAudioClip(AudioClip clip)
    {
        int index = 0;
        if(_AudioClipList.Contains(clip)) index = _AudioClipList.IndexOf(clip);
        else
        {
            _AudioClipList.Add(clip);
            index = _AudioClipList.IndexOf(clip);
        }
        return index;
    }

    public void Start()
    {
        _EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _SampleRate = AudioSettings.outputSampleRate;

        for (int i = 0; i < _MaxDynamicSpeakers; i++)
            CreateSpeaker(transform.position);

        _DSPTimerEntity = _EntityManager.CreateEntity();
        _EntityManager.AddComponentData(_DSPTimerEntity, new DSPTimerComponent { _CurrentSampleIndex = _CurrentDSPSample, _GrainQueueDuration = (int)(AudioSettings.outputSampleRate * _QueueDurationMS) });

        _GrainQuery = _EntityManager.CreateEntityQuery(typeof(GrainProcessorComponent));

        // ---- CREATE SPEAKER MANAGER
        _Listener = FindObjectOfType<AudioListener>();
        _SpeakerManagerEntity = _EntityManager.CreateEntity();
        _EntityManager.AddComponentData(_SpeakerManagerEntity, new ActivationRadiusComponent
        {
            _ListenerPos = _Listener.transform.position,
            _ListenerRadius = _ListenerRadius,
            _AttachmentRadius = _SpeakerAttachRadius
        });

        // ----   CREATE AUDIO SOURCE BLOB ASSETS AND ASSIGN TO AudioClipDataComponent ENTITIES
        for (int i = 0; i < _AudioClips.Length; i++)
        {
            Entity audioClipDataEntity = _EntityManager.CreateEntity();

            int clipChannels = _AudioClips[i].channels;

            float[] clipData = new float[_AudioClips[i].samples];

            _AudioClips[i].GetData(clipData, 0);

            using (BlobBuilder blobBuilder = new BlobBuilder(Allocator.Temp))
            {
                // ---- CREATE BLOB
                ref FloatBlobAsset audioClipBlobAsset = ref blobBuilder.ConstructRoot<FloatBlobAsset>();
                BlobBuilderArray<float> audioClipArray = blobBuilder.Allocate(ref audioClipBlobAsset.array, (clipData.Length / clipChannels));

                for (int s = 0; s < clipData.Length - 1; s += clipChannels)
                {
                    audioClipArray[s / clipChannels] = 0;
                    // Mono-sum stereo audio files
                    // TODO: Investigate if audio assets could provide benefit to the EmitterSynth framework. 
                    for (int c = 0; c < clipChannels; c++)
                        audioClipArray[s / clipChannels] += clipData[s + c];
                }

                // ---- CREATE REFERENCE AND ASSIGN TO ENTITY
                BlobAssetReference<FloatBlobAsset> audioClipBlobAssetRef = blobBuilder.CreateBlobAssetReference<FloatBlobAsset>(Allocator.Persistent);
                _EntityManager.AddComponentData(audioClipDataEntity, new AudioClipDataComponent { _ClipDataBlobAsset = audioClipBlobAssetRef, _ClipIndex = i });

                #if UNITY_EDITOR
                    _EntityManager.SetName(audioClipDataEntity, "Audio clip blob asset " + i);
                #endif
            }
        }

        // ---- CREATE WINDOWING BLOB ASSET
        Entity windowingBlobEntity = _EntityManager.CreateEntity();
        using (BlobBuilder blobBuilder = new BlobBuilder(Allocator.Temp))
        {
            // ---- CREATE BLOB
            ref FloatBlobAsset windowingBlobAsset = ref blobBuilder.ConstructRoot<FloatBlobAsset>();
            BlobBuilderArray<float> windowArray = blobBuilder.Allocate(ref windowingBlobAsset.array, 512);

            for (int i = 0; i < windowArray.Length; i++)
                windowArray[i] = 0.5f * (1 - Mathf.Cos(2 * Mathf.PI * i / windowArray.Length));

            // ---- CREATE REFERENCE AND ASSIGN TO ENTITY
            BlobAssetReference<FloatBlobAsset> windowingBlobAssetRef = blobBuilder.CreateBlobAssetReference<FloatBlobAsset>(Allocator.Persistent);
            _EntityManager.AddComponentData(windowingBlobEntity, new WindowingDataComponent { _WindowingArray = windowingBlobAssetRef });
        }
    }

    private void Update()
    {
        // Update DSP sample
        DSPTimerComponent dspTimer = _EntityManager.GetComponentData<DSPTimerComponent>(_DSPTimerEntity);
        _EntityManager.SetComponentData(_DSPTimerEntity, new DSPTimerComponent { _CurrentSampleIndex = _CurrentDSPSample + (int)(Time.deltaTime * _SampleRate), _GrainQueueDuration = QueueDurationSamples });

        NativeArray<Entity> currentGrainProcessors = _GrainQuery.ToEntityArray(Allocator.TempJob);

        // Update audio listener position
        _EntityManager.SetComponentData(_SpeakerManagerEntity, new ActivationRadiusComponent
        {
            _ListenerPos = _Listener.transform.position,
            _ListenerRadius = _ListenerRadius,
            _AttachmentRadius = _SpeakerAttachRadius
        });


        _GrainProcessorCount = (int)Mathf.Lerp(_GrainProcessorCount, currentGrainProcessors.Length, Time.deltaTime * 10f);

        //---- Loop through all grain processors and fill its speaker's audio buffer
        for (int i = 0; i < currentGrainProcessors.Length; i++)
        {
            GrainProcessorComponent grainProcessor = _EntityManager.GetComponentData<GrainProcessorComponent>(currentGrainProcessors[i]);

            //----  Remove grain processor if start time is one grain queue length in the past
            if (grainProcessor._StartSampleIndex < _CurrentDSPSample - QueueDurationSamples)
            {
                _EntityManager.DestroyEntity(currentGrainProcessors[i]);
                _UnplayedProcessorsDestroyed ++;
                continue;
            }

            if (grainProcessor._SamplePopulated)
            {
                GrainData playbackData = _Speakers[grainProcessor._SpeakerIndex].GetGrainDataFromPool();

                if (playbackData == null)
                    continue;

                NativeArray<float> processedSamples = _EntityManager.GetBuffer<GrainSampleBufferElement>(currentGrainProcessors[i]).Reinterpret<float>().ToNativeArray(Allocator.Temp);

                playbackData._IsPlaying = true;
                playbackData._PlayheadIndex = 0;
                playbackData._SizeInSamples = processedSamples.Length;
                playbackData._DSPStartTime = grainProcessor._StartSampleIndex;
                playbackData._PlayheadNormalised = grainProcessor._PlayheadNorm;

                NativeToManagedCopyMemory(playbackData._SampleData, processedSamples);

                // Destroy entity once we have sapped it of it's samply goodness and add playback data to speaker grain pool
                _EntityManager.DestroyEntity(currentGrainProcessors[i]);
                _Speakers[grainProcessor._SpeakerIndex].AddGrainPlaybackDataToPool(playbackData);
            }
        }
        currentGrainProcessors.Dispose();
    }


    public static unsafe void NativeToManagedCopyMemory(float[] targetArray, NativeArray<float> SourceNativeArray)
    {
        void* memoryPointer = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(SourceNativeArray);
        Marshal.Copy((IntPtr)memoryPointer, targetArray, 0, SourceNativeArray.Length);
    }

    public void CreateSpeaker(Vector3 pos)
    {
        SpeakerAuthoring speaker = Instantiate(_DynamicSpeakerPrefab, pos, quaternion.identity, transform);    
    }


    public void RegisterSpeaker(SpeakerAuthoring speaker)
    {
        if (speaker._Registered || _Speakers.Contains(speaker))
            return;
        speaker._SpeakerIndex = _Speakers.Count;
        speaker._Registered = true;
        speaker.name = speaker.name + " - Speaker " + _Speakers.Count;
        _Speakers.Add(speaker);
    }

    public int RegisterEmitter(Entity emitterEntity)
    {
        int index = _AllEmitters.Count;
        _AllEmitters.Add(emitterEntity);
        return index;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        for (int dataIndex = 0; dataIndex < data.Length; dataIndex += channels)
            _CurrentDSPSample++;
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
            return;
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(_Listener.transform.position, _ListenerRadius);
    }
}

[System.Serializable]
public class AudioClipLibrary
{
    public AudioClip[] _Clips;
    public List<float[]> _ClipsDataArray = new List<float[]>();

    public void Initialize()
    {
        if (_Clips.Length == 0)
            Debug.LogError("No clips in clip library");
        else
            Debug.Log("Initializing clip library.");

        for (int i = 0; i < _Clips.Length; i++)
        {
            AudioClip audioClip = _Clips[i];

            if (audioClip.channels > 1)
            {
                Debug.LogError("Audio clip not mono");
            }

            float[] samples = new float[audioClip.samples];
            _Clips[i].GetData(samples, 0);
            _ClipsDataArray.Add(samples);

            Debug.Log(String.Format("Clip {0}      Samples: {1}        Time length: {2} ", _Clips[i].name, _ClipsDataArray[i].Length, _ClipsDataArray[i].Length / (float)_Clips[i].frequency));
        }


    }
}
