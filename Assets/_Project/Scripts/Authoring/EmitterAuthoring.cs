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
public class EmitterAuthoring : MonoBehaviour, IConvertGameObjectToEntity
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
    public SpeakerAuthoring _LinkedSpeaker;
    public int _LinkedSpeakerIndex;
    public bool _LinkedToSpeaker = false;
    protected bool _FixedSpeakerLink = false;


    [Header("Runtime Dynamics")]
    public bool _IsPlaying = true;
    public bool _IsTriggered = false;
    public GameObject _PrimaryObject;
    public GameObject _SecondaryObject;
    public Collision _LatestCollision;
    public bool _InListenerRadius = false;
    public bool _IsWithinEarshot = true;
    public float _CurrentDistance = 0;
    public float _DistanceVolume = 0;
    public float _TimeExisted = 0;


    public DSP_Class[] _DSPChainParams;

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
            _PrimaryObject = transform.parent.gameObject;

        InitialiseTypeAndInteractions();
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
            _EntityManager.AddComponent<InListenerRadiusTag>(_EmitterEntity);
        }
        else
        {
            _IsWithinEarshot = false;
            _EntityManager.RemoveComponent<InListenerRadiusTag>(_EmitterEntity);
        }

        if (_IsPlaying)
            _EntityManager.AddComponent<PlayingTag>(_EmitterEntity);
        else
            _EntityManager.RemoveComponent<PlayingTag>(_EmitterEntity);

        _DistanceVolume = AudioUtils.EmitterFromListenerVolumeAdjust(_HeadPosition.position, transform.position, _MaxAudibleDistance);
        #endregion

        UpdateProperties();
        UpdateDSPEffectsChain();

        // Burst emitters only need a single pass to generate grain data for its duration.
        if (_EmitterType == EmitterType.Burst) 
            _IsPlaying = false;

        Translation trans = _EntityManager.GetComponentData<Translation>(_EmitterEntity);
        _EntityManager.SetComponentData(_EmitterEntity, new Translation { Value = transform.position });
    }

    public void ResetEmitter(GameObject primaryObject, GameObject secondaryObject, Collision collision)
    {
        _TimeExisted = 0;
        gameObject.transform.localPosition = Vector3.zero;

        if (primaryObject != null)
            _PrimaryObject = primaryObject;
        if (secondaryObject != null)
            _SecondaryObject = secondaryObject;
        if (collision != null)
        {
            _SecondaryObject = collision.collider.gameObject;
            _LatestCollision = collision;
        }

        InitialiseTypeAndInteractions();

        if ((_EmitterType == EmitterType.Burst || _ContactEmitter) && _LatestCollision == null)
        {
            _IsPlaying = false;
            _IsTriggered = false;
        }
        else
        {
            _IsPlaying = true;
            _IsTriggered = true;
        }
    }

    public void UpdateLinkedSpeaker(SpeakerAuthoring speaker)
    {
        _LinkedSpeaker = speaker;
        _LinkedToSpeaker = speaker != null ? true : false;
        _FixedSpeakerLink = speaker != null ? true : false;
    }

    public virtual void InitialiseTypeAndInteractions() { }
    protected virtual void UpdateProperties() { }
    public SpeakerAuthoring DynamicallyLinkedSpeaker { get { return GrainSynth.Instance._GrainSpeakers[_LinkedSpeakerIndex]; } }

    public void UpdateCollision(Collision collision)
    {
        if (collision == null)
        {
            _IsTriggered = false;
            _VolumeMultiply = 1;
        }
        else
        {
            _IsPlaying = true;
            if (!_ColliderRigidityVolumeScale)
                _VolumeMultiply = 1;
            else if (collision.collider.GetComponent<SurfaceParameters>() != null)
                _VolumeMultiply = collision.collider.GetComponent<SurfaceParameters>()._Rigidity;
            else
                _VolumeMultiply = 1;
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
        Gizmos.color = _InListenerRadius ? Color.yellow : Color.blue;
        Gizmos.DrawSphere(transform.position, .1f);
    }

    public virtual void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) { }

    protected virtual void UpdateCollisionNumbers(int currentCollisionCount) { }
}
