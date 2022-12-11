using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
//     Dynamically spawn/destroy emitter objects as children of this object.
/// <summary>
public class EmitterSpawner : MonoBehaviour
{
    public GameObject _TetherObject;
    public bool _EmittersUseSharedSpeaker = true;
    public GrainSpeakerAuthoring _SharedSpeaker;
    public GameObject _SelectedPrefab;
    public List<GameObject> _SliderPrefabs;
    public bool _RandomPrefab = false;
    [Range(1, 200)]
    public int _TargetNumber = 1;
    [Range(0f, 2f)]
    public float _SpawnFrequency = 1f;
    public float _NextSpawnCountdown = 0;
    [Range(0f, 3f)]
    public float _SpawnRadius = 0.5f;
    public List<GameObject> _Sliders = new List<GameObject>();
    protected string _Name;

    void Start()
    {
        _Name = transform.parent.name + " > " + this.name;

        if (_SharedSpeaker == null)
            _SharedSpeaker = GetComponent<GrainSpeakerAuthoring>();
            if (_SharedSpeaker == null)
                _SharedSpeaker = GetComponentInChildren<GrainSpeakerAuthoring>();

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

        if (_TetherObject != null && _SelectedPrefab != null)
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
                GameObject newObject = Instantiate(objectToSpawn, _TetherObject.transform.position,
                    Quaternion.identity, gameObject.transform);
                EmitterActionManager newObjectActionManager = newObject.GetComponent<EmitterActionManager>();
                newObjectActionManager.ResetEmitterInteractions(newObject, _TetherObject, null);
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
