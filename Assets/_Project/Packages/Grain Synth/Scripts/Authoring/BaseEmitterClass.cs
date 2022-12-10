using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Transforms;
using Random = UnityEngine.Random;


[DisallowMultipleComponent]
[RequiresEntityConversion]
[RequireComponent(typeof(ConvertToEntity))]
public class BaseEmitterClass : MonoBehaviour, IConvertGameObjectToEntity
{
    public enum EmitterType {Continuous, Burst}

    protected bool _Initialised = false;
    protected Entity _EmitterEntity;
    protected EntityManager _EntityManager;
    protected Transform _HeadPosition;
    protected float[] _PerlinSeedArray;
    protected float _VolumeMultiply = 1;
    protected float _SamplesPerMS = 0;

    [Header("Emitter Configuration")]
    public int _ClipIndex = 0;
    public EmitterType _EmitterType;
    public bool _ContactEmitter = false;

    [Header("Playback Config")]
    [Range(0.1f, 50f)]
    public float _MaxAudibleDistance = 10f;
    public bool _PingPongAtEndOfClip = true;
    public bool _ColliderRigidityVolumeScale = false;

    [Header("Speaker Configuration")]
    public GrainSpeakerAuthoring _LinkedSpeaker;
    public int _LinkedSpeakerIndex;
    public bool _LinkedToSpeaker = false;
    protected bool _StaticallyLinked = false;


    [Header("Runtime Dynamics")]
    public bool _IsPlaying = true;
    public bool _IsTriggered = false;
    public GameObject _PrimaryObject;
    public GameObject _SecondaryObject;
    protected bool _InSpeakerRange = false;
    public bool _IsWithinEarshot = true;
    public float _CurrentDistance = 0;
    public float _DistanceVolume = 0;
    public float _TimeExisted = 0;

    //[SerializeField]
    //protected GameObject _CollidingDummyEmitterGameObject;
    //protected List<GameObject> _RemoteInteractions;

    public DSPBase[] _DSPChainParams;


    void Start()
    {
        _SamplesPerMS = AudioSettings.outputSampleRate * 0.001f;
        _EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _HeadPosition = FindObjectOfType<Camera>().transform;

        _PerlinSeedArray = new float[10];
        for (int i = 0; i < _PerlinSeedArray.Length; i++)
        {
            float offset = Random.Range(0, 1000);
            _PerlinSeedArray[i] = Mathf.PerlinNoise(offset, offset * 0.5f);
        }

        if (_PrimaryObject == null)
            _PrimaryObject = this.transform.parent.gameObject;

        Initialise();
    }

    public void Awake()
    {
        GetComponent<ConvertToEntity>().ConversionMode = ConvertToEntity.Mode.ConvertAndInjectGameObject;
    }

    public void DestroyEntity()
    {
        if (World.All.Count != 0 && _EmitterEntity != null)
            _EntityManager.DestroyEntity(_EmitterEntity);
    }

    private void OnDestroy()
    {
        DestroyEntity();
    }

    private void Update()
    {
        if (!_Initialised)
            return;

        _TimeExisted += Time.deltaTime;
        _SamplesPerMS = AudioSettings.outputSampleRate * 0.001f;

        #region DETERMINE PLAYBACK STATUS AND ADD COMPONENTS TO EMITTER ENTITY
        if ((_ContactEmitter && !_IsTriggered))
        {
            _IsPlaying = false;
        }

        _CurrentDistance = Mathf.Abs((_HeadPosition.position - transform.position).magnitude);

        if (_CurrentDistance < _MaxAudibleDistance)
        {
            _IsWithinEarshot = true;
            _EntityManager.AddComponent<WithinEarshot>(_EmitterEntity);
        }
        else
        {
            _IsWithinEarshot = false;
            _EntityManager.RemoveComponent<WithinEarshot>(_EmitterEntity);
        }

        if (_IsPlaying)
            _EntityManager.AddComponent<IsPlayingTag>(_EmitterEntity);
        else
            _EntityManager.RemoveComponent<IsPlayingTag>(_EmitterEntity);

        _DistanceVolume = AudioUtils.EmitterFromListenerVolumeAdjust(_HeadPosition.position, transform.position, _MaxAudibleDistance);
        #endregion

        UpdateProperties();
        UpdateDSPEffectsChain();

        // Burst emitters only need a single pass to generate grain data for its duration.
        if (_EmitterType == EmitterType.Burst) 
        {
            _IsPlaying = false;
        }
        Translation trans = _EntityManager.GetComponentData<Translation>(_EmitterEntity);
        _EntityManager.SetComponentData(_EmitterEntity, new Translation { Value = transform.position });
    }

    public void ResetEmitter(GameObject secondaryObject, GrainSpeakerAuthoring speaker)
    {
        _TimeExisted = 0;
        _IsPlaying = true;
        _IsTriggered = true;
        _StaticallyLinked = true;
        _LinkedSpeaker = speaker;
        _SecondaryObject = secondaryObject;
        gameObject.transform.localPosition = Vector3.zero;
        Initialise();
    }

    public virtual void Initialise() { }
    protected virtual void UpdateProperties() { }

    public virtual void SetupContactEmitter(Collision collision, GrainSpeakerAuthoring speaker) { }
    public virtual void SetupAttachedEmitter(GameObject primaryObject, GameObject secondaryObject, GrainSpeakerAuthoring speaker) { }

    public GrainSpeakerAuthoring DynamicallyLinkedSpeaker { get { return GrainSynth.Instance._GrainSpeakers[_LinkedSpeakerIndex]; } }

    // Only for burst emitters
    public virtual void NewCollision(Collision collision) { }

    // Only for continuous emitters
    public void UpdateCurrentCollisionStatus(Collision collision)
    {
        if (collision == null)
        {
            _IsTriggered = false;
            _VolumeMultiply = 1;
        }
        else
        {
            _IsTriggered = true;
            if (!_ColliderRigidityVolumeScale)
                _VolumeMultiply = 1;
            else if (collision.collider.GetComponent<SurfaceParameters>() != null)
                _VolumeMultiply = collision.collider.GetComponent<SurfaceParameters>()._Rigidity;
        }
    }

    protected void UpdateDSPEffectsChain(bool clear = true)
    {
        //--- TODO not sure if clearing and adding again is the best way to do this
        DynamicBuffer<DSPParametersElement> dspBuffer = _EntityManager.GetBuffer<DSPParametersElement>(_EmitterEntity);

        if (clear) dspBuffer.Clear();

        for (int i = 0; i < _DSPChainParams.Length; i++)
        {
            dspBuffer.Add(_DSPChainParams[i].GetDSPBufferElement());
        }
    }

    public float GeneratePerlinForParameter(int parameterIndex)
    {
        return Mathf.PerlinNoise(Time.time + _PerlinSeedArray[parameterIndex], (Time.time + _PerlinSeedArray[parameterIndex]) * 0.5f);
    }

    protected void OnDrawGizmos()
    {
        Gizmos.color = _InSpeakerRange ? Color.yellow : Color.blue;
        Gizmos.DrawSphere(transform.position, .1f);
    }

    public virtual void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) { }

    protected virtual void UpdateCollisionNumbers(int currentCollisionCount) { }
}
