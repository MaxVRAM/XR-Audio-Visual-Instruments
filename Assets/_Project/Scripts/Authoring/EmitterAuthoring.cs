using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Unity.Transforms;
using Random = UnityEngine.Random;


[DisallowMultipleComponent]
[RequiresEntityConversion]
public class EmitterAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    [SerializeField]
    protected bool _Initialised = false;
    public enum EmitterType {Continuous, Burst}
    protected Entity _EmitterEntity;
    protected EntityManager _EntityManager;
    protected float[] _PerlinSeedArray;

    [Header("Emitter Configuration")]
    [Tooltip("(generated) Host component managing this emitter.")]
    public HostAuthoring _Host;
    [Tooltip("(generated) This emitter's subtype. Current subtypes are either 'Continuous' or 'Burst'.")]
    public EmitterType _EmitterType;
    [Tooltip("Limit the emitter's playback to collision/contact states.")]
    public bool _ContactEmitter = false;
    [Tooltip("Audio clip used as the emitter's content source.")]
    public int _ClipIndex = 0;

    // TODO: Create AudioClipSource and AudioClipLibrary objects to add properties to source
    // content, making it much easier to create emitter configurations. Adding properties like
    // tagging/grouping, custom names/descriptions, and per-clip processing; like volume,
    // compression, and eq; are feasible and would drastically benefit workflow.

    [Header("Playback Config")]
    [Range(0.001f, 1f)]
    [Tooltip("Scaling factor applied to the global listener radius value. The result defines the emitter's distance-volume attenuation.")]
    public float _DistanceAttenuationFactor = 1f;
    [Tooltip("Normalised age to begin fadeout of spawned emitter if a DestroyTimer component is attached.")]
    [Range(0,1)]
    public float _NormalisedAgeFadeout = .9f;  // TODO - not implemented yet
    [Tooltip("Reverses the playhead of an individual grain if it reaches the end of the clip during playback instead of outputting 0s.")]
    public bool _PingPongGrainPlayheads = true;
    [Tooltip("Multiplies the emitter's output by the rigidity value of the colliding surface.")]
    public bool _ColliderRigidityVolumeScale = false;

    [Header("Runtime Dynamics")]
    public bool _IsPlaying = true;
    public float _TimeExisted = 0;
    public float _AdjustedDistance = 0;
    public float _DistanceAmplitude = 0;
    protected float _ContactSurfaceAttenuation = 1;
    protected int _LastSampleIndex = 0;
    protected float _SamplesPerMS = 0;

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

    void Awake()
    {
        Initialise();
        GetComponent<ConvertToEntity>().ConversionMode = ConvertToEntity.Mode.ConvertAndInjectGameObject;
    }

    public virtual void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) { }

    public virtual void Initialise() {}

    public void UpdateTranslationAndTags()
    {
        if (!_Initialised)
            return;

        _TimeExisted += Time.deltaTime;

        if (_Host._InListenerRadius) _EntityManager.AddComponent<InListenerRadiusTag>(_EmitterEntity);
        else _EntityManager.RemoveComponent<InListenerRadiusTag>(_EmitterEntity);

        if (_Host._Connected) _EntityManager.AddComponent<ConnectedTag>(_EmitterEntity);
        else _EntityManager.RemoveComponent<ConnectedTag>(_EmitterEntity);

        if (_IsPlaying) _EntityManager.AddComponent<PlayingTag>(_EmitterEntity);
        else _EntityManager.RemoveComponent<PlayingTag>(_EmitterEntity);
    }

    public virtual void UpdateEmitterComponents() { }

    protected void UpdateDSPEffectsBuffer(bool clear = true)
    {
        //--- TODO not sure if clearing and adding again is the best way to do this.
        DynamicBuffer<DSPParametersElement> dspBuffer = _EntityManager.GetBuffer<DSPParametersElement>(_EmitterEntity);
        if (clear) dspBuffer.Clear();
        for (int i = 0; i < _DSPChainParams.Length; i++)
            dspBuffer.Add(_DSPChainParams[i].GetDSPBufferElement());
    }

    public void UpdateDistanceAmplitude(float distance, float speakerFactor)
    {
        _AdjustedDistance = distance / _DistanceAttenuationFactor;
        _DistanceAmplitude = AudioUtils.ListenerDistanceVolume(_AdjustedDistance) * speakerFactor;
    }

    public void NewCollision(Collision collision)
    {
        if (_EmitterType == EmitterType.Burst && _ContactEmitter)
            _IsPlaying = true;
        UpdateContactStatus(collision);
    }

    public void UpdateContactStatus(Collision collision)
    {
        if (_ContactEmitter)
        {
            // TODO Add feature to quickly fade-out these emitters in case their grain durations are very long
            // May need to add fade-out property to the GrainPlayback data component.
            if (_EmitterType == EmitterType.Continuous)
                _IsPlaying = collision != null;
            if (collision != null && _ColliderRigidityVolumeScale && collision.collider.TryGetComponent(out SurfaceParameters surface))
                _ContactSurfaceAttenuation = surface._Rigidity;
            else
                _ContactSurfaceAttenuation = 1;           
        }
    }

    public float GeneratePerlinForParameter(int parameterIndex)
    {
        return Mathf.PerlinNoise(
            Time.time + _PerlinSeedArray[parameterIndex],
            (Time.time + _PerlinSeedArray[parameterIndex]) * 0.5f);
    }

    private void OnDestroy()
    {
        GrainSynth.Instance.DeRegisterEmitter(this);
        DestroyEntity();
    }

    public void DestroyEntity()
    {
        if (World.All.Count != 0 && _EmitterEntity != null)
            _EntityManager.DestroyEntity(_EmitterEntity);
    }

}
