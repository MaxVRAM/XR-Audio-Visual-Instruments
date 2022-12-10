using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractionBase : MonoBehaviour
{
    public GameObject _PrimaryObject;
    public Rigidbody _PrimaryRigidBody;
    protected GameObject _SecondaryObject;
    public Rigidbody _SecondaryRigidBody;

    [SerializeField]
    public float _InputMin = 0f;
    public float _InputMax = 0f;
    public float _OutputValue = 0;
    protected float _PreviousValue = 0;
    protected bool _HoldTempValue = false;
    protected bool _Colliding = false;
    protected PhysicMaterial _CollidedMaterial;

    void Start()
    {
        if (_PrimaryObject == null)
            _PrimaryObject = gameObject;

        if (_PrimaryObject.GetComponent<Rigidbody>() == null)
            _PrimaryObject = this.transform.parent.gameObject;

        if (_PrimaryObject != null)
            _PrimaryRigidBody = _PrimaryObject.GetComponent<Rigidbody>();
        
        if (_SecondaryObject != null)
            _SecondaryRigidBody = _SecondaryObject.GetComponent<Rigidbody>();
    }

    public virtual void UpdateInteractionSource(GameObject primaryObject) { }
    public virtual void UpdateInteractionSource(GameObject primaryObject, GameObject secondaryObject) { }
    public virtual void UpdateInteractionSource(GameObject primaryObject, Collision collision) { }

    public float GetValue()
    {
        return _OutputValue;
    }

    public void UpdateSmoothedOutputValue(float value, float smoothing)
    {
        float newValue = Map(value, _InputMin, _InputMax, 0, 1);

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
