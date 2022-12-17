using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;

[DisallowMultipleComponent]
[RequiresEntityConversion]
public class HostAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    protected bool _Initialised = false;
    protected Entity _HostEntity;
    protected EntityManager _EntityManager;
    protected Transform _HeadTransform;
    protected BlankModulation _BlankInputComponent;

    [Header("Speaker assignment")]
    [Tooltip("If a dedicated speaker is set, the runtime attachment system will be disabled for this host.")]
    public SpeakerAuthoring _DedicatedSpeaker;
    protected Transform _SpeakerTransform;
    public int _SpeakerIndex = int.MaxValue;
    public bool _Connected = false;

    [Header("Emitter objects")]
    [Tooltip("Primary target for generating collision and modulation data for emitters. Defaults parent game object.")]
    public GameObject _LocalObject;
    [Tooltip("Additional object used to generate 'relative' values with against the interaction object. E.g. distance, relative speed, etc.")]
    public CollisionPipe _LocalObjectCollisionPipe;
    public GameObject _RemoteObject;
    [Tooltip("Finds all sibling emitter components to manage.")]

    public EmitterAuthoring[] _HostedEmitters;
    [Tooltip("Finds all sibling modulation input components to manage.")]
    public ModulationSource[] _ModulationSources;

    [Header("Runtime dynamics")]
    public bool _IsColliding = false;
    public bool _InListenerRadius = false;
    public float _ListenerDistance = 0;
    public List<GameObject> _CollidingObjects;


    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        _HostEntity = entity;

        if (_DedicatedSpeaker != null) dstManager.AddComponentData(_HostEntity, new DedicatedSpeakerTag { });

        dstManager.AddComponentData(_HostEntity, new EmitterHostComponent
        {
            _Connected = _Connected,
            _InListenerRadius = false,
            _SpeakerIndex = _SpeakerIndex,
            _HasDedicatedSpeaker = _DedicatedSpeaker != null
        });

        #if UNITY_EDITOR
                dstManager.SetName(entity, "Emitter Host:   " + gameObject.name);
        #endif

        _Initialised = true;
    }

    void Awake()
    {
        GetComponent<ConvertToEntity>().ConversionMode = ConvertToEntity.Mode.ConvertAndInjectGameObject;
        // Set up a blank input component for emitter properties that don't have one attached
        if (!TryGetComponent(out _BlankInputComponent))
            _BlankInputComponent = gameObject.AddComponent(typeof(BlankModulation)) as BlankModulation;
    }
    
    void Start()
    {
        _EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _HeadTransform = FindObjectOfType<Camera>().transform;

        _HostedEmitters = gameObject.transform.parent.GetComponentsInChildren<EmitterAuthoring>();
        _ModulationSources = gameObject.transform.parent.GetComponentsInChildren<ModulationSource>();

        if (_LocalObject == null) SetLocalInputSource(gameObject.transform.parent.gameObject);
        if (_RemoteObject != null) SetRemoteInputSource(_RemoteObject);
        if (_DedicatedSpeaker != null)
        {
            _DedicatedSpeaker.AddEmitterLink(gameObject);                
            _SpeakerIndex = _DedicatedSpeaker.RegisterAndGetIndex();
            _SpeakerTransform = _DedicatedSpeaker.transform;
            _Connected = true;
        }
    }

    void Update()
    {
        if (!_Initialised)
            return;

        // Update host range status.
        _ListenerDistance = Mathf.Abs((transform.position - _HeadTransform.position).magnitude);

        // Update host translation component.
        Translation translation = _EntityManager.GetComponentData<Translation>(_HostEntity);
        _EntityManager.SetComponentData(_HostEntity, new Translation { Value = transform.position });

        // Update host component data.
        EmitterHostComponent hostData = _EntityManager.GetComponentData<EmitterHostComponent>(_HostEntity);        

        if (_DedicatedSpeaker != null)
        {
            hostData._Connected = true;
            hostData._HasDedicatedSpeaker = true;
            hostData._SpeakerIndex = _SpeakerIndex;
            hostData._InListenerRadius = _ListenerDistance < GrainSynth.Instance._ListenerRadius;
        }
        else
        {
            _InListenerRadius = hostData._InListenerRadius;
            hostData._HasDedicatedSpeaker = false;
            _Connected = hostData._Connected;
            if (hostData._SpeakerIndex < GrainSynth.Instance._Speakers.Count)
            {               
                _SpeakerIndex = hostData._SpeakerIndex; 
                _SpeakerTransform = DynamicSpeaker.gameObject.transform;
            }
        }
        _EntityManager.SetComponentData(_HostEntity, hostData);

        float speakerFactor = 0;

        if (_Connected)
        {
            speakerFactor = AudioUtils.SpeakerOffsetFactor(
                transform.position,
                _HeadTransform.position,
                _SpeakerTransform.position);
        }

        foreach (EmitterAuthoring emitter in _HostedEmitters)
        {
            emitter.UpdateDistanceAmplitude(_ListenerDistance / GrainSynth.Instance._ListenerRadius, speakerFactor);
            emitter._InListenerRadius = _InListenerRadius;
            emitter._SpeakerIndex = _SpeakerIndex;
            emitter._Connected = _Connected;
            emitter.UpdateTranslationAndTags();
            emitter.UpdateComponents();
        }
    }
    
    public SpeakerAuthoring DynamicSpeaker { get { return GrainSynth.Instance._Speakers[_SpeakerIndex]; } }

    public void SetLocalInputSource(GameObject go)
    {
        _LocalObject = go;
        // Set up a collision pipe to send collisions from the targeted object here
        if (!_LocalObject.TryGetComponent(out _LocalObjectCollisionPipe))
            _LocalObjectCollisionPipe = _LocalObject.AddComponent<CollisionPipe>();
        if (_LocalObjectCollisionPipe != null) _LocalObjectCollisionPipe.AddHost(this);

        foreach (ModulationSource source in _ModulationSources)
            if (!(source is BlankModulation))
                source._Objects.SetLocalObject(_LocalObject);
    }

    public void SetRemoteInputSource(GameObject go)
    {
        _RemoteObject = go;
        foreach (ModulationSource source in _ModulationSources)
            if (!(source is BlankModulation))
                source._Objects.SetRemoteObject(_RemoteObject);
    }
    
    public void OnCollisionEnter(Collision collision)
    {
        _CollidingObjects.Add(collision.collider.gameObject);

        foreach (ModulationSource source in _ModulationSources)
            source.ProcessCollisionValue(collision);

        foreach (EmitterAuthoring emitter in _HostedEmitters)
            emitter.NewCollision(collision);
    }

    public void OnCollisionStay(Collision collision)
    {
        _IsColliding = true;
        foreach (ModulationSource source in _ModulationSources)
            source.SetInputCollision(true, collision.collider.material);

        foreach (EmitterAuthoring emitter in _HostedEmitters)
            emitter.UpdateContactStatus(collision);
    }

    public void OnCollisionExit(Collision collision)
    {
        _CollidingObjects.Remove(collision.collider.gameObject);

        foreach (ModulationSource source in _ModulationSources)
            source.SetInputCollision(false, collision.collider.material);

        if (_CollidingObjects.Count == 0)
        {
            _IsColliding = false;
            foreach (EmitterAuthoring emitter in _HostedEmitters)
                emitter.UpdateContactStatus(null);
        }
    }

    protected void OnDrawGizmos()
    {
        Gizmos.color = _InListenerRadius ? Color.yellow : Color.blue;
        Gizmos.DrawSphere(transform.position, .1f);
    }
    
    private void OnDestroy()
    {
        if (_LocalObjectCollisionPipe != null)
            _LocalObjectCollisionPipe.RemoveHost(this);
        DestroyEntity();
    }

    public void DestroyEntity()
    {
        if (World.All.Count != 0 && _HostEntity != null)
            _EntityManager.DestroyEntity(_HostEntity);
    }
}
