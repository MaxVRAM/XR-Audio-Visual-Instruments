using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using System;

using GD.MinMaxSlider;

using MaxVRAM.Math;
using MaxVRAM.Ticker;
using PlaneWaver.Synthesis;


namespace PlaneWaver.Interaction
{
    /// <summary>
    //  Manager for spawning child game objects with a variety of existance and controller behaviours.
    /// <summary>
    public class ObjectSpawner : MonoBehaviour
    {
        #region FIELDS & PROPERTIES

        public enum SiblingCollision { All, Single, None };
        public enum BoundingArea { Unrestricted, Spawn, Controller, ControllerBounds }

        [Header("Runtime Dynamics")]
        [SerializeField] private bool _Initialised = false;
        [SerializeField] private int _ObjectCounter = 0;
        [SerializeField] private float _SecondsSinceSpawn = 0;
        [SerializeField] public HashSet<GameObject> _CollidedThisUpdate;

        [Header("Object Configuration")]
        [Tooltip("Object providing the spawn location and controller behaviour.")]
        public GameObject _ControllerObject;
        [Tooltip("Parent GameObject to attach spawned prefabs.")]
        public GameObject _SpawnableHost;
        [Tooltip("Prefab to spawn.")]
        [SerializeField] private GameObject _PrefabToSpawn;
        [SerializeField] private bool _RandomiseSpawnPrefab = false;
        [Tooltip("A list of prebs to spawn can also be supplied, allowing runtime selection of object spawn selection.")]
        [SerializeField] private List<GameObject> _SpawnablePrefabs;
        public List<GameObject> _ActiveObjects = new List<GameObject>();

        [Header("Spawning Rules")]
        [Tooltip("Number of seconds after this ObjectSpawner is created before it starts spawning loop.")]
        [SerializeField] private float _WaitBeforeSpawning = 2;
        private float _StartTime = int.MaxValue;
        private bool _StartTimeReached = false;
        [SerializeField][Range(0, 100)] private int _SpawnablesAllocated = 10;
        [Tooltip("Period (seconds) to instantiate/destroy spawnables.")]
        [Range(0.01f, 1)][SerializeField] private float _SpawnPeriodSeconds = 0.2f;
        private Trigger _SpawnTimer;
        [SerializeField] private bool _AutoSpawn = true;
        [SerializeField] private bool _AutoRemove = true;

        [Header("Spawned Object Removal")]
        public bool _DestroyOnCollision = false;
        [Tooltip("Coodinates that define the bounding for spawned objects, which are destroyed if they leave. The bounding radius is ignored when using Controller Bounds, defined instead by the controller's collider bounds.")]
        public BoundingArea _BoundingAreaType = BoundingArea.Controller;
        [Tooltip("Radius of the bounding volume.")]
        public float _BoundingRadius = 30f;
        [Tooltip("Use a timer to destroy spawned objects after a duration.")]
        [SerializeField] private bool _UseSpawnDuration = true;
        [Tooltip("Duration in seconds before destroying spawned object.")]
        [MinMaxSlider(0f, 60f)] public Vector2 _SpawnObjectDuration = new Vector2(5, 10);

        public enum ControllerEvent { All, OnSpawn, OnCollision }
        [Header("Visual Feedback")]
        [SerializeField] private ControllerEvent _EmissiveFlashEvent = ControllerEvent.OnSpawn;
        [Tooltip("Emissive brightness range to modulate associated renderers. X = base emissive brightness, Y = brightness on event trigger.")]
        [MinMaxSlider(-10f, 10f)] public Vector2 _EmissiveBrightness = new Vector2(0, 10);
        [Tooltip("Supply list of renderers to modulate/flash emissive brightness on selected triggers.")]
        [SerializeField] private List<Renderer> _ControllerRenderers = new List<Renderer>();
        private List<Color> _EmissiveColours = new List<Color>();
        private float _EmissiveIntensity = 0;

        [Header("Ejection Position")]
        [Tooltip("Distance from the anchor that objects will be spawned.")]
        [MinMaxSlider(0f, 10f)] public Vector2 _EjectionRadiusRange = new Vector2(1, 2);
        [Tooltip("Normalised default position that spawnables leave the anchor.")]
        public Vector3 _EjectionPosition = new Vector3(0, -1, 0);
        [Tooltip("Randomise spawnable ejection position. 0 = Always spawn at defined position. 0.5 = Spawn within one hemisphere of ejection position. 1.0 = Spawn at any angle around the position.")]
        [Range(0, 1)] public float _EjectionPositionVariance = 0;

