using Unity.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using System;
using System.Runtime.InteropServices;


    // PROJECT AUDIO CONFIGURATION NOTES
    // ---------------------------------

        // DSP Buffer size in audio settings
        // Best performance - 46.43991
        // Good latency - 23.21995
        // Best latency - 11.60998

    // TODO - FIX WINDOWING FUNCTIONS:
    // https://michaelkrzyzaniak.com/AudioSynthesis/2_Audio_Synthesis/11_Granular_Synthesis/1_Window_Functions/


[RequireComponent(typeof(AudioSource))]
public class GrainSynth :  MonoBehaviour
{
    public static GrainSynth Instance;
    protected EntityManager _EntityManager;
    protected EntityQuery _GrainQuery;
    protected Entity _DSPTimerEntity;
    protected Entity _AttachParamEntity;
    protected AudioListener _Listener;


    [Header("Audio Clip Library")]
    public AudioClip[] _AudioClips;


    [Header("Speaker Configuration")]
    public GameObject _SpeakerPrefab;
    public Vector3 _PooledSpeakerPosition = Vector3.down * 10;
    [Range(0, 64)]
    public int _MaxDynamicSpeakers = 32;
    [Range(0.1f, 50)]
    public float _ListenerRadius = 10;
    [Range(0.1f, 45)]
    public float _SpeakerAttachArcDegrees = 10;
    [Range(0, 30)]
    public float _SpeakerAttachPositionSmoothing = 0.1f;
    public float AttachSmoothing { get { return 1 / _SpeakerAttachPositionSmoothing / 5; }}
    public bool _DrawAttachmentLines = false;
    [Range(0, 0.05f)]
    public float _AttachmentLineWidth = 0.002f;
    public Material _AttachmentLineMat;

    // TODO: Implement system to reduce dynamic speakers if additional dedicated speakers are spawned at runtime to avoid audio voice limit.
    int _MaxSpeakers;

    [Header("DSP Config")]
    [Range(0, 100)]
    [Tooltip("Additional ms to calculate and queue grains each frame. Set to 0, the grain queue equals the previous frame's duration. Adds latency, but help to avoid underrun. Recommended values > 20ms.")]
    public float _QueueDurationMS = 22;
    [Range(0, 100)]
    [Tooltip("Percentage of previous frame duration to delay grain start times of next frame. Adds a predictable amount of latency to help avoid timing issues when the framerate slows.")]
    public float _DelayByPercentageOfPreviousDuration = 10;
    [Range(0, 60)]
    [Tooltip("Discard unplayed grains with a DSP start index more than this value (ms) in the past. Prevents clustered grain playback when resources are near their limit.")]
    public float _DiscardGrainsOlderThanMS = 10;
    [Range(0, 40)]
    [Tooltip("Delay bursts triggered on the same frame by a random amount. Helps avoid phasing issues caused by identical emitters triggered together.")]
    public float _RandomiseBurstStartTimeMS = 8;
    [Range(0, 50)]
    [Tooltip("Burst emitters ignore subsequent collisions for this duration to avoid fluttering from weird physics.")]
    public float _BurstDebounceDurationMS = 25;
    
    public int RandomiseBurstStartIndex { get { return (int)(_RandomiseBurstStartTimeMS * SamplesPerMS); }}
    public int SamplesPerMS { get { return (int)(_SampleRate * .001f); }}
    public int QueueDurationSamples { get { return (int)(_QueueDurationMS * SamplesPerMS); } }

    [Header("Registered Components")]
    public List<HostAuthoring> _Hosts = new();
    protected int _HostCounter = 0;
    public List<EmitterAuthoring> _Emitters = new();
    protected int _EmitterCounter = 0;
    public List<SpeakerAuthoring> _Speakers = new();
    protected List<AudioClip> _AudioClipList = new();

    [Header("Runtime Dynamics")]
    [SerializeField]
    public int _SampleRate = 44100; // TODO make protected and create property
    [SerializeField]
    public int _CurrentDSPSample; // TODO make protected and create property
    [SerializeField]
    protected int _GrainProcessorCount = 0;
    [SerializeField]
    protected int _UnplayedGrainsDestroyed = 0;


    private void Awake()
    {
        Instance = this;
        _MaxSpeakers = AudioSettings.GetConfiguration().numRealVoices;
    }

