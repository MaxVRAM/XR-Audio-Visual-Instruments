using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using System;

/// <summary>
//      Multiple emitters are often spawned and attached to the same object and modulated by
//      the same game-world interactions. Emitter Hosts manage emitters designed to create
//      a single "profile" for an interactive sound object.
/// <summary>

public class HostAuthoring : MonoBehaviour
{
    #region FIELDS & PROPERTIES

    private Entity _HostEntity;
    private EntityManager _EntityManager;
    private EntityArchetype _HostArchetype;

    private Transform _HeadTransform;
    private BlankModulation _BlankInputComponent;

    [SerializeField] private int _HostIndex = int.MaxValue;
    public int HostIndex { get { return _HostIndex; } }

    [Header("Runtime Dynamics")]
    [SerializeField] private bool _Initialised = false;
    [SerializeField] private bool _Connected = false;
    public bool IsConnected { get { return _Connected; } }

    [SerializeField] public int _SpeakerIndex = int.MaxValue;
    [SerializeField] private bool _InListenerRadius = false;
    public bool InListenerRadius { get { return _InListenerRadius; } }
    [SerializeField] private float _ListenerDistance = 0;
    [SerializeField] private bool _IsColliding = false;

    [Header("Speaker assignment")]
    [Tooltip("Parent transform position for speakers to target for this host. Defaults to this transform.")]
    [SerializeField] private Transform _SpeakerTarget;
    [SerializeField] private Transform _SpeakerTransform;
    [SerializeField] private AttachmentLine _AttachmentLine;

    [Header("Interactions")]
    public ObjectSpawner _Spawner;
    public SpawnableManager _SpawnableManager;
    public GameObject _LocalObject;
    [Tooltip("Additional object used to generate 'relative' values with against the interaction object. E.g. distance, relative speed, etc.")]
    public GameObject _RemoteObject;
    [Tooltip("(generated) Paired component that pipes collision data from the local object target to this host.")]
    public CollisionPipe _CollisionPipeComponent;
    private SurfaceProperties _SurfaceProperties;
    [SerializeField] private float _SurfaceRigidity = 0.5f;
    public float SurfaceRigidity { get { return _SurfaceRigidity; } }
    [Tooltip("List of attached behaviour scripts to use as modulation input sources.")]
    [SerializeField] private List<BehaviourClass> _Behaviours;

    [Tooltip("(generated) Sibling emitters components for this host to manage.")]
    public EmitterAuthoring[] _HostedEmitters;
    [Tooltip("(generated) Sibling modulation input source components this host provides to its emitters.")]
    public ModulationSource[] _ModulationSources;
    [Tooltip("(generated) List of objects currently in-contact with the host's local object target.")]
    public List<GameObject> _CollidingObjects;
    public List<float> _ContactRigidValues;
    public float _RigiditySmoothUp = 0.5f;
    public float RigiditySmoothUp { get { return 1 / _RigiditySmoothUp; }}
    public float _CurrentCollidingRigidity = 0;
    public float _TargetCollidingRigidity = 0;

    #endregion

    #region GAME OBJECT MANAGEMENT

    public int EntityIndex { get { return _HostEntity.Index; } }

    void Awake()
    {
        if (!TryGetComponent(out _BlankInputComponent))
            _BlankInputComponent = gameObject.AddComponent(typeof(BlankModulation)) as BlankModulation;
    }

    void Start()
    {
        _HeadTransform = FindObjectOfType<Camera>().transform;
        _SpeakerTarget = _SpeakerTarget != null ? _SpeakerTarget : transform;
        _SpeakerTransform = _SpeakerTarget;

        if (_LocalObject == null)
            _LocalObject = transform.parent.gameObject;

        _HostedEmitters = transform.parent.GetComponentsInChildren<EmitterAuthoring>();

        foreach (EmitterAuthoring emitter in _HostedEmitters)
            emitter._Host = this;

        _ModulationSources = transform.parent.GetComponentsInChildren<ModulationSource>();

        if (_AttachmentLine = TryGetComponent(out _AttachmentLine) ? _AttachmentLine : gameObject.AddComponent<AttachmentLine>())
            _AttachmentLine._TransformA = _SpeakerTarget;

        if (TryGetComponent(out _SurfaceProperties) || transform.parent.TryGetComponent(out _SurfaceProperties))
            _SurfaceRigidity = _SurfaceProperties._Rigidity;

        SetLocalInputSource(_LocalObject);
        SetRemoteInputSource(_RemoteObject);
        UpdateBehaviourModulationInputs();

        _EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _HostArchetype = _EntityManager.CreateArchetype(
            typeof(EmitterHostComponent),
            typeof(Translation));
    }