        [Header("Ejection Velocity")]
        [Tooltip("Speed that spawned objects leave the anchor.")]
        [MinMaxSlider(0f, 100f)] public Vector2 _EjectionSpeedRange = new Vector2(5, 10);
        [Tooltip("Normalised velocity that spawnables have at the spawn time.")]
        public Vector3 _EjectionDirection = new Vector3(0, 0, 0);
        [Tooltip("Apply random amount of spread to the direction for each spawn.")]
        [Range(0f, 1f)] public float _EjectionDirectionVariance = 0;

        [Header("Emitter Behaviour")]
        public bool _AllowSiblingSurfaceContact = true;
        public SiblingCollision _AllowSiblingCollisionBurst = SiblingCollision.Single;

        #endregion

        #region INITIALISATION

        void Start()
        {
            if (!InitialiseSpawner())
                gameObject.SetActive(false);
        }

        void Awake()
        {
            StartCoroutine(ClearCollisions());
            _SpawnTimer = new Trigger(TimeUnit.seconds, _SpawnPeriodSeconds);
        }

        IEnumerator ClearCollisions()
        {
            while (true)
            {
                yield return new WaitForEndOfFrame();
                _CollidedThisUpdate.Clear();
            }
        }

        private bool InitialiseSpawner()
        {
            _CollidedThisUpdate = new HashSet<GameObject>();

            if (_ControllerObject == null)
                _ControllerObject = gameObject;

            foreach (Renderer renderer in _ControllerRenderers)
            {
                Color colour = renderer.material.GetColor("_EmissiveColor");
                _EmissiveColours.Add(colour);
            }

            if (_SpawnableHost == null)
                _SpawnableHost = gameObject;

            if (_SpawnablePrefabs.Count == 0 && _PrefabToSpawn != null)
                _SpawnablePrefabs.Add(_PrefabToSpawn);

            if (_SpawnablePrefabs.Count >= 1 && _PrefabToSpawn == null)
                _PrefabToSpawn = _SpawnablePrefabs[0];

            if (_SpawnablePrefabs.Count == 0)
            {
                Debug.LogWarning($"{name} not assigned any prefabs!");
                _Initialised = false;
                return false;
            }

            _Initialised = true;
            _StartTime = Time.time + _WaitBeforeSpawning;
            return true;
        }

        #endregion

        #region UPDATE SCHEDULE

        void Update()
        {
            _SpawnTimer.UpdateTrigger(Time.deltaTime, _SpawnPeriodSeconds);

            if (ReadyToSpawn())
                if (_AutoSpawn) CreateSpawnable();
                else if (_AutoRemove) RemoveSpawnable(0);

            _ActiveObjects.RemoveAll(item => item == null);
            UpdateShaderModulation();
        }

        #endregion

        #region SPAWN MANAGEMENT

        public bool ReadyToSpawn()
        {
            if (!_Initialised)
                return false;
            if (!_StartTimeReached)
            {
                if (Time.time > _StartTime)
                    _StartTimeReached = true;
                else
                    return false;
            }
            if ((_ControllerObject == null | _PrefabToSpawn == null) && !InitialiseSpawner())
                return false;
            return true;
        }

        public void CreateSpawnable()
        {
            if (_ActiveObjects.Count >= _SpawnablesAllocated || !_SpawnTimer.DrainTrigger())
                return;

            InstantiatePrefab(out GameObject newObject);
            SpawnableManager spawnableManager = AttachSpawnableManager(newObject);
            ConfigureSpawnableBehaviour(newObject);
            ConfigureEmitterHost(newObject, spawnableManager);
            newObject.SetActive(true);
            _ActiveObjects.Add(newObject);

            if (_EmissiveFlashEvent == ControllerEvent.OnSpawn)
                _EmissiveIntensity = _EmissiveBrightness.y;
        }

        public void RemoveSpawnable(int index)
        {
            if (_ActiveObjects.Count <= _SpawnablesAllocated || !_SpawnTimer.DrainTrigger())
                return;
            if (_ActiveObjects[index] != null)
                Destroy(_ActiveObjects[index]);
            _ActiveObjects.RemoveAt(index);

        }

