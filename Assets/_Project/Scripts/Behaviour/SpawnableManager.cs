using System.Collections;
using System.Collections.Generic;
using Unity.XR.Oculus;
using UnityEngine;

public class SpawnableManager : BehaviourClass
{
    private int _SampleRate;
    public float _Lifespan = int.MaxValue;
    [SerializeField] protected float _SpawnTime;
    [SerializeField] protected float _Age = -1;
    [SerializeField] protected Vector3 _SpawnPosition;
    public float _DestroyRadius = 20;
    public float CurrentAge { get => _Age; }
    public float CurrentAgeNorm { get => Mathf.Clamp(_Age / _Lifespan, 0, 1); }

    void Start()
    {
        _SampleRate = AudioSettings.outputSampleRate;
        _SpawnTime = Time.time;
        _SpawnPosition = transform.position;
    }

    void Update()
    {
        _Age = Time.time - _SpawnTime;
        if (_Age >= _Lifespan || Mathf.Abs((transform.position - _SpawnPosition).magnitude) > _DestroyRadius)
            Destroy(gameObject);
    }

    public int SamplesUntilFade(float normFadeStart)
    {
        if (_Lifespan == int.MaxValue)
            return int.MaxValue;
        else
            return (int)((_Lifespan * normFadeStart - _Age) * _SampleRate);
    }

    public int SamplesUntilDeath()
    {
        if (_Lifespan == int.MaxValue)
            return int.MaxValue;
        else
            return (int)((_Lifespan - _Age) * _SampleRate);
    }
}