    private void OnDestroy()
    {
        if (GrainSynth.Instance != null)
            GrainSynth.Instance.DeRegisterHost(this);
        if (_CollisionPipeComponent != null)
            _CollisionPipeComponent.RemoveHost(this);

        DestroyEntity();
    }

    public void DestroyEntity()
    {
        if (World.All.Count != 0 && _HostEntity != null && _EntityManager != null)
            _EntityManager.DestroyEntity(_HostEntity);
    }

    #endregion

    #region ENTITY CALLS

    public Entity CreateEntity()
    {
        _HostIndex = GrainSynth.Instance.RegisterHost(this);
        name = $"Host.{_HostIndex}.{transform.parent.name}";
        Debug.Log($"Host {name} creating new entity");

        _SpeakerIndex = int.MaxValue;
        _SpeakerTransform = _SpeakerTarget;

        if (_HostEntity != Entity.Null)
            _EntityManager.DestroyEntity(_HostEntity);

        _HostEntity = _EntityManager.CreateEntity(_HostArchetype);

        _EntityManager.SetComponentData(_HostEntity, new EmitterHostComponent
        {
            _Connected = false,
            _HostIndex = _HostIndex,
            _SpeakerIndex = int.MaxValue
        });

        _EntityManager.SetComponentData(_HostEntity, new Translation { Value = _SpeakerTarget.position });

#if UNITY_EDITOR
        _EntityManager.SetName(_HostEntity, name);
#endif

        _Initialised = true;
        return _HostEntity;
    }

    public bool UpdateComponents()
    {
        if (!_Initialised || _EntityManager == null || _HostEntity == null)
            CreateEntity();

        if (!_Initialised)
            return false;

        _EntityManager.SetComponentData(_HostEntity, new Translation { Value = _SpeakerTarget.position });

        EmitterHostComponent hostData = _EntityManager.GetComponentData<EmitterHostComponent>(_HostEntity);
        if (GrainSynth.Instance.GetSpeakerFromIndex(hostData._SpeakerIndex, out SpeakerAuthoring speaker))
        {
            _SpeakerTransform = speaker.gameObject.transform;
            _SpeakerIndex = hostData._SpeakerIndex;
            _Connected = hostData._Connected;
        }
        else
        {
            _Connected = false;
            _SpeakerIndex = int.MaxValue;
            _SpeakerTransform = _SpeakerTarget;
        }

        _InListenerRadius = hostData._InListenerRadius;
        _EntityManager.SetComponentData(_HostEntity, hostData);

        UpdateSpeakerAttachmentLine();

        float speakerAmplitudeFactor = AudioUtils.SpeakerOffsetFactor(
            transform.position,
            _HeadTransform.position,
            _SpeakerTransform.position
            );

        _ListenerDistance = Mathf.Abs((transform.position - _HeadTransform.position).magnitude);

        foreach (EmitterAuthoring emitter in _HostedEmitters)
        {
            emitter.UpdateDistanceAmplitude(_ListenerDistance / GrainSynth.Instance._ListenerRadius, speakerAmplitudeFactor);
            emitter.UpdateEntityTags();
            if (_Connected && InListenerRadius)
                emitter.UpdateEmitterComponents();
        }

        return true;
    }

    #endregion

    #region BEHAVIOUR UPDATES

    public void ProcessRigidity()
    {
        // Clear lingering null contact objects and add rigidity values to list
        _CollidingObjects.RemoveAll(item => item == null);
        _ContactRigidValues.Clear();

        foreach (GameObject go in _CollidingObjects)
            if (go.TryGetComponent(out SurfaceProperties props))
                _ContactRigidValues.Add(props._Rigidity);
            else _ContactRigidValues.Add(0.5f);
        // Sort and find largest rigidity value to set as target
        if (_ContactRigidValues.Count > 0)
        {
            _ContactRigidValues.Sort();
            _TargetCollidingRigidity = _ContactRigidValues[^1];
            // Smooth transition to upward rigidity values to avoid random bursts of roll emitters from short collisions
            if (_TargetCollidingRigidity < _CurrentCollidingRigidity + 0.001f || _RigiditySmoothUp <= 0)
                _CurrentCollidingRigidity = _TargetCollidingRigidity;
            else
                _CurrentCollidingRigidity = _CurrentCollidingRigidity.Lerp(_TargetCollidingRigidity, RigiditySmoothUp * Time.deltaTime);
        }
        if (_CurrentCollidingRigidity < 0.001f) _CurrentCollidingRigidity = 0;
    }


