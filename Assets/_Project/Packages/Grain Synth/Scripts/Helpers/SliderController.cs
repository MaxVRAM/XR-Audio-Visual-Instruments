using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.PlayerSettings;
using Random = UnityEngine.Random;


public class SliderController : MonoBehaviour
{
    [Header("Slider Spawn")]
    public GameObject _SliderPrefab;
    [Range(1, 200)]
    public int _SpawnAmount = 1;
    [Range(0f, 3f)]
    public float _SpawnRadius = 0.5f;
    public int _MaxSpawnPerFrame = 1;
    public int _MaxRemovePerFrame = -1;
    protected List<GameObject> _Sliders = new List<GameObject>();

    void Start()
    {
    }

    void Update()
    {
        SpawnSliders();
        RemoveSliders();
    }

    private void SpawnSliders()
    {
        int iteration = 0;
        while (_Sliders.Count < _SpawnAmount && iteration < _MaxSpawnPerFrame)
        {
            GameObject newSlider = Instantiate(
                _SliderPrefab,
                Random.insideUnitCircle * _SpawnRadius,
                Random.rotation,
                transform);
            newSlider.name = "[" + this.name + "] - Slider (" + (_Sliders.Count) + ")";
            _Sliders.Add(newSlider);

            if (_MaxSpawnPerFrame > 0)
                iteration++;
        }
    }

    private void RemoveSliders()
    {
        int iteration = 0;
        while (_Sliders.Count > _SpawnAmount && iteration < _MaxSpawnPerFrame)
        {
            Destroy(_Sliders[_Sliders.Count - 1]);
            _Sliders.RemoveAt(_Sliders.Count - 1);

            if (_MaxRemovePerFrame > 0)
                iteration++;
        }
    }
}
