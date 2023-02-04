using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using Unity.Entities.UniversalDelegates;
using System.Reflection;
using UnityEditor.Rendering;
using System;

public enum SiblingCollision { All, Single, None };

/// <summary>
//     Dynamically spawn/destroy child objects.
/// <summary>
public class ObjectSpawner : MonoBehaviour
{
    [Header("Runtime Dynamics")]
    [SerializeField] private bool _Initialised = false;
    [SerializeField] protected int _ObjectCounter = 0;
    [SerializeField] protected float _SecondsSinceSpawn = 0;
    [SerializeField] public HashSet<GameObject> _CollidedThisUpdate;

    [Header("Spawnable Prefab Selection")]
    [Tooltip("Currently selected prefab to spawn.")]
    public GameObject _PrefabToSpawn;
    public List<GameObject> _SpawnablePrefabs;
    public bool _RandomPrefab = false;
    public List<GameObject> _ActiveObjects = new List<GameObject>();

    [Header("Spawning Limits")]
    [Range(0, 100)] public int _SpawnablesAllocated = 10;
    [Range(0.01f, 2f)] public float _SpawnPeriodSeconds = 0.1f;
    public bool _AutoSpawn = true;
    public bool _AutoRemove = true;

    [Header("Spawnable Lifetime")]
    [Tooltip("Duration in seconds before destroying spawned object (0 = do not destroy based on duration).")]
    [Range(0, 60)] public float _ObjectLifespan = 0;
    [Tooltip("Subtract a random amount from a spawned object's lifespan by this fraction of its max duration.")]
    [Range(0, 1)] public float _LifespanVariance = 0;
    [Tooltip("Destroy object outside this radius from its spawning position.")]
    public float _DestroyRadius = 20f;
    
    [Header("Spawning Transform")]
    [Tooltip("Distance from the anchor that objects will be spawned.")]
    [Range(0f, 10f)] public float _EjectionRadius = 0.5f;
    [Tooltip("Default direction spawnables leave the anchor.")]
    public Vector3 _EjectionDirection = new Vector3(0,-1,0);
    [Tooltip("Apply random amount of spread to the direction for each spawn.")]
    [Range(0, 1)] public float _EjectionDirectionVariance = 0;
    [Tooltip("Default speed that spawnables leave the anchor.")]
    [Range(0, 100)] public float _EjectionSpeed = 0;

    [Header("Emitter Behaviour")]
    public bool _AllowSiblingSurfaceContact = true;
    public SiblingCollision _AllowSiblingCollisionBurst = SiblingCollision.Single;

    [Header("Object Configuration")]
    [Tooltip("Parent GameObject to attach spawned prefabs.")]
    public GameObject _SpawnableHost;
    [Tooltip("Object providing the spawn location and controller behaviour.")]
    public GameObject _ControllerObject;


    void Start()
    {
        InitialiseSpawner();
    }

    void Awake()
    {
        StartCoroutine(ClearCollisions());
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
        _SecondsSinceSpawn = float.IsFinite(_SecondsSinceSpawn) ? _SecondsSinceSpawn : 0;
        _SecondsSinceSpawn += Time.deltaTime;

        if (ReadyToSpawn())
        {
            if (_ActiveObjects.Count < _SpawnablesAllocated && _AutoSpawn)
                CreateNewSpawnable();
            else if (_ActiveObjects.Count > _SpawnablesAllocated && _AutoRemove)
                RemoveOverSpawnTarget();
        }
        _ActiveObjects.RemoveAll(item => item == null);
    }

    public bool ReadyToSpawn()
    {
        if (!_Initialised)
            return false;
        if ((_ControllerObject == null | _PrefabToSpawn == null) && !InitialiseSpawner())
            return false;
        if (_ActiveObjects.Count == _SpawnablesAllocated || _SecondsSinceSpawn < _SpawnPeriodSeconds)
            return false;
        return true;
    }

    private bool InitialiseSpawner()
    {
        _CollidedThisUpdate = new HashSet<GameObject>();

        if (_ControllerObject == null)
            _ControllerObject = gameObject;

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
        return true;
    }

    public void CreateNewSpawnable()
    {
        if (!InstantiatePrefab(out GameObject newObject))
        {
            _Initialised = false;
            return;
        }

        SpawnableManager spawnableManager = AttachSpawnableManager(newObject);
        ConfigureSpawnableBehaviour(newObject);
        ConfigureEmitterHost(newObject, spawnableManager);


        newObject.SetActive(true);
        _ActiveObjects.Add(newObject);
        _SecondsSinceSpawn = 0;
        _ObjectCounter++;
    }

    public bool InstantiatePrefab(out GameObject newObject)
    {
        int index = (!_RandomPrefab || _SpawnablePrefabs.Count < 2) ? Random.Range(0, _SpawnablePrefabs.Count) : 0;
        GameObject objectToSpawn = _SpawnablePrefabs[index];

        Vector3 spawnOnSphere = Random.insideUnitSphere.normalized;
        Vector3 spawnDirection = Vector3.Slerp(_EjectionDirection.normalized, spawnOnSphere, _EjectionDirectionVariance).normalized;
        Vector3 spawnPosition = _ControllerObject.transform.position + spawnDirection * _EjectionRadius;
        newObject = Instantiate(objectToSpawn, spawnPosition, Quaternion.Euler(spawnDirection), _SpawnableHost.transform);
        newObject.name = newObject.name + " (" + _ObjectCounter + ")";

        if (!newObject.TryGetComponent(out Rigidbody rb))
            rb = newObject.AddComponent<Rigidbody>();
        rb.velocity = spawnDirection * _EjectionSpeed;

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

    public void RemoveOverSpawnTarget()
    {
        if (_ActiveObjects[0] != null)
        {
            Destroy(_ActiveObjects[0]);
            _SecondsSinceSpawn = 0;
        }
        _ActiveObjects.RemoveAt(0);

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
