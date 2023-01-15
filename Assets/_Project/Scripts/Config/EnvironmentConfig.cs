using System.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

public class EnvironmentConfig : MonoBehaviour
{
    public bool _GravityToggle = true;
    public float _GravityMultiplier = 9.8f;
    public Vector3 _GravityUnitVector = new Vector3(0, -1, 0);

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    private void FixedUpdate()
    {
        UpdateGravity();
    }

    private void UpdateGravity()
    {
        Physics.gravity = _GravityToggle ? _GravityUnitVector * _GravityMultiplier : Vector3.zero;
    }
}