using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroyTimer : MonoBehaviour
{
    [SerializeField]
    protected float _SpawnTime;
    public float _Duration = -1;

    void Start()
    {
        _SpawnTime = Time.time;
        if (_Duration == -1)
            _Duration = float.MaxValue;
    }

    void Update()
    {
        if (Time.time >= _SpawnTime + _Duration)
            Destroy(gameObject);
    }
}
