using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;

[DisallowMultipleComponent]
public class HostAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    protected Entity _HostEntity;
    protected EntityManager _EntityManager;
    protected Transform _HeadTransform;
    protected BlankModulation _BlankInputComponent;
        
    [Header("Runtime Dynamics")]
    [SerializeField]
    protected bool _Initialised = false;
    public bool _IsColliding = false;
    [SerializeField]
    protected bool _Connected = false;
    public bool IsConnected { get { return _Connected; } }
    [SerializeField]
    public int _SpeakerIndex = int.MaxValue;
    protected Transform _SpeakerTransform;
    public bool _InListenerRadius = false;
    public float _ListenerDistance = 0;

    [Header("Speaker assignment")]
    [Tooltip("Host will spawn a speaker prefab for itself if true, disabling attachment system functionality on both host and speaker entities.")]
    public bool _UseFixedSpeaker = false;
    public SpeakerAuthoring _FixedSpeakerPrefab;
    [Tooltip("Parent transform to host dedicated speaker, defaults to parent. Cannot be this transform!")]
    //  (may also be used as a location target for dynamic speakers)
    public Transform _SpeakerContainer;
    [SerializeField]
    protected SpeakerAuthoring _FixedSpeaker;
    [SerializeField]
    protected AttachmentLine _AttachmentLine;

    [Header("Interactions")]
    public ObjectSpawner _Spawner;
    public SpawnableManager _SpawnableManager;
    public GameObject _LocalObject;
    [Tooltip("Additional object used to generate 'relative' values with against the interaction object. E.g. distance, relative speed, etc.")]
    public GameObject _RemoteObject;
    [Tooltip("(generated) Paired component that pipes collision data from the local object target to this host.")]
    public CollisionPipe _CollisionPipeComponent;
    protected SurfaceProperties _SurfaceProperties;
    protected float _SurfaceRigidity = 0.5f;
    public float SurfaceRigidity { get { return _SurfaceRigidity; } }
    [Tooltip("List of attached behaviour scripts to use as modulation input sources.")]
    [SerializeField]
    protected List<BehaviourClass> _Behaviours;

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
    public bool ContactAllowed(GameObject other)
    {
        return _Spawner == null || _Spawner._AllowSiblingSurfaceContact || !_Spawner._ActiveObjects.Contains(other);
    }

    void Awake()
    {
        GetComponent<ConvertToEntity>().ConversionMode = ConvertToEntity.Mode.ConvertAndInjectGameObject;
        // Set up a blank input component for emitter properties that don't have one attached
        if (!TryGetComponent(out _BlankInputComponent))
            _BlankInputComponent = gameObject.AddComponent(typeof(BlankModulation)) as BlankModulation;
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        _HostEntity = entity;
        int index = GrainSynth.Instance.RegisterHost(this);

        dstManager.AddComponentData(_HostEntity, new EmitterHostComponent
        {
            _HostIndex = index,
            _Connected = false,
            _InListenerRadius = false,
            _SpeakerIndex = _SpeakerIndex,
            _IsUsingFixedSpeaker = _UseFixedSpeaker
        });

        name = $"Host {index}: {transform.parent.name}";

        #if UNITY_EDITOR
                dstManager.SetName(_HostEntity, name);
        #endif

        _Initialised = true;
    }

    void Start()
    {
        _EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _HeadTransform = FindObjectOfType<Camera>().transform;

        if (_LocalObject == null)
            _LocalObject = transform.parent.gameObject;

        _HostedEmitters = transform.parent.GetComponentsInChildren<EmitterAuthoring>();
        _ModulationSources = transform.parent.GetComponentsInChildren<ModulationSource>();

        if (!TryGetComponent(out _SurfaceProperties))
            _SurfaceProperties = transform.parent.GetComponent<SurfaceProperties>();
        if (_SurfaceProperties != null)
            _SurfaceRigidity = _SurfaceProperties._Rigidity;

        foreach (EmitterAuthoring emitter in _HostedEmitters)
            emitter._Host = this;

        SetLocalInputSource(_LocalObject);
        SetRemoteInputSource(_RemoteObject);
        UpdateBehaviourModulationInputs();

        if (_UseFixedSpeaker)
        {
            if (_FixedSpeaker == null && _FixedSpeakerPrefab != null)
            {
                _SpeakerContainer = _SpeakerContainer != null ? _SpeakerContainer : transform.parent;
                _FixedSpeaker = Instantiate(_FixedSpeakerPrefab, _SpeakerContainer.position, Quaternion.identity, _SpeakerContainer);
            }

            _FixedSpeaker._IsFixedSpeaker = true;
            _SpeakerIndex = _FixedSpeaker.SpeakerIndex;
            _SpeakerTransform = _FixedSpeaker.transform;
            _Connected = _FixedSpeaker._Registered;

            _FixedSpeaker.AddEmitterHostLink(this);
        }
        else if (!TryGetComponent(out _AttachmentLine))
        {
            _AttachmentLine = gameObject.AddComponent<AttachmentLine>();
            _AttachmentLine._TransformA = transform;
        }
    }

    void Update()
    {
        if (!_Initialised)
            return;

        // Update host translation component.
        Translation translation = _EntityManager.GetComponentData<Translation>(_HostEntity);
        _EntityManager.SetComponentData(_HostEntity, new Translation { Value = transform.position });
        _ListenerDistance = Mathf.Abs((transform.position - _HeadTransform.position).magnitude);

        #region START HOST COMPONENT

        EmitterHostComponent hostData = _EntityManager.GetComponentData<EmitterHostComponent>(_HostEntity);

        hostData._IsUsingFixedSpeaker = _UseFixedSpeaker;

        if (!_UseFixedSpeaker)
        {
            _EntityManager.RemoveComponent<UsingFixedSpeaker>(_HostEntity);
            _InListenerRadius = hostData._InListenerRadius;

            if (GrainSynth.Instance.GetSpeakerFromIndex(hostData._SpeakerIndex, out SpeakerAuthoring speaker) != null)
            {
                _SpeakerTransform = speaker.gameObject.transform;
                _SpeakerIndex = hostData._SpeakerIndex;
                _Connected = hostData._Connected;
            }
            else
            {
                _SpeakerTransform = transform;
                _SpeakerIndex = int.MaxValue;
                _Connected = false;
            }
        }
        else
        {
            _EntityManager.AddComponent<UsingFixedSpeaker>(_HostEntity);
            _InListenerRadius = _ListenerDistance < GrainSynth.Instance._ListenerRadius;

            if (_FixedSpeaker != null)
            {
                hostData._InListenerRadius = _InListenerRadius;
                hostData._Connected = _FixedSpeaker._Registered;
                _SpeakerIndex = _FixedSpeaker.SpeakerIndex;
                _Connected = _FixedSpeaker._Registered;
            }
            else
            {
                hostData._Connected = false;
                _SpeakerTransform = transform;
                _SpeakerIndex = int.MaxValue;
                _Connected = false;
            }

            hostData._SpeakerIndex = _SpeakerIndex;
        }
        _EntityManager.SetComponentData(_HostEntity, hostData);

        #endregion

        UpdateSpeakerAttachmentLine();

        #region PROCESS RIGIDITY VALUE FROM COLLIDING OBJECTS

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
    
    public int EntityIndex { get { return _HostEntity.Index; } }

    public void UpdateSpeakerAttachmentLine()
    {
        if (_AttachmentLine != null)
            if (!_UseFixedSpeaker && _Connected && _SpeakerTransform != null && GrainSynth.Instance._DrawAttachmentLines)
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

    protected void OnDrawGizmos()
    {
        // Gizmos.color = _InListenerRadius ? Color.yellow : Color.blue;
        // Gizmos.DrawSphere(transform.position, .1f);
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