    public int RegisterAudioClip(AudioClip clip)
    {
        if (!_AudioClipList.Contains(clip))
            _AudioClipList.Add(clip);
        return _AudioClipList.IndexOf(clip);
    }

    public void Start()
    {
        _EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _SampleRate = AudioSettings.outputSampleRate;

        // for (int i = 0; i < _MaxDynamicSpeakers; i++)
        //     CreateSpeaker(_PooledSpeakerPosition);

        _DSPTimerEntity = _EntityManager.CreateEntity();
        _EntityManager.AddComponentData(_DSPTimerEntity, new DSPTimerComponent {
            _NextFrameIndexEstimate = _CurrentDSPSample + QueueDurationSamples,
            _GrainQueueSampleDuration = QueueDurationSamples,
            _RandomiseBurstStartIndex = RandomiseBurstStartIndex });
        #if UNITY_EDITOR
                    _EntityManager.SetName(_DSPTimerEntity, "_DSP Timer");
        #endif

        _GrainQuery = _EntityManager.CreateEntityQuery(typeof(GrainProcessorComponent),typeof(SamplesProcessedTag));

        // ---- CREATE SPEAKER MANAGER
        _Listener = FindObjectOfType<AudioListener>();
        _AttachParamEntity = _EntityManager.CreateEntity();
        _EntityManager.AddComponentData(_AttachParamEntity, new AttachParameterComponent
        {
            _ListenerPos = _Listener.transform.position,
            _ListenerRadius = _ListenerRadius,
            _AttachArcDegrees = _SpeakerAttachArcDegrees,
            _TranslationSmoothing = AttachSmoothing,
            _PooledSpeakerPosition = _PooledSpeakerPosition
        });
        #if UNITY_EDITOR
                    _EntityManager.SetName(_AttachParamEntity, "_Activation Radius");
        #endif

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
        #if UNITY_EDITOR
                    _EntityManager.SetName(windowingBlobEntity, "_Grain Windowing Blob");
        #endif

        // ----   CREATE AUDIO SOURCE BLOB ASSETS AND ASSIGN TO AudioClipDataComponent ENTITIES
        for (int i = 0; i < _AudioClips.Length; i++)
        {
            Entity audioClipDataEntity = _EntityManager.CreateEntity();
            int clipChannels = _AudioClips[i].channels;
            float[] clipData = new float[_AudioClips[i].samples];
            _AudioClips[i].GetData(clipData, 0);
            using BlobBuilder blobBuilder = new BlobBuilder(Allocator.Temp);
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
            _EntityManager.SetName(audioClipDataEntity, "Audio Clip " + i + ": " + _AudioClips[i].name);
#endif
        }
    }

