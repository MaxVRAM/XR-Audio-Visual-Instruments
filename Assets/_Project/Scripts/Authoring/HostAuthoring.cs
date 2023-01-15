using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;

[DisallowMultipleComponent]
[RequiresEntityConversion]
public class HostAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    [SerializeField]
    protected bool _Initialised = false;
    
    protected Entity _HostEntity;
    protected EntityManager _EntityManager;
    protected Transform _HeadTransform;
    protected BlankModulation _BlankInputComponent;
    
    [Header("Runtime Dynamics")]
    public DestroyTimer _DestroyTimer;
    public bool _IsColliding = false;
    public bool _InListenerRadius = false;
    public float _ListenerDistance = 0;

    [Header("Speaker assignment")]
    [Tooltip("If a dedicated speaker is set, the runtime attachment system will be disabled for this host.")]
    public SpeakerAuthoring _DedicatedSpeaker;
    [SerializeField]
    public bool _Connected = false;
    protected Transform _SpeakerTransform;
    [SerializeField]
    public int _SpeakerIndex = int.MaxValue;
    [SerializeField]
    protected EmitterAttachmentLine _AttachmentLine;

    [Header("Interaction Sources")]
    [Tooltip("List of attached behaviour scripts to use as modulation input sources.")]
    [SerializeField]
    protected List<BehaviourClass> _Behaviours;
    [Tooltip("Primary target for generating collision and modulation data for emitters. Defaults parent game object.")]
    public GameObject _LocalObject;
    [Tooltip("Additional object used to generate 'relative' values with against the interaction object. E.g. distance, relative speed, etc.")]
    public GameObject _RemoteObject;
    [Tooltip("(generated) Paired component that pipes collision data from the local object target to this host.")]
    public CollisionPipe _CollisionPipeComponent;

    [Tooltip("(generated) Sibling emitters components for this host to manage.")]
    public EmitterAuthoring[] _HostedEmitters;
    [Tooltip("(generated) Sibling modulation input source components this host provides to its emitters.")]
    public ModulationSource[] _ModulationSources;
    [Tooltip("(generated) List of objects currently in-contact with the host's local object target.")]
    public List<GameObject> _CollidingObjects;


    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        _HostEntity = entity;
        int index = GrainSynth.Instance.RegisterHost(this);

        dstManager.AddComponentData(_HostEntity, new EmitterHostComponent
        {
            _HostIndex = index,
            _InListenerRadius = false,
            _SpeakerIndex = _SpeakerIndex,
            _Connected = _DedicatedSpeaker != null,
            _HasDedicatedSpeaker = _DedicatedSpeaker != null
        });

        name = $"Host {index}: {transform.parent.name}";

        #if UNITY_EDITOR
                dstManager.SetName(entity, name);
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

        if (!TryGetComponent(out _AttachmentLine))
            _AttachmentLine = gameObject.AddComponent<EmitterAttachmentLine>();
        _AttachmentLine._TransformA = transform;

        _HostedEmitters = transform.parent.GetComponentsInChildren<EmitterAuthoring>();
        _ModulationSources = transform.parent.GetComponentsInChildren<ModulationSource>();

        foreach (EmitterAuthoring emitter in _HostedEmitters)
            emitter._Host = this;

        if (_LocalObject == null)
            _LocalObject = transform.parent.gameObject;

        SetLocalInputSource(_LocalObject);
        SetRemoteInputSource(_RemoteObject);
        UpdateBehaviourModulationInputs();

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

        UpdateSpeakerAttachmentLine();

        // Update host translation component.
        Translation translation = _EntityManager.GetComponentData<Translation>(_HostEntity);
        _EntityManager.SetComponentData(_HostEntity, new Translation { Value = transform.position });
        
        _ListenerDistance = Mathf.Abs((transform.position - _HeadTransform.position).magnitude);

        #region START HOST COMPONENT
        EmitterHostComponent hostData = _EntityManager.GetComponentData<EmitterHostComponent>(_HostEntity);        

        if (_DedicatedSpeaker != null)
        {
            hostData._Connected = true;
            hostData._HasDedicatedSpeaker = true;
            hostData._SpeakerIndex = _SpeakerIndex;
            _InListenerRadius = _ListenerDistance < GrainSynth.Instance._ListenerRadius;
            hostData._InListenerRadius = _InListenerRadius;
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
        #endregion

        float speakerAmplitudeFactor = 0;

        if (_Connected)
            speakerAmplitudeFactor = AudioUtils.SpeakerOffsetFactor(
                transform.position,
                _HeadTransform.position,
                _SpeakerTransform.position);

        foreach (EmitterAuthoring emitter in _HostedEmitters)
        {
            emitter.UpdateDistanceAmplitude(_ListenerDistance / GrainSynth.Instance._ListenerRadius, speakerAmplitudeFactor);
            emitter.UpdateTranslationAndTags();
            if (_Connected && _InListenerRadius)
                emitter.UpdateEmitterComponents();
        }
    }
    
    public SpeakerAuthoring DynamicSpeaker { get { return GrainSynth.Instance._Speakers[_SpeakerIndex]; } }

    public int EntityIndex { get { return _HostEntity.Index; } }

    public void UpdateSpeakerAttachmentLine()
    {
        if (_AttachmentLine != null && _Connected && GrainSynth.Instance._DrawAttachmentLines)
        {
            _AttachmentLine._Active = true;
            _AttachmentLine._TransformB = _SpeakerTransform;
        }
        else
            _AttachmentLine._Active = false;
    }

    public void SetLocalInputSource(GameObject go)
    {
        _LocalObject = go;
        if (go != null)
        {
            // Set up a collision pipe to send collisions from the targeted object here
            if (!_LocalObject.TryGetComponent(out _CollisionPipeComponent))
                _CollisionPipeComponent = _LocalObject.AddComponent<CollisionPipe>();
            if (_CollisionPipeComponent != null)
                _CollisionPipeComponent.AddHost(this);
            
            foreach (ModulationSource source in _ModulationSources)
                if (!(source is BlankModulation))
                    source._Objects.SetLocalObject(_LocalObject);
        }
    }

    public void SetRemoteInputSource(GameObject go)
    {
        _RemoteObject = go;
        if (go != null)
            foreach (ModulationSource source in _ModulationSources)
                if (!(source is BlankModulation))
                    source._Objects.SetRemoteObject(_RemoteObject);
    }

    public void AddBehaviourInputSource(BehaviourClass behaviour)
    {
        if (behaviour != null && !_Behaviours.Contains(behaviour))
            _Behaviours.Add(behaviour);
    }

    public void UpdateBehaviourModulationInputs()
    {
        foreach (BehaviourClass behaviour in _Behaviours)
        {
            if (behaviour is DestroyTimer timer)
                _DestroyTimer = timer;
            foreach (ModulationSource source in _ModulationSources)
                if (source is InputBehaviour)
                    source.SetBehaviourInput(behaviour);
        }
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
        GrainSynth.Instance.DeRegisterHost(this);
        if (_CollisionPipeComponent != null) _CollisionPipeComponent.RemoveHost(this);
        DestroyEntity();
    }

    public void DestroyEntity()
    {
        if (World.All.Count != 0 && _HostEntity != null)
            _EntityManager.DestroyEntity(_HostEntity);
    }
}
