using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.VisualScripting;
using static UnityEngine.EventSystems.EventTrigger;
using Unity.Transforms;

// PROJECT AUDIO CONFIGURATION NOTES
// ---------------------------------
// DSP Buffer size in audio settings
// Best performance - 46.43991
// Good latency - 23.21995
// Best latency - 11.60998

// TODO - FIX WINDOWING FUNCTIONS:
// https://michaelkrzyzaniak.com/AudioSynthesis/2_Audio_Synthesis/11_Granular_Synthesis/1_Window_Functions/


[RequireComponent(typeof(AudioSource))]
public class GrainSynth : MonoBehaviour
{
    #region FIELDS & PROPERTIES

    public static GrainSynth Instance;

    private EntityManager _EntityManager;
    private EntityQuery _GrainQuery;

    private Entity _WindowingEntity;
    private Entity _AudioTimerEntity;
    private Entity _AttachmentEntity;

    public static class EntityType
    {
        public readonly static string Attachment = "_AttachmentParameters";
        public readonly static string Windowing = "_WindowingBlob";
        public readonly static string AudioTimer = "_AudioTimer";
        public readonly static string AudioClip = "_AudioClip";
    }

    private AudioListener _Listener;

    [Header("Runtime Dynamics")]
    [SerializeField] public int _SampleRate = 44100; // TODO make private and create property
    [SerializeField] public int _CurrentDSPSample; // TODO make private and create property
    [SerializeField] private int _GrainProcessorCount = 0;
    [SerializeField] private int _DiscardedGrains = 0;
    [SerializeField] private int _LastFrameSampleDuration = 0;


    [Header(header: "Speaker Configuration")]
    public SpeakerAuthoring _SpeakerPrefab;
    [SerializeField] private int _MaxSpeakers;
    [SerializeField] private Transform _SpeakerSpawnTransform;
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

    [Header(header: "Interaction Behaviour")]
    [Tooltip("During collision/contact between two emitter hosts, only trigger the emitter with the greatest surface rigidity, using an average of the two values.")]
    public bool _OnlyTriggerMostRigidSurface = true;

    [Header("DSP Config")]
    [Tooltip("Additional ms to calculate and queue grains each frame. Set to 0, the grainComponent queue equals the previous frame's duration. Adds latency, but help to avoid underrun. Recommended values > 20ms.")]
    [Range(0, 100)] public float _QueueDurationMS = 22;

    [Tooltip("Percentage of previous frame duration to delay grainComponent start times of next frame. Adds a more predictable amount of latency to help avoid timing issues when the framerate slows.")]
    [Range(0, 100)] public float _DelayPercentLastFrame = 10;

    [Tooltip("Discard unplayed grains with a DSP start index more than this value (ms) in the past. Prevents clustered grainComponent playback when resources are near their limit.")]
    [Range(0, 60)] public float _DiscardGrainsOlderThanMS = 10;

    [Tooltip("Delay bursts triggered on the same frame by a random amount. Helps avoid phasing issues caused by identical emitters triggered together.")]
    [Range(0, 40)] public float _BurstStartOffsetRangeMS = 8;

    [Tooltip("Burst emitters ignore subsequent collisions for this duration to avoid fluttering from weird physics.")]
    [Range(0, 50)] public float _BurstDebounceDurationMS = 25;

    private int _SamplesPerMS = 0;
    public int SamplesPerMS { get { return _SamplesPerMS; } set { _SamplesPerMS = (int)(_SampleRate * .001f); } }
    public int QueueDurationSamples { get { return (int)(_QueueDurationMS * SamplesPerMS); } }
    public int BurstStartOffsetRange { get { return (int)(_BurstStartOffsetRangeMS * SamplesPerMS); } }
    public int GrainDiscardSampleIndex { get { return _CurrentDSPSample - (int)(_DiscardGrainsOlderThanMS * SamplesPerMS); } }
    public int NextFrameIndexEstimate { get { return _CurrentDSPSample + (int)(_LastFrameSampleDuration * (1 + _DelayPercentLastFrame / 100)); } }

    [Header("Registered Synth Elements")]
    public List<HostAuthoring> _Hosts = new List<HostAuthoring>();
    private int _HostCounter = 0;
    public List<EmitterAuthoring> _Emitters = new List<EmitterAuthoring>();
    private int _EmitterCounter = 0;
    public List<SpeakerAuthoring> _Speakers = new List<SpeakerAuthoring>();

