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
    protected Transform _HeadTransform;

    [Header("Speaker assignment")]
    [Tooltip("If a dedicated speaker is set, the runtime attachment system will be disabled for this host.")]
    public SpeakerAuthoring _DedicatedSpeaker;
    protected Transform _SpeakerTransform;
    public int _SpeakerIndex = int.MaxValue;
    public bool _Connected = false;

    [Header("Emitter modulation inputs")]
    [Tooltip("Object to generate collision and modulation data with for the host's emitters. Defaults to this game object.")]
    public GameObject _LocalObject;
    [Tooltip("Additional object used to generate 'relative' values with against the interaction object. E.g. distance, relative speed, etc.")]
    public GameObject _RemoteObject;
    [Tooltip("Finds all emitter components in children to manage.")]

    public EmitterAuthoring[] _HostedEmitters;
    [Tooltip("Finds all modulation input components in children to manage.")]
    public InputValueClass[] _InputValues;

    [Header("Runtime dynamics")]
    public bool _IsColliding = false;
    public bool _InListenerRadius = false;
    public float _ListenerDistance = 0;
    public List<GameObject> _CollidingObjects;


    void Start()
    {
        _EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _HeadTransform = FindObjectOfType<Camera>().transform;

        _HostedEmitters = GetComponentsInChildren<EmitterAuthoring>();
        _InputValues = GetComponentsInChildren<InputValueClass>();

        if (_LocalObject == null) SetLocalObject(gameObject);
        if (_RemoteObject != null) SetRemoteObject(_RemoteObject);
        if (_DedicatedSpeaker != null)
        {
            _DedicatedSpeaker.AddEmitterLink(gameObject);                
            _SpeakerIndex = _DedicatedSpeaker.RegisterAndGetIndex();
            _SpeakerTransform = _DedicatedSpeaker.transform;
            _Connected = true;
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

    void Update()
    {
        if (!_Initialised)
            return;


        foreach (InputValueClass input in _InputValues)
        {
            Debug.Log("Input Value:    " + input.name + "      GetValue = " + input.GetValue());
            
        }

        // Update host range status.
        _ListenerDistance = Mathf.Abs((_LocalObject.transform.position - _HeadTransform.position).magnitude);

        // Update host translation component.
        Translation translation = _EntityManager.GetComponentData<Translation>(_HostEntity);
        _EntityManager.SetComponentData(_HostEntity, new Translation { Value = transform.position });

        // Update host component data.
        EmitterHostComponent hostEntity = _EntityManager.GetComponentData<EmitterHostComponent>(_HostEntity);        

        if (_DedicatedSpeaker != null)
        {
            hostEntity._Connected = true;
            hostEntity._HasDedicatedSpeaker = true;
            hostEntity._SpeakerIndex = _SpeakerIndex;
            hostEntity._InListenerRadius = _ListenerDistance < GrainSynth.Instance._ListenerRadius;
        }
        else
        {
            _InListenerRadius = hostEntity._InListenerRadius;
            hostEntity._HasDedicatedSpeaker = false;
            _Connected = hostEntity._Connected;
            if (hostEntity._SpeakerIndex < GrainSynth.Instance._Speakers.Count)
            {               
                _SpeakerIndex = hostEntity._SpeakerIndex; 
                _SpeakerTransform = DynamicSpeaker.gameObject.transform;
                if (_SpeakerIndex != hostEntity._SpeakerIndex)
                {
                    Debug.Log(name + "   assigned new speaker:   " + _SpeakerIndex);
                    foreach (EmitterAuthoring emitter in _HostedEmitters)
                        emitter.ResetLastSampleIndex();
                }
            }
            else _Connected = false;
        }
        _EntityManager.SetComponentData(_HostEntity, hostEntity);

        float speakerFactor = 0;

        if (_Connected)
        {
            speakerFactor = AudioUtils.SpeakerOffsetFactor(
                _LocalObject.transform.position,
                _HeadTransform.position,
                _SpeakerTransform.position);
        }

        foreach (EmitterAuthoring emitter in _HostedEmitters)
        {
            emitter.UpdateDistanceAmplitude(_ListenerDistance, speakerFactor);
            emitter._InListenerRadius = _InListenerRadius;
            emitter._SpeakerIndex = _SpeakerIndex;
            emitter._Connected = _Connected;
            emitter.ManualUpdate();
        }
    }
    
    public SpeakerAuthoring DynamicSpeaker { get { return GrainSynth.Instance._Speakers[_SpeakerIndex]; } }

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