        public bool InstantiatePrefab(out GameObject newObject)
        {
            int index = (!_RandomiseSpawnPrefab || _SpawnablePrefabs.Count < 2) ? Random.Range(0, _SpawnablePrefabs.Count) : 0;
            GameObject objectToSpawn = _SpawnablePrefabs[index];

            Vector3 spawnOnSphere = Random.onUnitSphere;
            Vector3 spawnPositionOffset = Vector3.Slerp(_EjectionPosition.normalized, spawnOnSphere, _EjectionPositionVariance);
            Vector3 spawnPosition = _ControllerObject.transform.position + spawnPositionOffset * Random.Range(_EjectionRadiusRange.x, _EjectionRadiusRange.y);

            newObject = Instantiate(objectToSpawn, spawnPosition, Quaternion.identity, _SpawnableHost.transform);
            newObject.name = newObject.name + " (" + _ObjectCounter + ")";

            if (!newObject.TryGetComponent(out Rigidbody rb))
                rb = newObject.AddComponent<Rigidbody>();

            spawnOnSphere = Random.onUnitSphere.normalized;
            Vector3 spawnDirection = Vector3.Slerp(_EjectionDirection.normalized, spawnOnSphere, _EjectionDirectionVariance);

            if (spawnDirection == Vector3.zero)
                return true;

            Vector3 directionVector = (_ControllerObject.transform.position - spawnPosition).normalized;
            Quaternion directionRotation = Quaternion.FromToRotation(spawnDirection, directionVector);
            Vector3 rotatedDirection = directionRotation * directionVector;
            Vector3 spawnVelocity = rotatedDirection * Random.Range(_EjectionSpeedRange.x, _EjectionSpeedRange.y);
            rb.velocity = spawnVelocity;
            return true;
        }

        public SpawnableManager AttachSpawnableManager(GameObject go)
        {
            if (!go.TryGetComponent(out SpawnableManager spawnableManager))
                spawnableManager = go.AddComponent<SpawnableManager>();

            spawnableManager._Lifespan = _UseSpawnDuration ? Rando.Range(_SpawnObjectDuration) : int.MaxValue;
            spawnableManager._BoundingArea = _BoundingAreaType;
            spawnableManager._BoundingRadius = _BoundingRadius;
            return spawnableManager;
        }

        public void ConfigureSpawnableBehaviour(GameObject go)
        {
            foreach (var behaviour in go.GetComponents<BehaviourClass>())
            {
                behaviour._SpawnedObject = go;
                behaviour._ControllerObject = _ControllerObject;
                behaviour._ObjectSpawner = this;
            }
        }

        // !TODO: Decouple Synthesis authoring from this
        public void ConfigureEmitterHost(GameObject go, SpawnableManager spawnable)
        {
            if (!go.TryGetComponent(out HostAuthoring newHost))
                newHost = go.GetComponentInChildren(typeof(HostAuthoring), true) as HostAuthoring;

            if (newHost == null)
                return;

            newHost._Spawner = this;
            newHost._LocalObject = go;
            newHost._RemoteObject = _ControllerObject;
            newHost.AddBehaviourInputSource(spawnable);
        }

        #endregion

        #region RUNTIME MODULATIONS

        public void UpdateShaderModulation()
        {
            for (int i = 0; i < _ControllerRenderers.Count; i++)
            {
                _ControllerRenderers[i].material.SetColor("_EmissiveColor", _EmissiveColours[i] * _EmissiveIntensity * 2);
            }

            float glow = _EmissiveBrightness.x + (1 + Mathf.Sin(_SecondsSinceSpawn / _SpawnPeriodSeconds * 2)) * 0.5f;
            _EmissiveIntensity = Mathf.Lerp(_EmissiveIntensity, glow, Time.deltaTime * 4);
        }

        public bool UniqueCollision(GameObject goA, GameObject goB)
        {
            if (!_ActiveObjects.Contains(goA) || !_ActiveObjects.Contains(goB) || _AllowSiblingCollisionBurst == SiblingCollision.All)
                return true;
            else if (_AllowSiblingCollisionBurst == SiblingCollision.None)
                return false;
            else if (_AllowSiblingCollisionBurst == SiblingCollision.Single &
                    _CollidedThisUpdate.Add(goA) | _CollidedThisUpdate.Add(goB))
                return true;
            else
                return false;
        }

        #endregion
    }
}