    [Header("Audio Clip Library")]
    public AudioClip[] _AudioClips;
    protected List<AudioClip> _AudioClipList = new List<AudioClip>();

    #endregion

    #region UPDATE SCHEDULE

    private void Awake()
    {
        Instance = this;
        _MaxSpeakers = AudioSettings.GetConfiguration().numRealVoices;
    }

    public void Start()
    {
        _SampleRate = AudioSettings.outputSampleRate;
        _Listener = FindObjectOfType<AudioListener>();
        if (_SpeakerSpawnTransform == null)
        {
            GameObject go = new GameObject($"SpeakerContainer");
            go.transform.parent = gameObject.transform;
            go.transform.position = Vector3.down * 10;
            _SpeakerSpawnTransform = go.transform;
        }

        _EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _GrainQuery = _EntityManager.CreateEntityQuery(typeof(GrainComponent), typeof(SamplesProcessedTag));

        _WindowingEntity = UpdateEntity(_WindowingEntity, EntityType.Windowing);
        _AudioTimerEntity = UpdateEntity(_AudioTimerEntity, EntityType.AudioTimer);
        _AttachmentEntity = UpdateEntity(_AttachmentEntity, EntityType.Attachment);
        PopulateAudioClipEntities(EntityType.AudioClip);
    }

    private void Update()
    {
        _LastFrameSampleDuration = (int)(Time.deltaTime * _SampleRate);

        UpdateEntity(_AudioTimerEntity, EntityType.AudioTimer);
        UpdateEntity(_AttachmentEntity, EntityType.Attachment);

        SpeakerUpkeep();
        UpdateSpeakers();

        DistributeGrains();

        UpdateHosts();
    }

    #endregion

    #region COMPONENT UPDATES

    public bool EntityTypeExists(string entityType)
    {
        foreach (FieldInfo field in typeof(EntityType).GetFields())
            if (field.GetValue(null).ToString() == entityType)
                return true;
        Debug.Log($"Could not update entity of unknown type {entityType}");
        return false;
    }

    private Entity CreateEntity(string entityType)
    {
        Entity entity = _EntityManager.CreateEntity();

#if UNITY_EDITOR
        _EntityManager.SetName(entity, entityType);
#endif
        return entity;
    }


    private Entity UpdateEntity(Entity entity, string entityType)
    {
        if (!EntityTypeExists(entityType) || entityType == EntityType.AudioClip)
            return entity;

        entity = entity != Entity.Null ? entity : CreateEntity(entityType);

        if (entityType == EntityType.Windowing)
            PopulateWindowingEntity(entity);
        else if (entityType == EntityType.Attachment)
            PopulateAttachmentEntity(entity);
        else if (entityType == EntityType.AudioTimer)
            PopulateTimerEntity(entity);

        return entity;
    }

    private void PopulateTimerEntity(Entity entity)
    {
        if (_EntityManager.HasComponent<AudioTimerComponent>(entity))
            _EntityManager.SetComponentData(entity, new AudioTimerComponent
            {
                _LastActualDSPIndex = _CurrentDSPSample,
                _NextFrameIndexEstimate = NextFrameIndexEstimate,
                _GrainQueueSampleDuration = QueueDurationSamples,
                _PreviousFrameSampleDuration = _LastFrameSampleDuration,
                _RandomiseBurstStartIndex = BurstStartOffsetRange
            });
        else
            _EntityManager.AddComponentData(entity, new AudioTimerComponent
            {
                _LastActualDSPIndex = _CurrentDSPSample,
                _NextFrameIndexEstimate = NextFrameIndexEstimate,
                _GrainQueueSampleDuration = QueueDurationSamples,
                _PreviousFrameSampleDuration = _LastFrameSampleDuration,
                _RandomiseBurstStartIndex = BurstStartOffsetRange
            });
    }

    private void PopulateAttachmentEntity(Entity entity)
    {
        if (_EntityManager.HasComponent<AttachmentComponent>(entity))
            _EntityManager.SetComponentData(entity, new AttachmentComponent
            {
                _ListenerPos = _Listener.transform.position,
                _ListenerRadius = _ListenerRadius,
                _AttachArcDegrees = _SpeakerAttachArcDegrees,
                _TranslationSmoothing = AttachSmoothing,
                _PooledSpeakerPosition = _SpeakerSpawnTransform.position
            });
        else
            _EntityManager.AddComponentData(entity, new AttachmentComponent
            {
                _ListenerPos = _Listener.transform.position,
                _ListenerRadius = _ListenerRadius,
                _AttachArcDegrees = _SpeakerAttachArcDegrees,
                _TranslationSmoothing = AttachSmoothing,
                _PooledSpeakerPosition = _SpeakerSpawnTransform.position
            });
    }

