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
    protected float[] _PerlinSeedArray;
    protected float _VolumeMultiply = 1;
    protected float _SamplesPerMS = 0;
    protected int _LastSampleIndex = 0;

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
    public SpeakerAuthoring _Speaker;
    public int _SpeakerIndex;


    [Header("Runtime Dynamics")]
    public bool _IsPlaying = true;
    public GameObject _PrimaryObject;
    public GameObject _SecondaryObject;
    public Collision _LatestCollision;
    public bool _InListenerRadius = false;
    public float _CurrentDistance = 0;
    public float _DistanceAmplitude = 0;
    public float _TimeExisted = 0;


    public DSP_Class[] _DSPChainParams;

    void Start()
    {
        _EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _SamplesPerMS = AudioSettings.outputSampleRate * 0.001f;

        _PerlinSeedArray = new float[10];
        for (int i = 0; i < _PerlinSeedArray.Length; i++)
        {
            float offset = Random.Range(0, 1000);
            _PerlinSeedArray[i] = Mathf.PerlinNoise(offset, offset * 0.5f);
        }
    }

    public void Awake()
    {
        GetComponent<ConvertToEntity>().ConversionMode = ConvertToEntity.Mode.ConvertAndInjectGameObject;
    }

    private void Update()
    {
        // if (!_Initialised)
        //     return;

        // _TimeExisted += Time.deltaTime;
        // _SamplesPerMS = AudioSettings.outputSampleRate * 0.001f;

        // #region UPDATE TAGS FOR GRAIN SYNTH SYSTEM
        // if (_CurrentDistance < _MaxAudibleDistance)
        // {
        //     _InListenerRadius = true;
        //     _EntityManager.AddComponent<InListenerRadiusTag>(_EmitterEntity);
        // }
        // else
        // {
        //     _InListenerRadius = false;
        //     _EntityManager.RemoveComponent<InListenerRadiusTag>(_EmitterEntity);
        // }

        // if (_IsPlaying)
        //     _EntityManager.AddComponent<PlayingTag>(_EmitterEntity);
        // else
        //     _EntityManager.RemoveComponent<PlayingTag>(_EmitterEntity);

        // #endregion

        // UpdateEntity();
        // UpdateDSPEffectsChain();

        // Translation trans = _EntityManager.GetComponentData<Translation>(_EmitterEntity);
        // _EntityManager.SetComponentData(_EmitterEntity, new Translation { Value = transform.position });
    }

    public void ManualUpdate()
    {
        if (!_Initialised)
            return;

        _TimeExisted += Time.deltaTime;
        _SamplesPerMS = AudioSettings.outputSampleRate * 0.001f;

        #region UPDATE TAGS FOR GRAIN SYNTH SYSTEM
        if (_InListenerRadius) _EntityManager.AddComponent<InListenerRadiusTag>(_EmitterEntity);
        else _EntityManager.RemoveComponent<InListenerRadiusTag>(_EmitterEntity);

        if (_IsPlaying)
            _EntityManager.AddComponent<PlayingTag>(_EmitterEntity);
        else
            _EntityManager.RemoveComponent<PlayingTag>(_EmitterEntity);

        Translation trans = _EntityManager.GetComponentData<Translation>(_EmitterEntity);
        _EntityManager.SetComponentData(_EmitterEntity, new Translation { Value = transform.position });

        #endregion

        UpdateEntity();
        UpdateDSPEffectsChain();

    }


    public bool UpdateDistanceFromListener(float distance)
    {
        _CurrentDistance = distance;
        _DistanceAmplitude = AudioUtils.ListenerDistanceVolume(distance, _MaxAudibleDistance);

        if (_CurrentDistance < _MaxAudibleDistance) _InListenerRadius = true;
        else _InListenerRadius = false;
        return _InListenerRadius;
    }

    public void SetAttachedSpeaker(int index)
    {
        _SpeakerIndex = index;
        if (index != int.MaxValue)
            _Speaker = GrainSynth.Instance._GrainSpeakers[_SpeakerIndex];
        _LastSampleIndex = GrainSynth.Instance._CurrentDSPSample;
    }

    protected virtual void UpdateEntity() { }

    public void UpdateCollision(Collision collision)
    {
        if (_EmitterType == EmitterType.Burst || _ContactEmitter)
            if (collision == null)
            {
                _IsPlaying = false;
                _VolumeMultiply = 1;
            }
            else
            {
                _IsPlaying = true;
                if (_ColliderRigidityVolumeScale && collision.collider.GetComponent<SurfaceParameters>() != null)
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
            dspBuffer.Add(_DSPChainParams[i].GetDSPBufferElement());
    }

    public float GeneratePerlinForParameter(int parameterIndex)
    {
        return Mathf.PerlinNoise(
            Time.time + _PerlinSeedArray[parameterIndex],
            (Time.time + _PerlinSeedArray[parameterIndex]) * 0.5f);
    }

    protected void OnDrawGizmos()
    {
        Gizmos.color = _InListenerRadius ? Color.yellow : Color.blue;
        Gizmos.DrawSphere(transform.position, .1f);
    }

    public virtual void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) { }

    protected virtual void UpdateCollisionNumbers(int currentCollisionCount) { }

    private void OnDestroy()
    {
        DestroyEntity();
    }

    public void DestroyEntity()
    {
        if (World.All.Count != 0 && _EmitterEntity != null)
            _EntityManager.DestroyEntity(_EmitterEntity);
    }
}
