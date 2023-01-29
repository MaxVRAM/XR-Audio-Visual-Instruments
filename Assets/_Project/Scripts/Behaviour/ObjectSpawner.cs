using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;


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
    public SiblingCollision _AllowSiblingCollisionBurst = SiblingCollision.Single;
    public bool _AllowSiblingSurfaceContact = true;
    public bool _AutoSpawn = true;
    public bool _AutoRemove = true;
    [Range(1, 200)]
    public int _MaxSpawnables = 1;
    [Range(0.01f, 2f)]
    public float _SpawnFrequency = 1f;
    [Tooltip("Duration in seconds before destroying spawned object (0 = do not destroy based on duration).")]
    [Range(0, 60)]
    public float _ObjectLifespan = 0;
    [Tooltip("Subtract a random amount from a spawned object's lifespan by this fraction of its max duration.")]
    [Range(0, 1)]
    public float _LifespanVariance = 0;
    [Range(0f, 3f)]
    public float _SpawnRadius = 0.5f;
    [Tooltip("Destroy object outside this radius from its spawning position.")]
    public float _DestroyRadius = 20f;
    
    [Header("Spawn Randomisation")]
    [Range(0, 100)]
    public float _RandomVelocity = 0;
    [Range(0, 100)]
    public float _RandomAngularVelocity = 0;

    //[Header("Spawnable Behaviour")]
    //public GameObject _BehaviourPrefab;
    protected string _Name;

    [Header("Runtime Dynamics")]
    [SerializeField]
    protected int _ObjectCounter = 0;
    [SerializeField]
    protected float _TimeSinceSpawn = 0;
    [SerializeField]
    protected int _MaxSpawnsPerFrame;
    [SerializeField]
    public HashSet<GameObject> _CollidedThisUpdate;
    

    void Start()
    {
        _Name = "Spawner | " + name;

        if (_SpawnableHost == null)
            _SpawnableHost = gameObject;

        if (_SpawnablePrefabs.Count == 0 && _PrefabToSpawn != null)
            _SpawnablePrefabs.Add(_PrefabToSpawn);

        if (_SpawnablePrefabs.Count > 1 && _PrefabToSpawn == null)
            _PrefabToSpawn = _SpawnablePrefabs[0];

        if (_SpawnablePrefabs.Count == 0)
            Debug.LogWarning("Spawner [" + _Name + "] not assigned any prefabs!");

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
        if (_TimeSinceSpawn == float.NaN)
            _TimeSinceSpawn = 0;
        _TimeSinceSpawn += Time.deltaTime;

        if (_ControllerObject != null && _PrefabToSpawn != null && _ActiveObjects.Count != _MaxSpawnables && _TimeSinceSpawn > _SpawnFrequency)
        {
            _MaxSpawnsPerFrame = GetSpawnNumber(_SpawnFrequency, ref _TimeSinceSpawn);
            if (_ActiveObjects.Count < _MaxSpawnables && _AutoSpawn)
                SpawnNewObject(_MaxSpawnsPerFrame);
            if (_ActiveObjects.Count > _MaxSpawnables && _AutoRemove)
                RemoveOverSpawnTarget(_MaxSpawnsPerFrame);
        }

        _ActiveObjects.RemoveAll(item => item == null);
    }

    public void SpawnNewObject(int maxToSpawn)
    {
        while (_ActiveObjects.Count < _MaxSpawnables && maxToSpawn > 0)
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

            Vector3 randomVel = new Vector3(Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f);
            Vector3 randomAng = new Vector3(Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f);
            rb.velocity = randomVel * _RandomVelocity;
            // rb.AddForce(randomVel * _RandomVelocity);
            rb.angularVelocity = randomAng * _RandomAngularVelocity;

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


            //if (_BehaviourPrefab != null && _BehaviourPrefab.TryGetComponent(out BehaviourClass behaviour))
            //{
            //    BehaviourClass newBehaviour = Instantiate(_BehaviourPrefab, newObject.transform).GetComponent<BehaviourClass>();
            //    newBehaviour._SpawnedObject = newObject;
            //    newBehaviour._ControllerObject = _ControllerObject;
            //    newBehaviour._ObjectSpawner = this;
            //    newBehaviour.UpdateBehaviour(behaviour);
            //}

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
            _TimeSinceSpawn = 0;
            maxToSpawn --;
        }
    }

    public void RemoveOverSpawnTarget(int maxToSpawn)
    {
        while (_ActiveObjects.Count > _MaxSpawnables && maxToSpawn > 0)
        {
            if (_ActiveObjects[0] != null)
            {
                Destroy(_ActiveObjects[0]);
                maxToSpawn --;
            }
        }
    }

    public int GetSpawnNumber(float frequency, ref float spawnClock)
    {
        int maxSpawnPerFrame = Mathf.CeilToInt(Time.deltaTime / frequency);
        float numberToSpawn = spawnClock / frequency;
        spawnClock = Time.deltaTime / (numberToSpawn % 1);
        return Mathf.Min(maxSpawnPerFrame, Mathf.CeilToInt(numberToSpawn));
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
