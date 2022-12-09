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
    [Range(0f, 20f)]
    public float _SpawnFrequency = 5f;
    protected float _PreviousSpawnTime;
    [Range(0f, 3f)]
    public float _SpawnRadius = 0.5f;
    public int _MaxSpawnPerFrame = 1;
    public int _MaxRemovePerFrame = -1;
    protected List<GameObject> _EmitterObjects = new List<GameObject>();
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

        if (_EmitterPrefabs.Count == 0)
            Debug.LogWarning("Emitter Spawner [" + _Name + "] not assigned any prefabs!");
    }

    void Update()
    {
        if (_EmitterPrefabs.Count == 0 || (_EmittersUseSharedSpeaker && _SharedSpeaker == null))
            return;

        if (_SelectedPrefab == null)
            _SelectedPrefab = _EmitterPrefabs[0];
        SpawnSliders();
        RemoveSliders();
    }

    private void SpawnSliders()
    {
        if (_EmitterObjects.Count < _TargetNumber && Time.time > _PreviousSpawnTime + _SpawnFrequency / 1000)
         {
            GameObject go;
            if (_RandomPrefab)
                go = _EmitterPrefabs[Mathf.RoundToInt(Random.Range(0, _EmitterPrefabs.Count))];
            else go = _SelectedPrefab;

            GameObject newSlider = Instantiate(go, Random.insideUnitCircle * _SpawnRadius, Random.rotation, transform);
            newSlider.name = newSlider.name + " (" + (_EmitterObjects.Count) + ")";
            _EmitterObjects.Add(newSlider);
            _PreviousSpawnTime = Time.time;
        }
    }

    private void RemoveSliders()
    {
        if (_EmitterObjects.Count > _TargetNumber && Time.time > _PreviousSpawnTime + _SpawnFrequency / 1000)
        {
            Destroy(_EmitterObjects[_EmitterObjects.Count - 1]);
            _EmitterObjects.RemoveAt(_EmitterObjects.Count - 1);
            _PreviousSpawnTime = Time.time;
        }
    }
}
