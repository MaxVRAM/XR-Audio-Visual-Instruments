using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;

using MaxVRAM.Audio;
using NaughtyAttributes;

namespace PlaneWaver
{
    /// <summary>
    //      Multiple emitters are often spawned and attached to the same object and modulated by
    //      the same game-world interactions. Emitter Hosts manage emitters designed to create
    //      a single "profile" for an interactive sound object.
    /// <summary>
    public class HostAuthoring : SynthEntity
    {
        #region FIELDS & PROPERTIES

        private Transform _HeadTransform;

        [AllowNesting]
        [BoxGroup("Speaker Assignment")]
        [Tooltip("Parent transform position for speakers to target for this host. Defaults to this transform.")]
        [SerializeField] private Transform _SpeakerTarget;
        [AllowNesting]
        [BoxGroup("Speaker Assignment")]
        [SerializeField] private Transform _SpeakerTransform;

        private AttachmentLine _AttachmentLine;

        [AllowNesting]
        [BoxGroup("Interaction")]
        public ObjectSpawner _Spawner;
        [AllowNesting]
        [BoxGroup("Interaction")]
        public SpawnableManager _SpawnLife;
        [AllowNesting]
        [BoxGroup("Interaction")]
        public Actor _LocalActor;
        [AllowNesting]
        [BoxGroup("Interaction")]
        public Actor _RemoteActor;
        [AllowNesting]
        [BoxGroup("Interaction")]
        [Tooltip("(runtime) Paired component that pipes collision data from the local object target to this host.")]
        public CollisionPipe _CollisionPipeComponent;
        [AllowNesting]
        [BoxGroup("Interaction")]
        [SerializeField] private float _SelfRigidity = 0.5f;
        [BoxGroup("Interaction")]
        [AllowNesting]
        [SerializeField] private float _EaseCollidingRigidity = 0.5f;
        [AllowNesting]
        [BoxGroup("Interaction")]
        [SerializeField] private float _CollidingRigidity = 0;
        private float _TargetCollidingRigidity = 0;

        public float SurfaceRigidity => _SelfRigidity;
        public float RigiditySmoothUp => 1 / _EaseCollidingRigidity;
        public float CollidingRigidity => _CollidingRigidity;

        private SurfaceProperties _SurfaceProperties;

        [HorizontalLine(color: EColor.Gray)]
        public List<EmitterAuthoring> _HostedEmitters;
        public List<GameObject> _CollidingObjects;

        [AllowNesting]
        [Foldout("Runtime Dynamics")]
        [SerializeField] private bool _Connected = false;
        [AllowNesting]
        [Foldout("Runtime Dynamics")]
        [SerializeField] private int _AttachedSpeakerIndex = int.MaxValue;
        [AllowNesting]
        [Foldout("Runtime Dynamics")]
        [SerializeField] private bool _InListenerRadius = false;
        [AllowNesting]
        [Foldout("Runtime Dynamics")]
        [SerializeField] private float _ListenerDistance = 0;
        [AllowNesting]
        [Foldout("Runtime Dynamics")]
        public bool _IsColliding = false;

        public bool IsConnected => _Connected;
        public int AttachedSpeakerIndex => _AttachedSpeakerIndex;
        public bool InListenerRadius => _InListenerRadius;

        #endregion

        #region ENTITY-SPECIFIC START CALL

        void Awake()
        {
        }

        void Start()
        {
            InitialiseModules();
            InitialiseEmitters();

            _HeadTransform = FindObjectOfType<Camera>().transform;
            _SpeakerTarget = _SpeakerTarget != null ? _SpeakerTarget : transform;
            _SpeakerTransform = _SpeakerTarget;

            if (_AttachmentLine = TryGetComponent(out _AttachmentLine) ? _AttachmentLine : gameObject.AddComponent<AttachmentLine>())
            _AttachmentLine._TransformA = _SpeakerTarget;

            _EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _Archetype = _EntityManager.CreateArchetype(
                typeof(Translation),
                typeof(HostComponent));

            SetIndex(GrainSynth.Instance.RegisterHost(this));
        }

        public void InitialiseModules()
        {
            _LocalActor = _LocalActor.Exists() ? _LocalActor : new Actor(transform);
            _RemoteActor = _RemoteActor.Exists() ? _RemoteActor : new Actor(false);

            _SpawnLife = _SpawnLife != null ? _SpawnLife : gameObject.AddComponent<SpawnableManager>();

            if (TryGetComponent(out _SurfaceProperties) || _LocalActor.ActorGameObject.TryGetComponent(out _SurfaceProperties))
                _SelfRigidity = _SurfaceProperties._Rigidity;

            foreach (BehaviourClass behaviour in GetComponents<BehaviourClass>())
            {
                behaviour._SpawnedObject = _LocalActor.ActorGameObject;
                behaviour._ControllerObject = _RemoteActor.ActorGameObject;
                behaviour._ObjectSpawner = _Spawner;
            }

            if (!_LocalActor.ActorGameObject.TryGetComponent(out Collider _))
                return;
            // Set up a collision pipe to send collisions from the targeted object here. TODO: Move to event system via Actor struct
            _CollisionPipeComponent = _LocalActor.ActorGameObject.TryGetComponent(out _CollisionPipeComponent) ?
                _CollisionPipeComponent : _LocalActor.ActorGameObject.AddComponent<CollisionPipe>();
            _CollisionPipeComponent.AddHost(this);
        }

