using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;

[DisallowMultipleComponent]
[RequireComponent(typeof(ConvertToEntity))]
public class HostAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    protected bool _Initialised = false;
    protected Entity _HostEntity;
    protected EntityManager _EntityManager;
    protected Transform _HeadPosition;

    [Tooltip("If a dedicated speaker is set, the runtime attachment system will be disabled for this host.")]
    public SpeakerAuthoring _DedicatedSpeaker;
    public int _SpeakerIndex = int.MaxValue;

    [Header("Input Value configuration")]
    [Tooltip("Objects to generate collision and modulation data with for the host's emitters. Defaults to this game object.")]
    public GameObject _LocalObject;
    [Tooltip("Additional object used to generate 'relative' values with, against the interaction object; 'distance from', etc.")]
    public GameObject _RemoteObject;
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

        _HostedEmitters = GetComponentsInChildren<EmitterAuthoring>();
        _InputValues = GetComponentsInChildren<InputValueClass>();

        if (_LocalObject == null) SetLocalObject(gameObject);
        if (_RemoteObject != null) SetRemoteObject(_RemoteObject);
        if (_DedicatedSpeaker != null)
            _SpeakerIndex = _DedicatedSpeaker.RegisterAndGetIndex();
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

    void Update()
    {
        if (!_Initialised)
            return;

        // Update emitter range status. Host will be "in-range" if at least one of its emitters return true.
        _CurrentDistance = Mathf.Abs((_HeadPosition.position - _LocalObject.transform.position).magnitude);
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

        if (_DedicatedSpeaker == null)
        {
            bool newSpeaker = false;
            if (_SpeakerIndex != entity._SpeakerIndex)
                newSpeaker = true;
            _SpeakerIndex = entity._SpeakerIndex;

            Debug.Log("Speaker index = " + _SpeakerIndex);

            foreach (EmitterAuthoring emitter in _HostedEmitters)
            {
                if (newSpeaker) emitter.ResetLastSampleIndex();
                emitter._SpeakerIndex = _SpeakerIndex;
            }
        }

        // Manually call the hosted emitters' component update function.
        foreach (EmitterAuthoring emitter in _HostedEmitters)
            emitter.ManualUpdate();

        _EntityManager.SetComponentData(_HostEntity, entity);

        foreach (InputValueClass input in _InputValues)
        {
        }
    }

    public void SetLocalObject(GameObject go)
    {
        _LocalObject = go;
        foreach (InputValueClass inputValue in _InputValues)
            inputValue._Inputs.SetLocalObject(_LocalObject);
    }

    public void SetRemoteObject(GameObject go)
    {
        _RemoteObject = go;
        foreach (InputValueClass inputValue in _InputValues)
            inputValue._Inputs.SetRemoteObject(_RemoteObject);
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        _CollidingObjects.Add(collision.collider.gameObject);

        foreach (InputValueClass interaction in _InputValues)
            interaction.SetInputCollision(true, collision.collider.material);

        foreach (EmitterAuthoring emitter in _HostedEmitters)
            emitter.NewCollision(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        _IsColliding = true;
        foreach (InputValueClass inputValue in _InputValues)
            inputValue.ProcessCollisionValue(collision);

        foreach (EmitterAuthoring emitter in _HostedEmitters)
            emitter.UpdateContactStatus(collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        _CollidingObjects.Remove(collision.collider.gameObject);

        foreach (InputValueClass inputValue in _InputValues)
            inputValue.SetInputCollision(false, collision.collider.material);

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
        DestroyEntity();
    }

    public void DestroyEntity()
    {
        if (World.All.Count != 0 && _HostEntity != null)
            _EntityManager.DestroyEntity(_HostEntity);
    }
}
