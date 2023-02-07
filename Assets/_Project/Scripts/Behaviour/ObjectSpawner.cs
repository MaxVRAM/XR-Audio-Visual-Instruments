using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using System;
using UnityEditor;
using MaxVRAM;

public enum SiblingCollision { All, Single, None };

/// <summary>
//     Dynamically spawn/destroy child objects.
/// <summary>
public class ObjectSpawner : MonoBehaviour
{
    [Header("Runtime Dynamics")]
    [SerializeField] private bool _Initialised = false;
    [SerializeField] private int _ObjectCounter = 0;
    [SerializeField] private float _SecondsSinceSpawn = 0;
    [SerializeField] public HashSet<GameObject> _CollidedThisUpdate;

    [Header("Spawnable Prefab Selection")]
    [Tooltip("Currently selected prefab to spawn.")]
    [SerializeField] private GameObject _PrefabToSpawn;
    [SerializeField] private List<GameObject> _SpawnablePrefabs;
    [SerializeField] private bool _RandomPrefab = false;
    public List<GameObject> _ActiveObjects = new List<GameObject>();

    [Header("Spawning Limits")]
    [Tooltip("Number of seconds after this ObjectSpawner is created before it starts spawning loop.")]
    [SerializeField] private float _WaitBeforeSpawning = 2;
    private float _StartTime = int.MaxValue;
    private bool _StartTimeReached = false;
    [SerializeField] [Range(0, 100)] private int _SpawnablesAllocated = 10;
    [Tooltip("Period (seconds) to instantiate/destroy spawnables.")]
    [Range(0.01f, 1)][SerializeField] private float _SpawnPeriodSeconds = 0.2f;
    private ActionTimer _SpawnTimer;
    [SerializeField] private bool _AutoSpawn = true;
    [SerializeField] private bool _AutoRemove = true;

    [Header("Spawnable Lifetime")]
    [Tooltip("Duration in seconds before destroying spawned object (0 = do not destroy based on duration).")]
    [Range(0, 60)] public float _ObjectLifespan = 0;
    [Tooltip("Subtract a random amount from a spawned object's lifespan by this fraction of its max duration.")]
    [Range(0, 1)] public float _LifespanVariance = 0;
    [Tooltip("Destroy object outside this radius from its spawning position.")]
    public float _DestroyRadius = 20f;
    
    [Header("Ejection Position")]
    [Tooltip("Distance from the anchor that objects will be spawned.")]
    [Range(0f, 10f)] public float _EjectionRadius = 0.5f;
    [Tooltip("Randomise spawn position. 0 = objects will always spawn at the Ejection Position. 0.5 = objects spawn randomly within one hemisphere of the Ejection Position")]
    [Range(0, 1)] public float _EjectionPositionVariance = 0;
    [Tooltip("Normalised default position that spawnables leave the anchor.")]
    public Vector3 _EjectionPosition = new Vector3(0, -1, 0);

    [Header("Ejection Velocity")]
    [Tooltip("Default speed that spawnables leave the anchor.")]
    [Range(0, 100)] public float _EjectionSpeed = 0;
    [Tooltip("Apply random amount of spread to the direction for each spawn.")]
    [Range(0, 1)] public float _EjectionDirectionVariance = 0;
    [Tooltip("Normalised velocity that spawnables have at the spawn time.")]
    public Vector3 _EjectionDirection = new Vector3(0, 0, 0);

    [Header("Emitter Behaviour")]
    public bool _AllowSiblingSurfaceContact = true;
    public SiblingCollision _AllowSiblingCollisionBurst = SiblingCollision.Single;

    [Header("Object Configuration")]
    [Tooltip("Parent GameObject to attach spawned prefabs.")]
    public GameObject _SpawnableHost;
    [Tooltip("Object providing the spawn location and controller behaviour.")]
    public GameObject _ControllerObject;
    [SerializeField] private Renderer _ControllerRenderer;
    [SerializeField] private float _EmissiveIntensity = 0;
    [SerializeField] private Color _EmissiveColour;


    void Start()
    {
        if (!InitialiseSpawner())
            gameObject.SetActive(false);
    }

