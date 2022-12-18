using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
//     Dynamically spawn/destroy child objects.
/// <summary>
public class ObjectSpawner : MonoBehaviour
{
    // TODO: Dedicated speaker assignment not implemented
    [Header("Speaker Assignment")]
    [Tooltip("Force spawned emitters to use this speaker. Leave null to use dynamic allocation.")]
    public SpeakerAuthoring _DedicatedSpeaker;
    [Header("Object Configuration")]
    [Tooltip("Object providing the spawn location and controller behaviour.")]
    public GameObject _ControllerObject;
    [Tooltip("Currently selected prefab to spawn.")]
    public GameObject _PrefabToSpawn;
    public List<GameObject> _SpawnablePrefabs;
    public bool _SelectRandomPrefab = false;
    public List<GameObject> _ActiveObjects = new List<GameObject>();
    [Header("Spawn Configuration")]
    [Range(1, 200)]
    public int _MaxSpawnables = 1;
    public bool _AutoSpawn = true;
    public bool _AutoRemove = true;
    [Range(0f, 2f)]
    public float _SpawnFrequency = 1f;
    [SerializeField]
    protected float _TimeSinceSpawn = 0;
    [SerializeField]
    protected int _MaxSpawnsPerFrame;
    [Header("Object Behaviour")]
    public BehaviourClass _BehaviourPrefab;
    [Range(0f, 3f)]
    public float _SpawnRadius = 0.5f;
    protected string _Name;

    void Start()
    {
        _Name = "Spawner | " + name;

        if (_SpawnablePrefabs.Count == 0 && _PrefabToSpawn != null)
            _SpawnablePrefabs.Add(_PrefabToSpawn);

        if (_SpawnablePrefabs.Count > 1 && _PrefabToSpawn == null)
            _PrefabToSpawn = _SpawnablePrefabs[0];

        if (_SpawnablePrefabs.Count == 0)
            Debug.LogWarning("Spawner [" + _Name + "] not assigned any prefabs!");
    }

    void Update()
    {
        _TimeSinceSpawn += Time.deltaTime;

        if (_ControllerObject != null && _PrefabToSpawn != null && _ActiveObjects.Count != _MaxSpawnables && _TimeSinceSpawn > _SpawnFrequency)
        {
            _MaxSpawnsPerFrame = GetSpawnNumber(_SpawnFrequency, ref _TimeSinceSpawn);
            if (_ActiveObjects.Count < _MaxSpawnables && _AutoSpawn)
                SpawnObject(_MaxSpawnsPerFrame);
            if (_ActiveObjects.Count > _MaxSpawnables && _AutoRemove)
                RemoveObject(_MaxSpawnsPerFrame);
        }
    }

    public void SpawnObject(int maxToSpawn)
    {
        while (_ActiveObjects.Count < _MaxSpawnables && maxToSpawn > 0)
        {
            GameObject objectToSpawn;

            if (_SelectRandomPrefab)
                objectToSpawn = _SpawnablePrefabs[Mathf.RoundToInt(Random.Range(0, _SpawnablePrefabs.Count))];
            else objectToSpawn = _PrefabToSpawn;

            GameObject newObject = Instantiate(objectToSpawn,
                _ControllerObject.transform.position,
                Quaternion.identity, gameObject.transform);
            newObject.name = newObject.name + " (" + _ActiveObjects.Count + ")";
            newObject.transform.localPosition = _ControllerObject.transform.localPosition;

            // Set emitter properties if spawned GameObject is an emitter host
            HostAuthoring newHost = newObject.GetComponentInChildren(typeof(HostAuthoring), true) as HostAuthoring;
            if (newHost != null)
            {
                newHost._LocalObject = newObject;
                newHost._RemoteObject = _ControllerObject;
            }

            _ActiveObjects.Add(newObject);
            _TimeSinceSpawn = 0;
            maxToSpawn --;
        }

        // Zero time since value if 
        //if (_ActiveObjects.Count >= _MaxSpawnables) _TimeSinceSpawn = 0;
    }

    public void RemoveObject(int maxToSpawn)
    {
        while (_ActiveObjects.Count > _MaxSpawnables && maxToSpawn > 0)
        {
            Destroy(_ActiveObjects[_ActiveObjects.Count - 1]);
            _ActiveObjects.RemoveAt(_ActiveObjects.Count - 1);
            maxToSpawn --;
        }
    }

    public int GetSpawnNumber(float frequency, ref float spawnClock)
    {
        int maxSpawnPerFrame = Mathf.CeilToInt(Time.deltaTime / frequency);
        float numberToSpawn = spawnClock / frequency;
        spawnClock = Time.deltaTime / (numberToSpawn % 1);
        return Mathf.Min(maxSpawnPerFrame, Mathf.CeilToInt(numberToSpawn));
    }
}
