﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractionBase : MonoBehaviour
{
    public GameObject _SourceObject;
    public Rigidbody _RigidBody;

    public float _InputMin = 0f;
    public float _InputMax = 1f;
    public float _OutputValue = 0;

    protected float _PreviousValue = 0;
    protected bool _HoldTempValue = false;
    protected bool _Colliding = false;
    protected PhysicMaterial _CollidedMaterial;

    void Start()
    {
        if (_SourceObject == null)
            _SourceObject = gameObject;

        if (_SourceObject.GetComponent<Rigidbody>() == null)
            _SourceObject = this.transform.parent.gameObject;

        _RigidBody = _SourceObject.GetComponent<Rigidbody>();

        AdditionalStartFunction();
    }

    public virtual void AdditionalStartFunction() { }

    public virtual void UpdateAttachedEmitterInteractionSource(GameObject gameObject, Collision collision) { }

    public float GetValue()
    {
        return _OutputValue;
    }

    public void UpdateSmoothedOutputValue(float inputValue, float smoothing)
    {
        float newValue = Map(inputValue, _InputMin, _InputMax, 0, 1);

        float actualSmoothing = (1 - smoothing) * 10f;
        _OutputValue = Mathf.Lerp(_OutputValue, newValue, actualSmoothing * Time.deltaTime);

        if (_OutputValue < 0.001f)
            _OutputValue = 0;

        _OutputValue = Mathf.Clamp(_OutputValue, 0f, 1f);
    }

    public void SetColliding(bool collide, PhysicMaterial material)
    {
        _Colliding = collide;
        _CollidedMaterial = material;
    }

    public virtual void SetCollisionData(Collision collision) { }

    public static float Map(float val, float inMin, float inMax, float outMin, float outMax)
    {
        return outMin + ((outMax - outMin) / (inMax - inMin)) * (val - inMin);
    }
}

public class BlankInteraction : InteractionBase
{
}
