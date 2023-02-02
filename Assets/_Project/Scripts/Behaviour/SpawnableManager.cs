using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnableManager : BehaviourClass
{
    public float _Lifespan = -1;
    [SerializeField] protected float _SpawnTime;
    [SerializeField] protected float _Age = -1;
    [SerializeField] protected Vector3 _SpawnPosition;
    public float _DestroyRadius = 20;
    public float CurrentAge { get => _Age; }
    public float CurrentAgeNorm { get => Mathf.Clamp(_Age / _Lifespan, 0, 1); }

    void Start()
    {
        _SpawnTime = Time.time;
        if (_Lifespan < 0)
            _Lifespan = float.MaxValue;
        _SpawnPosition = transform.position;
    }

    void Update()
    {
        _Age = Time.time - _SpawnTime;
        if (_Age >= _Lifespan || Mathf.Abs((transform.position - _SpawnPosition).magnitude) > _DestroyRadius)
            Destroy(gameObject);
    }

    public float GetFadeoutStartTime(float normFadeStart)
    {
        if (_Lifespan < 0 || _Lifespan == float.MaxValue)
            return -1;
        return _SpawnTime + (int)(_Lifespan * normFadeStart);
    }

    public float GetFadeoutEndTime()
    {
        if (_Lifespan < 0 || _Lifespan == float.MaxValue)
            return -1;
        return _SpawnTime + (int)(_Lifespan);
    }
}
