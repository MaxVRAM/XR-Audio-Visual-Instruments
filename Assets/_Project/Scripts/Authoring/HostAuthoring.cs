using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;

[DisallowMultipleComponent]
[RequiresEntityConversion]
[RequireComponent(typeof(ConvertToEntity))]
public class HostAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    protected bool _Initialised = false;
    protected Entity _HostEntity;
    protected EntityManager _EntityManager;
    protected Transform _HeadPosition;


    [Header("Emitter/speaker configuration")]
    [Tooltip("Object that provides collision and modulation data to emitters. Defaults to this game object.")]
    public GameObject _InteractionObject;
    public Rigidbody _InteractionRigidBody;
    [Tooltip("An additional object to calculate 'relative' values with against the interaction object. Distance from, etc.")]
    public GameObject _RemoteObject;
    public Rigidbody _RemoteRigidBody;
    [Tooltip("Place a speaker component here to dedicate a speaker to this host, overriding the runtime attachment system.")]
    public SpeakerAuthoring _DedicatedSpeaker;
    protected int _SpeakerIndex = int.MaxValue;
    [Tooltip("Finds all emitter components in children to manage.")]
    public EmitterAuthoring[] _HostedEmitters;
    [Tooltip("Finds all input value components in children to manage.")]
    public InputValueClass[] _InputValues;

    [Header("Runtime dynamics")]
    public bool _IsColliding = false;
    public bool _InListenerRadius = false;
    public float _CurrentDistance = 0;
    public List<GameObject> _CollidingObjects;



    void Start()
    {
        _EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _HeadPosition = FindObjectOfType<Camera>().transform;

        if (_InteractionObject == null) _InteractionObject = gameObject;
        if (_InteractionRigidBody != _InteractionObject.TryGetComponent(out _InteractionRigidBody))
                Debug.Log(name + ":     No Rigidbody found on host's Primary Object: " + _InteractionObject.name);
        if (_RemoteObject != null && _RemoteObject.TryGetComponent(out _RemoteRigidBody))
                Debug.Log(name + ":     No Rigidbody found on host's Target Object: " + _RemoteObject.name);

        _HostedEmitters = GetComponentsInChildren<EmitterAuthoring>();
        _InputValues = GetComponentsInChildren<InputValueClass>();

        if (_DedicatedSpeaker != null)
            UpdateSpeakerAttachment(_DedicatedSpeaker.GetRegisterAndGetIndex());

        foreach (InputValueClass inputValue in _InputValues)
            inputValue.UpdateInteractionSources(_InteractionObject, _RemoteObject, null);

        foreach (EmitterAuthoring emitter in _HostedEmitters)
        {
            
        }
    }

    void Awake()
    {
        GetComponent<ConvertToEntity>().ConversionMode = ConvertToEntity.Mode.ConvertAndInjectGameObject;
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        _HostEntity = entity;

        if (_DedicatedSpeaker != null) dstManager.AddComponentData(_HostEntity, new DedicatedSpeakerTag { });

        dstManager.AddComponentData(_HostEntity, new EmitterHostComponent
        {
            _InListenerRadius = false,
            _DedicatedSpeaker = _DedicatedSpeaker != null,
            _SpeakerIndex = _SpeakerIndex
        });
        #if UNITY_EDITOR
                dstManager.SetName(entity, "Emitter Host:   " + gameObject.name);
        #endif

        _Initialised = true;
    }

    public void SetTargetObject(GameObject target)
    {
        _RemoteObject = target;
        if (_RemoteObject != null &&_RemoteRigidBody == null)
            _RemoteRigidBody = _RemoteObject.GetComponent<Rigidbody>();
        foreach (InputValueClass inputValue in _InputValues)
            inputValue.UpdateInteractionSources(_InteractionObject, _RemoteObject, null);
    }

    void Update()
    {
        if (!_Initialised)
            return;
        
        // Update emitter range status. Host will be "in-range" if at least one of its emitters return true.
        _CurrentDistance = Mathf.Abs((_HeadPosition.position - _InteractionObject.transform.position).magnitude);
        _InListenerRadius = false;
        foreach (EmitterAuthoring emitter in _HostedEmitters)
            if (emitter.ListenerDistance(_CurrentDistance))
                _InListenerRadius = true;

        // Update host translation component.
        Translation translation = _EntityManager.GetComponentData<Translation>(_HostEntity);
        _EntityManager.SetComponentData(_HostEntity, new Translation { Value = transform.position });

        // Update host component data.
        EmitterHostComponent entity = _EntityManager.GetComponentData<EmitterHostComponent>(_HostEntity);        

        entity._InListenerRadius = _InListenerRadius;
        UpdateSpeakerAttachment(entity._SpeakerIndex);
        // Manually call the hosted emitters' component update function.
        foreach (EmitterAuthoring emitter in _HostedEmitters)
            emitter.ManualUpdate();

        _EntityManager.SetComponentData(_HostEntity, entity);

    }
    
    public void UpdateSpeakerAttachment(int index)
    {
        SpeakerAuthoring speaker = _DedicatedSpeaker;
        
        // Get valid speaker from EmitterSynth manager.
        if (speaker == null || index != speaker._SpeakerIndex)
            if (index < GrainSynth.Instance._MaxDynamicSpeakers)
                speaker = GrainSynth.Instance._Speakers[index];
        // Zero provided index points to an invalid speaker.
        if (speaker == null) index = int.MaxValue;

        // Update host's emitters
        foreach (EmitterAuthoring emitter in _HostedEmitters)
        {
            if (index != emitter._SpeakerIndex)
                emitter.ResetLastSampleIndex();
            emitter._SpeakerIndex = index;
        }
        _SpeakerIndex = index;
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        _CollidingObjects.Add(collision.collider.gameObject);

        foreach (EmitterAuthoring emitter in _HostedEmitters)
            emitter.UpdateCollision(collision);        
    }

    private void OnCollisionStay(Collision collision)
    {
        _IsColliding = true;
        foreach (InputValueClass inputValue in _InputValues)
            inputValue.UpdateInteractionSources(_InteractionObject, _RemoteObject, collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        _CollidingObjects.Remove(collision.collider.gameObject);

        foreach (InputValueClass inputValue in _InputValues)
            inputValue.UpdateInteractionSources(_InteractionObject, _RemoteObject, null);

        if (_CollidingObjects.Count == 0)
        {
            _IsColliding = true;
            foreach (EmitterAuthoring emitter in _HostedEmitters)
                emitter.UpdateCollision(null);
        }
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
        if (World.All.Count != 0 && _HostEntity != null)
            _EntityManager.DestroyEntity(_HostEntity);
    }
}