    public void UpdateSpeakerAttachmentLine()
    {
        if (_AttachmentLine != null)
            if (_Connected && _SpeakerTransform != _SpeakerTarget && GrainSynth.Instance._DrawAttachmentLines)
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
                if (source is not BlankModulation)
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
            if (behaviour is SpawnableManager manager)
                _SpawnableManager = manager;
            foreach (ModulationSource source in _ModulationSources)
                if (source is InputBehaviour)
                    source.SetBehaviourInput(behaviour);
        }
    }

    #endregion

    #region COLLISION HANDLING

    public void OnCollisionEnter(Collision collision)
    {
        GameObject other = collision.collider.gameObject;
        _CollidingObjects.Add(other);

        if (ContactAllowed(other))
        {
            if (other.TryGetComponent(out SurfaceProperties surface))
                _ContactRigidValues.Add(surface._Rigidity);

            foreach (ModulationSource source in _ModulationSources)
                source.ProcessCollisionValue(collision);
        }

        if (_Spawner == null || _Spawner.UniqueCollision(_LocalObject, other))
            foreach (EmitterAuthoring emitter in _HostedEmitters)
                emitter.NewCollision(collision);
    }

    public void OnCollisionStay(Collision collision)
    {
        Collider collider = collision.collider;
        _IsColliding = true;

        if (ContactAllowed(collider.gameObject))
        {
            foreach (ModulationSource source in _ModulationSources)
                source.SetInputCollision(true, collider.material);

            foreach (EmitterAuthoring emitter in _HostedEmitters)
                emitter.UpdateContactStatus(collision);
        }
    }

    public void OnCollisionExit(Collision collision)
    {
        _CollidingObjects.Remove(collision.collider.gameObject);

        if (_CollidingObjects.Count == 0)
        {
            _IsColliding = false;
            _TargetCollidingRigidity = 0;
            _CurrentCollidingRigidity = 0;
            _ContactRigidValues.Clear();
            foreach (ModulationSource source in _ModulationSources)
                source.SetInputCollision(false, collision.collider.material);
            foreach (EmitterAuthoring emitter in _HostedEmitters)
                emitter.UpdateContactStatus(null);
        }
    }

    public bool ContactAllowed(GameObject other)
    {
        return _Spawner == null || _Spawner._AllowSiblingSurfaceContact || !_Spawner._ActiveObjects.Contains(other);
    }

    #endregion
}










//void Awake()
//{
//    GetComponent<ConvertToEntity>().ConversionMode = ConvertToEntity.Mode.ConvertAndInjectGameObject;
//    // Set up a blank input component for emitter properties that don't have one attached
//    if (!TryGetComponent(out _BlankInputComponent))
//        _BlankInputComponent = gameObject.AddComponent(typeof(BlankModulation)) as BlankModulation;
//}


//public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
//{
//    _HostEntity = entity;
//    int index = GrainSynth.Instance.RegisterHost(this);

//    dstManager.AddComponentData(_HostEntity, new EmitterHostComponent
//    {
//        _HostIndex = index,
//        _Connected = false,
//        _InListenerRadius = false,
//        _SpeakerIndex = _SpeakerIndex
//    });


//    #if UNITY_EDITOR
//            dstManager.SetName(_HostEntity, name);
//    #endif

//    _EntityInitialised = true;
//}



//hostData._IsUsingFixedSpeaker = _UseFixedSpeaker;

//if (!_UseFixedSpeaker)
//{
//    _EntityManager.RemoveComponent<UsingFixedSpeaker>(_HostEntity);
//    InListenerRadius = hostData.InListenerRadius;

//    if (GrainSynth.Instance.GetSpeakerFromIndex(hostData._SpeakerIndex, out SpeakerAuthoring speaker) != null)
//    {
//        _SpeakerTransform = speaker.gameObject.transform;
//        _SpeakerIndex = hostData._SpeakerIndex;
//        _Connected = hostData._Connected;
//    }
//    else
//    {
//        _SpeakerTransform = transform;
//        _SpeakerIndex = int.MaxValue;
//        _Connected = false;
//    }
//}
//else
//{
//    _EntityManager.AddComponent<UsingFixedSpeaker>(_HostEntity);
//    InListenerRadius = _ListenerDistance < GrainSynth.Instance._ListenerRadius;

//    if (_FixedSpeaker != null)
//    {
//        hostData.InListenerRadius = InListenerRadius;
//        hostData._Connected = _FixedSpeaker._Registered;
//        _SpeakerIndex = _FixedSpeaker.SpeakerIndex;
//        _Connected = _FixedSpeaker._Registered;
//    }
//    else
//    {
//        hostData._Connected = false;
//        _SpeakerTransform = transform;
//        _SpeakerIndex = int.MaxValue;
//        _Connected = false;
//    }

//    hostData._SpeakerIndex = _SpeakerIndex;
//}