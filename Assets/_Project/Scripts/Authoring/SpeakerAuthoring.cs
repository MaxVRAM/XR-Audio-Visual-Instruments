using System.Collections;
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Unity.VisualScripting;


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
public class SpeakerAuthoring : MonoBehaviour
{
    #region FIELDS & PROPERTIES

    public delegate void GrainEmitted(Grain data, int currentDSPSample);
    public event GrainEmitted OnGrainEmitted;

    private EntityManager _EntityManager;
    private Entity _SpeakerEntity;
    private EntityArchetype _SpeakerArchetype;

    private MeshRenderer _MeshRenderer;
    private AudioSource _AudioSource;
    private Grain[] _GrainArray;

    [SerializeField] protected int _SpeakerIndex = int.MaxValue;
    public int SpeakerIndex { get { return _SpeakerIndex; } }
    private int _SampleRate;

    [SerializeField] private bool _EntityInitialised = false;
    [SerializeField] private bool _ManagerInitialised = false;
    [SerializeField] private bool _Active = false;
    [SerializeField] private bool _GrainArrayReady = false;
    [SerializeField] private int _GrainArraySize = 100;
    [SerializeField] private int _NumGrainsFree = 0;
    [SerializeField] private float _TargetVolume = 0;
    [SerializeField] private float _VolumeSmoothing = 4;
    [SerializeField] private float _AttachmentRadius = 1;

    #endregion

    #region GAME OBJECT MANAGEMENT
    
    private void Start()
    {
        _SampleRate = AudioSettings.outputSampleRate;
        _MeshRenderer = gameObject.GetComponentInChildren<MeshRenderer>();
        _AudioSource = gameObject.GetComponent<AudioSource>();
        _AudioSource.rolloffMode = AudioRolloffMode.Custom;
        _AudioSource.maxDistance = 500;
    }

    private void OnDestroy()
    {
        GrainSynth.Instance.DeRegisterSpeaker(this);
        DestroyEntity();
    }

    public void DestroyEntity()
    {
        try
        {
            if (_EntityManager != null && World.All.Count != 0)
                _EntityManager.DestroyEntity(_SpeakerEntity);
        }
        catch (Exception ex) when (ex is NullReferenceException)
        {
            Warning.Info($"Speaker {name} ({_SpeakerIndex}) failed to destroy entity: {ex.Message}");
        }
    }

    #endregion

    #region ENTIY MANAGEMENT

    public void UpdateIndex(int index)
    {
        if (index == int.MaxValue)
            Destroy(gameObject);

        if (_SpeakerIndex != index)
        {
            _SpeakerIndex = index;
            name = $"Speaker.{index}.{transform.parent.name}";
        }
    }

    public bool InitialiseManager()
    {
        if (!_ManagerInitialised)
        {
            _EntityInitialised = false;
            _EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _SpeakerArchetype = _EntityManager.CreateArchetype(
                typeof(SpeakerIndex),
                typeof(Translation),
                typeof(PoolingComponent));
            _ManagerInitialised = true;
            return false;
        }
        else return true;
    }

    public bool InitialiseEntity()
    {
        if (!_EntityInitialised)
        {
            if (!InitialiseManager())
                return false;

            if (SpeakerIndex == int.MaxValue)
                return false;

            if (_SpeakerEntity != Entity.Null)
                _EntityManager.DestroyEntity(_SpeakerEntity);

            Debug.Log($"Speaker {name} creating new entity");
            _SpeakerEntity = _EntityManager.CreateEntity(_SpeakerArchetype);
            _EntityManager.SetComponentData(_SpeakerEntity, new SpeakerIndex { Value = SpeakerIndex });
            _EntityManager.SetComponentData(_SpeakerEntity, new Translation { Value = transform.position });
            _EntityManager.SetComponentData(_SpeakerEntity, new PoolingComponent
            {
                _State = PooledState.Pooled,
                _AttachedHostCount = 0
            });

            InitialiseGrainArray();

#if UNITY_EDITOR
            _EntityManager.SetName(_SpeakerEntity, name);
#endif

            _EntityInitialised = true;
            return false;
        }
        else return true;
    }

    public bool UpdateComponents()
    {
        if (!InitialiseEntity())
            return false;

        ProcessSpeakerIndexComponent(_EntityManager.GetComponentData<SpeakerIndex>(_SpeakerEntity));
        ProcessPoolingComponent(_EntityManager.GetComponentData<PoolingComponent>(_SpeakerEntity));
        ProcessTranslationComponent(_EntityManager.GetComponentData<Translation>(_SpeakerEntity));
        return true;
    }

