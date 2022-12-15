using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
//     Dynamically spawn/destroy child objects.
/// <summary>
public class ObjectSpawner : MonoBehaviour
{
    public GameObject _TetherObject;
    public bool _EmittersUseSharedSpeaker = true;
    public SpeakerAuthoring _SharedSpeaker;
    public bool _RandomPrefab = false;
    [Range(1, 200)]
    public int _TargetNumber = 1;
    [Range(0f, 2f)]
    public float _SpawnFrequency = 1f;
    public float _NextSpawnCountdown = 0;
    [Range(0f, 3f)]
    public float _SpawnRadius = 0.5f;
    public GameObject _SelectedPrefab;
    public List<GameObject> _SpawnablePrefabs;
    public List<GameObject> _SpawnedObjects = new List<GameObject>();
    protected string _Name;

    void Start()
    {
        _Name = transform.parent.name + " > " + this.name;

        if (_SharedSpeaker == null)
            _SharedSpeaker = GetComponent<SpeakerAuthoring>();
            if (_SharedSpeaker == null)
                _SharedSpeaker = GetComponentInChildren<SpeakerAuthoring>();

        if (_EmittersUseSharedSpeaker && _SharedSpeaker == null)
            Debug.LogWarning("Emitter Spawner [" + _Name + "] has no speaker to provide spawned objects!");

        if (_SpawnablePrefabs.Count == 0 && _SelectedPrefab != null)
            _SpawnablePrefabs.Add(_SelectedPrefab);

        if (_SpawnablePrefabs.Count > 1 && _SelectedPrefab == null)
            _SelectedPrefab = _SpawnablePrefabs[0];

        if (_SpawnablePrefabs.Count == 0)
            Debug.LogWarning("Emitter Spawner [" + _Name + "] not assigned any prefabs!");
    }

    void Update()
    {
        _NextSpawnCountdown -= Time.deltaTime;

        if (_TetherObject != null && _SelectedPrefab != null)
        {
            SpawnObject();
            RemoveObject();
        }
    }

    private void SpawnObject()
    {
        if (_SpawnedObjects.Count < _TargetNumber && _NextSpawnCountdown <= 0)
         {
            GameObject objectToSpawn;

            if (_RandomPrefab)
                objectToSpawn = _SpawnablePrefabs[Mathf.RoundToInt(Random.Range(0, _SpawnablePrefabs.Count))];
            else objectToSpawn = _SelectedPrefab;

            if (objectToSpawn.GetComponent<HostAuthoring>() != null)
            {
                GameObject newObject = Instantiate(objectToSpawn,
                    _TetherObject.transform.position, Quaternion.identity, gameObject.transform);
                newObject.name = newObject.name + " (" + _SpawnedObjects.Count + ")";

                if (objectToSpawn.TryGetComponent(out HostAuthoring host))
                {
                    host.SetLocalObject(newObject);
                    host.SetRemoteObject(_TetherObject);
                }
                _SpawnedObjects.Add(newObject);
                _NextSpawnCountdown = _SpawnFrequency;
            }
        }
    }

    private void RemoveObject()
    {
        if (_SpawnedObjects.Count > _TargetNumber && _NextSpawnCountdown <= _SpawnFrequency)
        {
            Destroy(_SpawnedObjects[_SpawnedObjects.Count - 1]);
            _SpawnedObjects.RemoveAt(_SpawnedObjects.Count - 1);
            _NextSpawnCountdown = _SpawnFrequency;
        }
    }
}
