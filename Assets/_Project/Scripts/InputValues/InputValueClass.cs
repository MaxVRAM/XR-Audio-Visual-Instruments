using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class InputValueClass : MonoBehaviour
{
    // TODO: Turn the source object properties into a class for better management
    public GameObject _PrimaryObject;
    public Rigidbody _PrimaryRigidBody;
    public GameObject _SecondaryObject;
    public Rigidbody _SecondaryRigidBody;
    public Collision _Collision;

    [SerializeField]
    public float _InputMin = 0f;
    public float _InputMax = 1f;
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

        UpdateInteractionSources(_PrimaryObject, _SecondaryObject, null);
    }

    public void UpdateInteractionSources(GameObject primaryObject, GameObject secondaryObject, Collision collision)
    {
        if (primaryObject != null)
        {
            _PrimaryObject = primaryObject;
            _PrimaryRigidBody = _PrimaryObject.GetComponent<Rigidbody>();
        }
        if (secondaryObject != null)
        {
            _SecondaryObject = secondaryObject;
            _SecondaryRigidBody = _PrimaryObject.GetComponent<Rigidbody>();
        }
        if (collision != null)
        {
            _SecondaryObject = collision.collider.gameObject;
            _SecondaryRigidBody = _SecondaryObject.GetComponent<Rigidbody>();
            _Colliding = true;
            _Collision = collision;
            _CollidedMaterial = collision.collider.material;
            ProcessCollision(collision);
        }
        else
        {
            _Colliding = false;
            _Collision = null;
            _CollidedMaterial = null;
        }
    }


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

    public void UpdateCollideStatus(bool colliding, PhysicMaterial material)
    {
        _Colliding = colliding;
        _CollidedMaterial = material;
    }

    public virtual void ProcessCollision(Collision collision) { }

    public static float Map(float val, float inMin, float inMax, float outMin, float outMax)
    {
        return outMin + ((outMax - outMin) / (inMax - inMin)) * (val - inMin);
    }
}

public class BlankInteraction : InputValueClass
{
}
