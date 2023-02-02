using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using Unity.Entities.UniversalDelegates;


    public enum SiblingCollision { All, Single, None };

/// <summary>
//     Dynamically spawn/destroy child objects.
/// <summary>
public class ObjectSpawner : MonoBehaviour
{

    [Header("Speaker Assignment")]
    [Tooltip("Force spawned emitters to use this speaker. Leave null to use dynamic allocation.")]
    public SpeakerAuthoring _DedicatedSpeaker;

    [Header("Object Configuration")]
    [Tooltip("Parent GameObject to attach spawned prefabs.")]
    public GameObject _SpawnableHost;
    [Tooltip("Object providing the spawn location and controller behaviour.")]
    public GameObject _ControllerObject;
    [Tooltip("Currently selected prefab to spawn.")]
    public GameObject _PrefabToSpawn;
    public List<GameObject> _SpawnablePrefabs;
    public bool _SelectRandomPrefab = false;
    public List<GameObject> _ActiveObjects = new List<GameObject>();

    [Header("Spawn Configuration")]
    [Range(0, 100)] public int _SpawnablesAllocated = 1;
    public bool _AutoSpawn = true;
    public bool _AutoRemove = true;
    [Range(0.01f, 2f)] public float _SpawnPeriodSeconds = 0.1f;
    [Tooltip("Duration in seconds before destroying spawned object (0 = do not destroy based on duration).")]
    [Range(0, 60)] public float _ObjectLifespan = 0;
    [Tooltip("Subtract a random amount from a spawned object's lifespan by this fraction of its max duration.")]
    [Range(0, 1)] public float _LifespanVariance = 0;
    [Range(0f, 3f)] public float _SpawnRadius = 0.5f;
    [Tooltip("Destroy object outside this radius from its spawning position.")]
    public float _DestroyRadius = 20f;
    public bool _AllowSiblingSurfaceContact = true;
    public SiblingCollision _AllowSiblingCollisionBurst = SiblingCollision.Single;
    
    [Header("Spawn Randomisation")]
    public Vector3 _EjectionDirection = new Vector3(0,-1,0);
    [Range(0, 100)] public float _RandomEjectionAngle = 0;
    [Range(0, 100)] public float _RandomEjectionSpeed = 0;

    [Header("Runtime Dynamics")]
    [SerializeField] protected int _ObjectCounter = 0;
    [SerializeField] protected float _SecondsSinceSpawn = 0;
    [SerializeField] public HashSet<GameObject> _CollidedThisUpdate;
    

    void Start()
    {
        if (_SpawnableHost == null)
            _SpawnableHost = gameObject;

        if (_SpawnablePrefabs.Count == 0 && _PrefabToSpawn != null)
            _SpawnablePrefabs.Add(_PrefabToSpawn);

        if (_SpawnablePrefabs.Count > 1 && _PrefabToSpawn == null)
            _PrefabToSpawn = _SpawnablePrefabs[0];

        if (_SpawnablePrefabs.Count == 0)
            Debug.LogWarning($"{name} not assigned any prefabs!");

        _CollidedThisUpdate = new HashSet<GameObject>();
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
                SpawnNewObject();
            if (_ActiveObjects.Count > _SpawnablesAllocated && _AutoRemove)
                RemoveOverSpawnTarget();
        }

        _ActiveObjects.RemoveAll(item => item == null);
    }

    public bool ReadyToSpawn()
    {
        if (_ControllerObject == null || _PrefabToSpawn == null)
            return false;
        if (_ActiveObjects.Count == _SpawnablesAllocated || _SecondsSinceSpawn < _SpawnPeriodSeconds)
            return false;
        return true;
    }

    public void SpawnNewObject()
    {


        GameObject objectToSpawn;

        if (_SelectRandomPrefab)
            objectToSpawn = _SpawnablePrefabs[Mathf.RoundToInt(Random.Range(0, _SpawnablePrefabs.Count))];
        else objectToSpawn = _PrefabToSpawn;

        GameObject newObject = Instantiate(objectToSpawn,
            _ControllerObject.transform.position,
            Quaternion.identity, _SpawnableHost.transform);




        if (!newObject.TryGetComponent(out Rigidbody rb))
            rb = newObject.AddComponent<Rigidbody>();

        float speed = Random.Range(-_RandomEjectionSpeed, _RandomEjectionSpeed);
        float angle = Random.Range(-_RandomEjectionAngle, _RandomEjectionAngle);
        Quaternion direction = Quaternion.Euler(0, 0, angle) * Quaternion.Euler(Vector3.Normalize(_EjectionDirection));

        //Vector3 randomVel = new Vector3(Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f);
        //Vector3 randomAng = new Vector3(Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f);
        //rb.velocity = randomVel * _RandomVelocity;
        //// rb.AddForce(randomVel * _RandomVelocity);
        //rb.angularVelocity = randomAng * _RandomAngularVelocity;

        _ObjectCounter++;
        newObject.name = newObject.name + " (" + _ObjectCounter + ")";
        newObject.transform.localPosition = _ControllerObject.transform.localPosition;

        if (!newObject.TryGetComponent(out SpawnableManager spawnableManager))
            spawnableManager = newObject.AddComponent<SpawnableManager>();
        if (_ObjectLifespan != 0)
            spawnableManager._Lifespan = _ObjectLifespan - _ObjectLifespan * Random.Range(0, _LifespanVariance);
        spawnableManager._DestroyRadius = _DestroyRadius;
        spawnableManager._SpawnedObject = newObject;
        spawnableManager._ObjectSpawner = this;

        if (newObject.TryGetComponent(out BehaviourClass behaviour))
        {
            behaviour._SpawnedObject = newObject;
            behaviour._ControllerObject = _ControllerObject;
            behaviour._ObjectSpawner = this;
        }

        // Set emitter properties if spawned GameObject is an emitter host
        HostAuthoring newHost = newObject.GetComponentInChildren(typeof(HostAuthoring), true) as HostAuthoring;
        if (newHost != null)
        {
            newHost._Spawner = this;
            newHost._LocalObject = newObject;
            newHost._RemoteObject = _ControllerObject;
            newHost.AddBehaviourInputSource(spawnableManager);
        }
        newObject.SetActive(true);

        _ActiveObjects.Add(newObject);
        _SecondsSinceSpawn = 0;
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