    void Awake()
    {
        StartCoroutine(ClearCollisions());
        _SpawnTimer = new ActionTimer(TimeUnit.sec, _SpawnPeriodSeconds);
    }

    IEnumerator ClearCollisions()
    {
        while (true)
        {
            yield return new WaitForEndOfFrame();
            _CollidedThisUpdate.Clear();
        }
    }


    void Update()
    {
        _SpawnTimer.UpdateTrigger(Time.deltaTime, _SpawnPeriodSeconds);

        if (ReadyToSpawn())
        {
            if (_ActiveObjects.Count < _SpawnablesAllocated && _AutoSpawn && _SpawnTimer.DrainTrigger())
            {
                _ActiveObjects.Add(CreateSpawnable());
                _EmissiveIntensity = 10;
            }
            else if (_ActiveObjects.Count > _SpawnablesAllocated && _AutoRemove && _SpawnTimer.DrainTrigger())
                RemoveSpawnable(0);
        }
        _ActiveObjects.RemoveAll(item => item == null);

        UpdateShaderModulation();
    }

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

    private bool InitialiseSpawner()
    {
        _CollidedThisUpdate = new HashSet<GameObject>();

        if (_ControllerObject == null)
            _ControllerObject = gameObject;

        _ControllerRenderer = GetComponentInChildren<Renderer>();
        if (_ControllerRenderer != null)
            _EmissiveColour = _ControllerRenderer.material.GetColor("_EmissiveColor");

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

    public GameObject CreateSpawnable()
    {
        InstantiatePrefab(out GameObject newObject);
        SpawnableManager spawnableManager = AttachSpawnableManager(newObject);
        ConfigureSpawnableBehaviour(newObject);
        ConfigureEmitterHost(newObject, spawnableManager);
        newObject.SetActive(true);
        return newObject;
    }

    public bool InstantiatePrefab(out GameObject newObject)
    {
        int index = (!_RandomPrefab || _SpawnablePrefabs.Count < 2) ? Random.Range(0, _SpawnablePrefabs.Count) : 0;
        GameObject objectToSpawn = _SpawnablePrefabs[index];

        Vector3 spawnOnSphere = Random.onUnitSphere;
        Vector3 spawnPositionOffset = Vector3.Slerp(_EjectionPosition.normalized, spawnOnSphere, _EjectionPositionVariance);
        Vector3 spawnPosition = _ControllerObject.transform.position + spawnPositionOffset * _EjectionRadius;

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
        Vector3 spawnVelocity = rotatedDirection * _EjectionSpeed;
        rb.velocity = spawnVelocity;
        return true;
    }

    public SpawnableManager AttachSpawnableManager(GameObject go)
    {
        if (!go.TryGetComponent(out SpawnableManager spawnableManager))
            spawnableManager = go.AddComponent<SpawnableManager>();
        if (_ObjectLifespan > 0)
            spawnableManager._Lifespan = _ObjectLifespan - _ObjectLifespan * Random.Range(0, _LifespanVariance);
        spawnableManager._DestroyRadius = _DestroyRadius;
        spawnableManager._SpawnedObject = go;
        spawnableManager._ObjectSpawner = this;
        return spawnableManager;
    }

    public void ConfigureSpawnableBehaviour(GameObject go)
    {
        if (go.TryGetComponent(out BehaviourClass behaviour))
        {
            behaviour._SpawnedObject = go;
            behaviour._ControllerObject = _ControllerObject;
            behaviour._ObjectSpawner = this;
        }
    }

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

    public void RemoveSpawnable(int index)
    {
        if (_ActiveObjects[index] != null)
            Destroy(_ActiveObjects[index]);
        _ActiveObjects.RemoveAt(index);

    }

    public void UpdateShaderModulation()
    {
        if (_EmissiveColour != null)
        {
            _ControllerRenderer.material.SetColor("_EmissiveColor", _EmissiveColour * _EmissiveIntensity);
            float glow = (1 + Mathf.Sin(_SecondsSinceSpawn / _SpawnPeriodSeconds * 2)) * 0.5f;
            _EmissiveIntensity = Mathf.Lerp(_EmissiveIntensity, glow, Time.deltaTime * 4);
        }
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
}