        public void InitialiseEmitters()
        {
            if (transform.parent != null)
                _HostedEmitters.AddRange(transform.parent.GetComponentsInChildren<EmitterAuthoring>());
            else
                _HostedEmitters.AddRange(gameObject.GetComponentsInChildren<EmitterAuthoring>());

            foreach (EmitterAuthoring emitter in _HostedEmitters)
                emitter.InitialiseHostParameters(this, _LocalActor, _RemoteActor);
        }

        #endregion

        #region HEFTY HOST COMPONENT BUSINESS

        public override void SetEntityType()
        {
            _EntityType = SynthEntityType.Host;
        }

        public override void InitialiseComponents()
        {
            _EntityManager.SetComponentData(_Entity, new Translation { Value = _SpeakerTarget.position });
            _EntityManager.SetComponentData(_Entity, new HostComponent
            {
                _HostIndex = _EntityIndex,
                _Connected = false,
                _SpeakerIndex = int.MaxValue,
                _InListenerRadius = InListenerRadius
            });
        }

        public override void ProcessComponents()
        {
            _EntityManager.SetComponentData(_Entity, new Translation { Value = _SpeakerTarget.position });

            HostComponent hostData = _EntityManager.GetComponentData<HostComponent>(_Entity);
            hostData._HostIndex = _EntityIndex;
            _EntityManager.SetComponentData(_Entity, hostData);

            bool connected = GrainSynth.Instance.IsSpeakerAtIndex(hostData._SpeakerIndex, out SpeakerAuthoring speaker);
            _InListenerRadius = hostData._InListenerRadius;
            _SpeakerTransform = connected ? speaker.gameObject.transform : _SpeakerTarget;
            _AttachedSpeakerIndex = connected ? hostData._SpeakerIndex : int.MaxValue;
            _Connected = connected;

            UpdateSpeakerAttachmentLine();
            ProcessRigidity();

            float speakerAmplitudeFactor = ScaleAmplitude.SpeakerOffsetFactor(
                transform.position,
                _HeadTransform.position,
                _SpeakerTransform.position);

            _ListenerDistance = Mathf.Abs((_SpeakerTarget.position - _HeadTransform.position).magnitude);

            foreach (EmitterAuthoring emitter in _HostedEmitters)
            {
                emitter.UpdateDistanceAmplitude(_ListenerDistance / GrainSynth.Instance._ListenerRadius, speakerAmplitudeFactor);
            }
        }

        public override void Deregister()
        {
            if (GrainSynth.Instance != null)
                GrainSynth.Instance.DeregisterHost(this);
            if (_CollisionPipeComponent != null)
                _CollisionPipeComponent.RemoveHost(this);
        }

        #endregion

        #region BEHAVIOUR UPDATES

        public void ProcessRigidity()
        {
            // Clear lingering null contact objects and find most rigid collider value
            _CollidingObjects.RemoveAll(item => item == null);
            _TargetCollidingRigidity = 0;

            foreach (GameObject go in _CollidingObjects)
                if (go.TryGetComponent(out SurfaceProperties props))
                {
                    _TargetCollidingRigidity = _TargetCollidingRigidity > props._Rigidity ? _TargetCollidingRigidity : props._Rigidity;
                }
            // Smooth transition to upward rigidity values to avoid randomly triggering surface contact emitters from short collisions
            if (_TargetCollidingRigidity < CollidingRigidity + 0.001f || _EaseCollidingRigidity <= 0)
                _CollidingRigidity = _TargetCollidingRigidity;
            else
                _CollidingRigidity = CollidingRigidity.Lerp(_TargetCollidingRigidity, RigiditySmoothUp * Time.deltaTime);
            if (CollidingRigidity < 0.001f) _CollidingRigidity = 0;
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

        #endregion

        #region COLLISION HANDLING

        public void OnCollisionEnter(Collision collision)
        {
            GameObject other = collision.collider.gameObject;
            _CollidingObjects.Add(other);

            if (ContactAllowed(other)) _LocalActor.LatestCollision = collision;

            if (_Spawner == null || _Spawner.UniqueCollision(_LocalActor.ActorGameObject, other))
                foreach (EmitterAuthoring emitter in _HostedEmitters)
                    emitter.NewCollision(collision);
        }

        public void OnCollisionStay(Collision collision)
        {
            Collider collider = collision.collider;
            _IsColliding = true;

            if (ContactAllowed(collider.gameObject))
            {
                //foreach (ModulationSource source in _ModulationSources)
                //    source.SetInputCollision(true, collider.material);
                _LocalActor.IsColliding = true;

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
                _LocalActor.IsColliding = false;
                _TargetCollidingRigidity = 0;
                _CollidingRigidity = 0;
                //foreach (ModulationSource source in _ModulationSources)
                //    source.SetInputCollision(false, collision.collider.material);
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
}
