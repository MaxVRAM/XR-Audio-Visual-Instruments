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
    public GameObject _PrimaryObject;
    [Tooltip("An external object target used to calculate 'relative' values between the primary object with.")]
    public Rigidbody _PrimaryRigidBody;
    public GameObject _TargetObject;
    [Tooltip("Populate with emitters (general child objects) to manage with this component.")]
    public Rigidbody _TargetRigidBody;
    public EmitterAuthoring[] _HostedEmitters;
    [Tooltip("Dedicate a speaker for hosted emitters. Defaults to using attachment system to manage at runtime.")]
    public SpeakerAuthoring _DedicatedSpeaker;
    [Tooltip("Populates a list at start time of child object input value components to manage.")]
    protected int _SpeakerIndex;
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

        if (_PrimaryObject == null) _PrimaryObject = gameObject;
        if (_PrimaryRigidBody == null) _PrimaryRigidBody = _PrimaryObject.GetComponent<Rigidbody>();
        if (_TargetObject != null &&_TargetRigidBody == null)
            _TargetRigidBody = _TargetObject.GetComponent<Rigidbody>();

        _HostedEmitters = GetComponentsInChildren<EmitterAuthoring>();
        _InputValues = GetComponentsInChildren<InputValueClass>();

        foreach (InputValueClass inputValue in _InputValues)
            inputValue.UpdateInteractionSources(_PrimaryObject, _TargetObject, null);
    }

    void Awake()
    {
        GetComponent<ConvertToEntity>().ConversionMode = ConvertToEntity.Mode.ConvertAndInjectGameObject;
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        _HostEntity = entity;

        if (_DedicatedSpeaker != null)
        {
            foreach (EmitterAuthoring emitter in _HostedEmitters)
            {
                _DedicatedSpeaker.AddEmitterLink(emitter.gameObject);
                emitter.SetAttachedSpeaker(_SpeakerIndex);
            }
            _SpeakerIndex = _DedicatedSpeaker.GetRegisterAndGetIndex();
            dstManager.AddComponentData(_HostEntity, new FixedSpeakerLinkTag { });
        }
        else _SpeakerIndex = int.MaxValue;

        #region ADD EMITTER COMPONENT DATA
        dstManager.AddComponentData(_HostEntity, new EmitterHostComponent
        {
            _InListenerRadius = false,
            _DedicatedSpeaker = _DedicatedSpeaker != null,
            _SpeakerAttached = _DedicatedSpeaker != null,
            _SpeakerIndex = _SpeakerIndex,
            _NewSpeaker = false
        });
        #if UNITY_EDITOR
                dstManager.SetName(entity, "Emitter Host:   " + gameObject.name);
        #endif
        #endregion

        _Initialised = true;
    }

    public void SetTargetObject(GameObject target)
    {
        _TargetObject = target;
        if (_TargetObject != null &&_TargetRigidBody == null)
            _TargetRigidBody = _TargetObject.GetComponent<Rigidbody>();
        foreach (InputValueClass inputValue in _InputValues)
            inputValue.UpdateInteractionSources(_PrimaryObject, _TargetObject, null);
    }

    void Update()
    {
        if (!_Initialised)
            return;
        
        // Update emitter listener distance
        _CurrentDistance = Mathf.Abs((_HeadPosition.position - _PrimaryObject.transform.position).magnitude);
        foreach (EmitterAuthoring emitter in _HostedEmitters)
            if (emitter.UpdateDistanceFromListener(_CurrentDistance)) _InListenerRadius = true;
            else _InListenerRadius = false;

        // Update host's translation component
        Translation translation = _EntityManager.GetComponentData<Translation>(_HostEntity);
        _EntityManager.SetComponentData(_HostEntity, new Translation { Value = transform.position });

        // Apply component data processed by the systems in the previous frame
        EmitterHostComponent entity = _EntityManager.GetComponentData<EmitterHostComponent>(_HostEntity);
        entity._InListenerRadius = _InListenerRadius;
        _SpeakerIndex = entity._SpeakerIndex;
        if (entity._NewSpeaker)
            foreach (EmitterAuthoring emitter in _HostedEmitters)
                emitter.SetAttachedSpeaker(_SpeakerIndex);
        _EntityManager.SetComponentData(_HostEntity, entity);

        foreach (EmitterAuthoring emitter in _HostedEmitters)
        {
            emitter.ManualUpdate();
        }
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
            inputValue.UpdateInteractionSources(_PrimaryObject, _TargetObject, collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        _CollidingObjects.Remove(collision.collider.gameObject);

        foreach (InputValueClass inputValue in _InputValues)
            inputValue.UpdateInteractionSources(_PrimaryObject, _TargetObject, null);

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
