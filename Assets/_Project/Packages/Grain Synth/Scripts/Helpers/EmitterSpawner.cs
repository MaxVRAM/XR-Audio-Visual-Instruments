using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using static UnityEditor.PlayerSettings;
using Random = UnityEngine.Random;

/// <summary>
//     Dynamically spawn/destroy emitter objects as children of this object.
/// <summary>
public class EmitterSpawner : MonoBehaviour
{
    public GrainSpeakerAuthoring _SharedSpeaker;
    public bool _EmittersUseSharedSpeaker = true;
    public List<GameObject> _EmitterPrefabs;
    public GameObject _SelectedPrefab;
    public bool _RandomPrefab = false;
    [Range(1, 200)]
    public int _TargetNumber = 1;
    [Range(0f, 2f)]
    public float _SpawnFrequency = 1f;
    public float _NextSpawnCountdown = 0;
    [Range(0f, 3f)]
    public float _SpawnRadius = 0.5f;
    public List<GameObject> _EmitterObjects = new List<GameObject>();
    protected GameObject _ThisGameObject;
    protected string _Name;

    void Start()
    {
        _ThisGameObject = gameObject;
        _Name = transform.parent.name + " > " + this.name;

        if (_SharedSpeaker == null)
        {
            _SharedSpeaker = GetComponent<GrainSpeakerAuthoring>();

            if (_SharedSpeaker == null)
                _SharedSpeaker = GetComponentInChildren<GrainSpeakerAuthoring>();
        }

        if (_EmittersUseSharedSpeaker && _SharedSpeaker == null)
            Debug.LogWarning("Emitter Spawner [" + _Name + "] has no speaker to provide spawned objects!");

        if (_EmitterPrefabs.Count == 0 && _SelectedPrefab != null)
            _EmitterPrefabs.Add(_SelectedPrefab);

        if (_EmitterPrefabs.Count > 1 && _SelectedPrefab == null)
            _SelectedPrefab = _EmitterPrefabs[0];

        if (_EmitterPrefabs.Count == 0)
            Debug.LogWarning("Emitter Spawner [" + _Name + "] not assigned any prefabs!");
    }

    void Update()
    {
        _NextSpawnCountdown -= Time.deltaTime;

        if (_SelectedPrefab != null && _SharedSpeaker != null)
        {
            SpawnSliders();
            RemoveSliders();
        }
    }

    private void SpawnSliders()
    {
        if (_EmitterObjects.Count < _TargetNumber && _NextSpawnCountdown <= 0)
         {
            GameObject objectToSpawn;

            if (_RandomPrefab)
                objectToSpawn = _EmitterPrefabs[Mathf.RoundToInt(Random.Range(0, _EmitterPrefabs.Count))];
            else objectToSpawn = _SelectedPrefab;

            GameObject newObject = Instantiate(objectToSpawn, gameObject.transform);
            newObject.name = newObject.name + " (" + (_EmitterObjects.Count) + ")";

            EmitterActionManager actionManager = newObject.GetComponent<EmitterActionManager>();
            if (actionManager != null)
                actionManager._Speaker = _SharedSpeaker;

            BaseEmitterClass[] emitters = newObject.GetComponentsInChildren<BaseEmitterClass>();
            if (_EmittersUseSharedSpeaker)
                foreach (BaseEmitterClass emitter in emitters)
                    if (!emitter._ContactEmitter)
                        emitter.GetComponent<BaseEmitterClass>().SetupAttachedEmitter(newObject, _SharedSpeaker);

            _EmitterObjects.Add(newObject);
            _NextSpawnCountdown = _SpawnFrequency;
            Debug.Log("Created new slider " + newObject.name);
        }
    }

    private void RemoveSliders()
    {
        if (_EmitterObjects.Count > _TargetNumber && _NextSpawnCountdown <= _SpawnFrequency)
        {
            Destroy(_EmitterObjects[_EmitterObjects.Count - 1]);
            _EmitterObjects.RemoveAt(_EmitterObjects.Count - 1);
            _NextSpawnCountdown = _SpawnFrequency;
            Debug.Log("Removing slider " + name);
        }
    }
}