    private void PopulateWindowingEntity(Entity entity)
    {
        using (BlobBuilder blobTheBuilder = new BlobBuilder(Allocator.Temp))
        {
            ref FloatBlobAsset windowingBlobAsset = ref blobTheBuilder.ConstructRoot<FloatBlobAsset>();
            BlobBuilderArray<float> windowArray = blobTheBuilder.Allocate(ref windowingBlobAsset.array, 512);

            for (int i = 0; i < windowArray.Length; i++)
                windowArray[i] = 0.5f * (1 - Mathf.Cos(2 * Mathf.PI * i / windowArray.Length));

            BlobAssetReference<FloatBlobAsset> windowingBlobAssetRef = blobTheBuilder.CreateBlobAssetReference<FloatBlobAsset>(Allocator.Persistent);
            _EntityManager.AddComponentData(entity, new WindowingDataComponent { _WindowingArray = windowingBlobAssetRef });
        }
    }

    private void PopulateAudioClipEntities(string entityName)
    {
        for (int i = 0; i < _AudioClips.Length; i++)
        {
            Entity audioClipDataEntity = _EntityManager.CreateEntity();

            int clipChannels = _AudioClips[i].channels;
            float[] clipData = new float[_AudioClips[i].samples];
            _AudioClips[i].GetData(clipData, 0);
            
            using BlobBuilder blobBuilder = new BlobBuilder(Allocator.Temp);
            ref FloatBlobAsset audioClipBlobAsset = ref blobBuilder.ConstructRoot<FloatBlobAsset>();
            BlobBuilderArray<float> audioClipArray = blobBuilder.Allocate(ref audioClipBlobAsset.array, (clipData.Length / clipChannels));
            BuildAudioClipBlob(ref audioClipArray, clipData, clipChannels);
            BlobAssetReference<FloatBlobAsset> audioClipBlobAssetRef = blobBuilder.CreateBlobAssetReference<FloatBlobAsset>(Allocator.Persistent);
            _EntityManager.AddComponentData(audioClipDataEntity, new AudioClipDataComponent { _ClipDataBlobAsset = audioClipBlobAssetRef, _ClipIndex = i });

#if UNITY_EDITOR
            _EntityManager.SetName(audioClipDataEntity, entityName + "." + i + "." + _AudioClips[i].name);
#endif
        }
    }

    private void BuildAudioClipBlob(ref BlobBuilderArray<float> audioBlob, float[] samples, int channels)
    {
        for (int s = 0; s < samples.Length - 1; s += channels)
        {
            audioBlob[s / channels] = 0;
            // TODO: Possibly worth investigating multi-channel audio assets. Currently mono-summing everything.
            // TODO: Check if have to divide amplitude by channels to avoid clipping.
            for (int c = 0; c < channels; c++)
                audioBlob[s / channels] += samples[s + c];
        }
    }

    #endregion

    #region PROCESS GRAINS

    private void DistributeGrains()
    {
        if (_Speakers.Count == 0)
        {
            _EntityManager.DestroyEntity(_GrainQuery);
            return;
        }

        NativeArray<Entity> grainEntities = _GrainQuery.ToEntityArray(Allocator.TempJob);
        _GrainProcessorCount = (int)Mathf.Lerp(_GrainProcessorCount, grainEntities.Length, Time.deltaTime * 10f);

        GrainComponent grainComponent;

        for (int i = 0; i < grainEntities.Length; i++)
        {
            grainComponent = _EntityManager.GetComponentData<GrainComponent>(grainEntities[i]);
            int speakerIndex = grainComponent._SpeakerIndex;

            SpeakerAuthoring speaker = GetSpeakerForGrain(grainComponent);
            if (speaker != null && speaker.GetEmptyGrain(out Grain grain) != null)
            {
                try
                {
                    NativeArray<float> grainSamples = _EntityManager.GetBuffer<GrainSampleBufferElement>(grainEntities[i]).Reinterpret<float>().ToNativeArray(Allocator.Temp);
                    NativeToManagedCopyMemory(grain._SampleData, grainSamples);
                    grain._Pooled = false;
                    grain._IsPlaying = true;
                    grain._PlayheadIndex = 0;
                    grain._SizeInSamples = grainSamples.Length;
                    grain._DSPStartTime = grainComponent._StartSampleIndex;
                    grain._PlayheadNormalised = grainComponent._PlayheadNorm;
                    speaker.GrainAdded(grain);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is NullReferenceException)
                {
                    Debug.LogWarning($"Error while copying grain to managed array for speaker ({speakerIndex}). Destroying grain entity {i}.\n{ex}");
                }
            }
            _EntityManager.DestroyEntity(grainEntities[i]);
        }
        grainEntities.Dispose();
    }