    #endregion

    #region SECRET SPEAKER COMPONENT BUSINESS

    public void ProcessSpeakerIndexComponent(SpeakerIndex index)
    {
        if (SpeakerIndex != index.Value)
            _EntityManager.SetComponentData(_SpeakerEntity, new SpeakerIndex { Value = SpeakerIndex });
    }

    public void ProcessPoolingComponent(PoolingComponent pooling)
    {
        _AttachmentRadius = pooling._AttachmentRadius;
        transform.localScale = Vector3.one * _AttachmentRadius;

        bool newActiveState = pooling._State == PooledState.Active;

        if (_Active && !newActiveState)
            ResetGrainPool();
        else
            UpdateGrainPool();

        _Active = newActiveState;
        _TargetVolume = _Active ? 1 : 0;
        if (_TargetVolume == 0 && _AudioSource.volume < .005f)
            _AudioSource.volume = 0;
        else
            _AudioSource.volume = Mathf.Lerp(_AudioSource.volume, _TargetVolume, Time.deltaTime * _VolumeSmoothing);
        if (_MeshRenderer != null)
            _MeshRenderer.enabled = _Active;
    }

    public void ProcessTranslationComponent(Translation translation)
    {
        transform.position = translation.Value;
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
        if (!_EntityInitialised || _NumGrainsFree == _GrainArraySize || _GrainArray == null)
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
                        _GrainArrayReady = false;
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







//public void Awake()
//{
//    Debug.Log($"Speaker {Value} in AWAKE and about to check convert component");
//    if (TryGetComponent(out ConvertToEntity converter))
//    {
//        Debug.Log($"Speaker {Value} HAS convert component");
//        converter.ConversionMode = ConvertToEntity.Mode.ConvertAndInjectGameObject;
//    }
//    else
//        Debug.Log($"Speaker {Value} DOES NOT HAVE convert component");
//}

//public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
//{
//    Debug.Log($"Speaker {Value} in CONVERT function. Fixed Speaker: {_IsFixedSpeaker}");
//    _SpeakerEntity = entity;
//    dstManager.AddComponentData(_SpeakerEntity, new SpeakerIndex { Value = SpeakerIndex });

//    #if UNITY_EDITOR
//            dstManager.SetName(_SpeakerEntity, "Speaker " + Value + " (Dynamic) ");
//    #endif

//    dstManager.AddComponentData(_SpeakerEntity, new PoolingComponent {
//        _State = PooledState.Pooled,
//        _AttachedHostCount = 0
//    });

//    _EntityInitialised = true;
//}

//public void Start()
//{
//    Value = GrainSynth.Instance.RegisterSpeaker(this);
//    _Registered = true;
//    name = transform.parent.name + " - Speaker " + Value;

//    _SampleRate = AudioSettings.outputSampleRate;
//    _MeshRenderer = gameObject.GetComponentInChildren<MeshRenderer>();
//    _AudioSource = gameObject.GetComponent<AudioSource>();
//    _AudioSource.rolloffMode = AudioRolloffMode.Custom;
//    _AudioSource.maxDistance = 500;


//    if (_IsFixedSpeaker)
//    {
//        _EntityInitialised = true;

//        if (_SpeakerEntity != null)
//        {
//            Debug.Log($"Speaker {Value} in FIXED, but is associated with Entity. WHY!!");
//        }
//    }
//    else
//    {
//        if (_SpeakerEntity == null)
//        {
//            Debug.Log($"Speaker {Value} in DYNAMIC, but has no Entity. PLEASE KILL ME!!");
//        }
//    }

//    InitialiseGrainArray();
//}

//public void Update()
//{
//    if (!_EntityInitialised)
//        return;

//    _FixedHosts.RemoveAll(item => item == null);

//    if (_EntityManager == null || _SpeakerEntity == null)
//        return;

//    ProcessSpeakerIndexComponent(_EntityManager.GetComponentData<SpeakerIndex>(_SpeakerEntity));
//    ProcessPoolingComponent(_EntityManager.GetComponentData<PoolingComponent>(_SpeakerEntity));
//    ProcessTranslationComponent(_EntityManager.GetComponentData<Translation>(_SpeakerEntity));
//}