    private void Update()
    {
        // Update DSP timing
        int previousFrameSampleDuration = (int)(Time.deltaTime * _SampleRate);
        DSPTimerComponent dspTimer = _EntityManager.GetComponentData<DSPTimerComponent>(_DSPTimerEntity);
        _EntityManager.SetComponentData(_DSPTimerEntity, new DSPTimerComponent {
            _LastActualDSPIndex = _CurrentDSPSample,
            _NextFrameIndexEstimate = _CurrentDSPSample + (int)(previousFrameSampleDuration * (1 + _DelayByPercentageOfPreviousDuration / 100)),
            _GrainQueueSampleDuration = QueueDurationSamples,
            _PreviousFrameSampleDuration = previousFrameSampleDuration,
            _RandomiseBurstStartIndex = RandomiseBurstStartIndex });
        
        // Update audio listener position
        _EntityManager.SetComponentData(_AttachParamEntity, new AttachParameterComponent
        {
            _ListenerPos = _Listener.transform.position,
            _ListenerRadius = _ListenerRadius,
            _AttachArcDegrees = _SpeakerAttachArcDegrees,
            _TranslationSmoothing = AttachSmoothing,
            _PooledSpeakerPosition = _PooledSpeakerPosition
        });

        NativeArray<Entity> grainEntities = _GrainQuery.ToEntityArray(Allocator.TempJob);
        _GrainProcessorCount = (int)Mathf.Lerp(_GrainProcessorCount, grainEntities.Length, Time.deltaTime * 10f);

        //---- Add new Grains into their associated speaker's unused Grain objects.
        for (int i = 0; i < grainEntities.Length; i++)
        {
            GrainProcessorComponent grain = _EntityManager.GetComponentData<GrainProcessorComponent>(grainEntities[i]);
            int speakerIndex = grain._SpeakerIndex;
            //----  Remove old grains or any pointing to invalid/pooled speakers
            if (speakerIndex >= _Speakers.Count || _Speakers[speakerIndex] == null || grain._StartSampleIndex < _CurrentDSPSample - _DiscardGrainsOlderThanMS * SamplesPerMS)
            {
                // TODO - tell emitter to reset its "LastSampleIndex"?    --- blocked until emitter index added to Grain component. 
                // Debug.LogWarning($"KILLING GRAIN.    Speaker {speakerIndex}.    Current DSP index {_CurrentDSPSample}.   Queue Length Samples {QueueDurationSamples}.    Grain Start Index {grain._StartSampleIndex}.");
                _EntityManager.DestroyEntity(grainEntities[i]);
                _UnplayedGrainsDestroyed++;
                continue;
            }
            else if (_Speakers[grain._SpeakerIndex].GetEmptyGrainDataObject(out GrainData grainDataObject) != null)
            {
                // Populate empty GrainData object with fresh grain and pass it back to the speaker to play
                NativeArray<float> grainSamples = _EntityManager.GetBuffer<GrainSampleBufferElement>(grainEntities[i]).Reinterpret<float>().ToNativeArray(Allocator.Temp);
                try
                {
                    NativeToManagedCopyMemory(grainDataObject._SampleData, grainSamples);
                    grainDataObject._Pooled = false;
                    grainDataObject._IsPlaying = true;
                    grainDataObject._PlayheadIndex = 0;
                    grainDataObject._SizeInSamples = grainSamples.Length;
                    grainDataObject._DSPStartTime = grain._StartSampleIndex;
                    grainDataObject._PlayheadNormalised = grain._PlayheadNorm;
                    _Speakers[grain._SpeakerIndex].GrainDataAdded(grainDataObject);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is NullReferenceException )
                {
                    Debug.LogWarning($"Error while copying grain data to managed array for speaker ({grain._SpeakerIndex}). Killing grain {i}.\n{ex}");
                }
                // Destroy entity once we have sapped it of it's samply goodness and add playback data to speaker grain pool
                _EntityManager.DestroyEntity(grainEntities[i]);
            }
            else
            {
                Debug.Log($"Speaker {_Speakers[grain._SpeakerIndex]} has no empty GrainData objects!");
            }
        }
        grainEntities.Dispose();
    }


    public static unsafe void NativeToManagedCopyMemory(float[] targetArray, NativeArray<float> SourceNativeArray)
    {
        void* memoryPointer = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(SourceNativeArray);
        Marshal.Copy((IntPtr)memoryPointer, targetArray, 0, SourceNativeArray.Length);
    }

    public void CreateSpeaker(Vector3 pos)
    {
        GameObject speaker = Instantiate(_SpeakerPrefab, pos, Quaternion.identity, transform);    
    }

    public void RegisterSpeaker(SpeakerAuthoring speaker)
    {
        // TODO - should use null entry before adding one to the end 
        if (speaker._Registered || _Speakers.Contains(speaker))
            return;
        speaker._SpeakerIndex = _Speakers.Count;
        speaker._Registered = true;
        speaker.name = speaker.name + " - Speaker " + _Speakers.Count;
        _Speakers.Add(speaker);
    }

    public void DeRegisterSpeaker(SpeakerAuthoring speaker)
    {
        // TODO - replace with null and change RegisterSpeaker() to find first null slot.
        // Removing speakers from list will cause issues with indexing.
        _Speakers.Remove(speaker);
    }

    public int RegisterHost(HostAuthoring host)
    {
        _HostCounter++;
        _Hosts.Add(host);
        return _HostCounter - 1;
    }
    public void DeRegisterHost(HostAuthoring host)
    {
        _Hosts.Remove(host);
    }

    public int RegisterEmitter(EmitterAuthoring emitter)
    {
        _EmitterCounter++;
        _Emitters.Add(emitter);
        return _EmitterCounter - 1;
    }
    public void DeRegisterEmitter(EmitterAuthoring emitter)
    {
        _Emitters.Remove(emitter);
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

[Serializable]
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
