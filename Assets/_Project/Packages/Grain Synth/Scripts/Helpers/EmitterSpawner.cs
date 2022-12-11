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
    public List<GameObject> _SliderPrefabs;
    public GameObject _SelectedPrefab;
    public bool _RandomPrefab = false;
    [Range(1, 200)]
    public int _TargetNumber = 1;
    [Range(0f, 2f)]
    public float _SpawnFrequency = 1f;
    public float _NextSpawnCountdown = 0;
    [Range(0f, 3f)]
    public float _SpawnRadius = 0.5f;
    public List<GameObject> _Sliders = new List<GameObject>();
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

        if (_SliderPrefabs.Count == 0 && _SelectedPrefab != null)
            _SliderPrefabs.Add(_SelectedPrefab);

        if (_SliderPrefabs.Count > 1 && _SelectedPrefab == null)
            _SelectedPrefab = _SliderPrefabs[0];

        if (_SliderPrefabs.Count == 0)
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
        if (_Sliders.Count < _TargetNumber && _NextSpawnCountdown <= 0)
         {
            GameObject objectToSpawn;

            if (_RandomPrefab)
                objectToSpawn = _SliderPrefabs[Mathf.RoundToInt(Random.Range(0, _SliderPrefabs.Count))];
            else objectToSpawn = _SelectedPrefab;

            if (objectToSpawn.GetComponent<EmitterActionManager>() != null)
            {
                GameObject newObject = Instantiate(objectToSpawn, gameObject.transform);
                EmitterActionManager newObjectActionManager = newObject.GetComponent<EmitterActionManager>();
                newObjectActionManager.ResetEmitterInteractions(newObject, _ThisGameObject, null);
                newObjectActionManager.UpdateEmitterSpeaker(_EmittersUseSharedSpeaker ? _SharedSpeaker : null);
                newObject.name = newObject.name + " (" + (_Sliders.Count) + ")";

                _Sliders.Add(newObject);
                _NextSpawnCountdown = _SpawnFrequency;
            }
        }
    }

    private void RemoveSliders()
    {
        if (_Sliders.Count > _TargetNumber && _NextSpawnCountdown <= _SpawnFrequency)
        {
            Destroy(_Sliders[_Sliders.Count - 1]);
            _Sliders.RemoveAt(_Sliders.Count - 1);
            _NextSpawnCountdown = _SpawnFrequency;
        }
    }
}
