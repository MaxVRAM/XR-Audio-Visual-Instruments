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

    protected Entity _EmitterEntity;
    protected EntityManager _EntityManager;
    protected float _VolumeMultiply = 1;
    protected float[] _PerlinSeedArray;
    protected int _LastSampleIndex = 0;
    protected float _SamplesPerMS = 0;
    protected bool _Initialised = false;

    [Header("Emitter Configuration")]
    public EmitterType _EmitterType;
    public bool _ContactEmitter = false;
    public int _ClipIndex = 0;

    [Header("Playback Config")]
    [Range(0.1f, 50f)]
    public float _MaxAudibleDistance = 10f;
    public bool _PingPongGrainPlayheads = true;
    public bool _ColliderRigidityVolumeScale = false;

    [Header("Runtime Dynamics")]
    public int _SpeakerIndex;
    public bool _IsPlaying = true;
    public float _TimeExisted = 0;
    public float _CurrentDistance = 0;
    public float _DistanceAmplitude = 0;
    public bool _InListenerRadius = false;

    public DSP_Class[] _DSPChainParams;


    public virtual void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) { }

    public void Awake()
    {
        GetComponent<ConvertToEntity>().ConversionMode = ConvertToEntity.Mode.ConvertAndInjectGameObject;
    }

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

    private void Update() { }

    public void ManualUpdate()
    {
        if (!_Initialised)
            return;

        _TimeExisted += Time.deltaTime;
        _SamplesPerMS = AudioSettings.outputSampleRate * 0.001f;

        if (_InListenerRadius) _EntityManager.AddComponent<InListenerRadiusTag>(_EmitterEntity);
        else _EntityManager.RemoveComponent<InListenerRadiusTag>(_EmitterEntity);

        if (_IsPlaying) _EntityManager.AddComponent<PlayingTag>(_EmitterEntity);
        else _EntityManager.RemoveComponent<PlayingTag>(_EmitterEntity);

        _EntityManager.SetComponentData(_EmitterEntity, new Translation { Value = transform.position });

        UpdateComponents();
    }

    protected virtual void UpdateComponents() { }
    protected void UpdateDSPEffectsChain(bool clear = true)
    {
        //--- TODO not sure if clearing and adding again is the best way to do this.
        DynamicBuffer<DSPParametersElement> dstManager = _EntityManager.GetBuffer<DSPParametersElement>(_EmitterEntity);
        if (clear) dstManager.Clear();
        for (int i = 0; i < _DSPChainParams.Length; i++)
            dstManager.Add(_DSPChainParams[i].GetDSPBufferElement());
    }

    /// <summary>
    //      Updates and returns "in-range" status of emitter.
    //      Defined by the ratio between listener distance and emitter's maximum audible range.
    /// <summary>
    public bool ListenerDistance(float distance)
    {
        _CurrentDistance = distance;
        _DistanceAmplitude = AudioUtils.ListenerDistanceVolume(distance, _MaxAudibleDistance);

        if (_CurrentDistance < _MaxAudibleDistance) _InListenerRadius = true;
        else _InListenerRadius = false;
        return _InListenerRadius;
    }

    /// <summary>
    //      Removes continuous emitters' start offset.
    /// <summary>
    public void ResetLastSampleIndex()
    {
        _LastSampleIndex = GrainSynth.Instance._CurrentDSPSample;
    }

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