    public static unsafe void NativeToManagedCopyMemory(float[] targetArray, NativeArray<float> SourceNativeArray)
    {
        void* memoryPointer = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(SourceNativeArray);
        Marshal.Copy((IntPtr)memoryPointer, targetArray, 0, SourceNativeArray.Length);
    }

    #endregion

    #region SYNTH ELEMENT UPDATES

    public void UpdateSpeakers()
    {
        foreach (SpeakerAuthoring speaker in _Speakers)
        {
            speaker.UpdateComponents();
        }
    }

    public void UpdateHosts()
    {
        foreach (HostAuthoring host in _Hosts)
        {
            host.UpdateComponents();
        }
    }

    #endregion

    #region SYNTH ELEMENT MANAGEMENT

    public SpeakerAuthoring CreateSpeaker(Vector3 pos)
    {
        if (_Speakers.Count < _MaxDynamicSpeakers)
            return Instantiate(_SpeakerPrefab, _SpeakerSpawnTransform.position, Quaternion.identity, _SpeakerSpawnTransform);
        return null;
    }

    public void SpeakerUpkeep()
    {
        while (_Speakers.Count < _MaxDynamicSpeakers)
                _Speakers.Add(CreateSpeaker(_SpeakerSpawnTransform.position));
        while (_Speakers.Count > _MaxDynamicSpeakers)
            _Speakers.RemoveAt(_Speakers.Count - 1);

        for (int i = 0; i < _Speakers.Count; i++)
        {
            if (_Speakers[i] == null)
                _Speakers[i] = CreateSpeaker(_SpeakerSpawnTransform.position);
            _Speakers[i].UpdateIndex(i);
        }
    }

    public int RegisterSpeaker(SpeakerAuthoring speaker)
    {
        if (!_Speakers.Contains(speaker))
            _Speakers.Add(speaker);
        return _Speakers.IndexOf(speaker);
    }

    public bool GetSpeakerFromIndex(int index, out SpeakerAuthoring speaker)
    {
        if (index >= _Speakers.Count)
            speaker = null;
        else
            speaker = _Speakers[index];
        return speaker != null;
    }

    public SpeakerAuthoring GetSpeakerForGrain(GrainComponent grain)
    {
        if (!GetSpeakerFromIndex(grain._SpeakerIndex, out SpeakerAuthoring speaker) || grain._SpeakerIndex == int.MaxValue ||
            grain._StartSampleIndex < GrainDiscardSampleIndex)
        {
            _DiscardedGrains++;
            return null;
        }
        return speaker;
    }

    public int GetIndexOfSpeaker(SpeakerAuthoring speaker)
    {
        int index = _Speakers.IndexOf(speaker);
        if (index == -1 || index >= _Speakers.Count)
            return -1;
        return _Speakers.IndexOf(speaker);
    }

    public void DeRegisterSpeaker(SpeakerAuthoring speaker)
    {
        //if (_Speakers.Remove(speaker))
        //    Debug.Log($"{speaker.name} has been deregistered.");
        //else
        //    Debug.Log($"{speaker.name} with index of {speaker.SpeakerIndex} attempting to deregister but not in list.");
    }

    public int RegisterHost(HostAuthoring host)
    {
        _Hosts.Remove(host);
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

    #endregion

    #region AUDIO DSP CLOCK

    void OnAudioFilterRead(float[] data, int channels)
    {
        for (int dataIndex = 0; dataIndex < data.Length; dataIndex += channels)
            _CurrentDSPSample++;
    }

    #endregion

    #region GIZMOS

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
            return;
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(_Listener.transform.position, _ListenerRadius);
    }

    #endregion
}

#region AUDIO CLIP LIBRARY

// TODO: Create AudioClipSource and AudioClipLibrary objects to add properties to source
// content, making it much easier to create emitter configurations. Adding properties like
// tagging/grouping, custom names/descriptions, and per-clip processing; like volume,
// compression, and eq; are feasible and would drastically benefit workflow.

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

#endregion
