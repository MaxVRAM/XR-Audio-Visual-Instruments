using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroyTimer : BehaviourClass
{
    public float _Lifespan = -1;
    [SerializeField]
    protected float _SpawnTime;
    [SerializeField]
    protected int _SpawnTimeIndex;
    [SerializeField]
    protected float _Age = -1;
    public float CurrentAge { get => _Age; }
    public float CurrentAgeNorm { get => Mathf.Clamp(_Age / _Lifespan, 0, 1); }

    void Start()
    {
        _SpawnTime = Time.time;
        _SpawnTimeIndex = GrainSynth.Instance._CurrentDSPSample;
        if (_Lifespan == -1)
            _Lifespan = float.MaxValue;
    }

    void Update()
    {
        _Age = Time.time - _SpawnTime;
        if (_Age >= _Lifespan)
            Destroy(gameObject);
    }

    public int GetFadeoutStartIndex(float normFadeStart)
    {
        if (_Lifespan == -1)
            return -1;
        return _SpawnTimeIndex + (int)(_Lifespan * GrainSynth.Instance._SampleRate * normFadeStart);
    }

    public int GetFadeoutEndIndex()
    {
        if (_Lifespan == -1)
            return -1;
        return _SpawnTimeIndex + (int)(_Lifespan * GrainSynth.Instance._SampleRate);
    }
}
