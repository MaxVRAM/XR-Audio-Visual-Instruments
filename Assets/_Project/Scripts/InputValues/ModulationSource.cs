using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class InputObjects
{
    public GameObject _LocalObject;
    public Rigidbody _LocalRigidbody;
    public GameObject _RemoteObject;
    public Rigidbody _RemoteRigidbody;

    public void SetLocalObject(GameObject go)
    {
        _LocalObject = go;
        if (!go.TryGetComponent(out _LocalRigidbody))
            Debug.Log("Warning: No Rigidbody component on _LocalObject    " + go.name);
    }

    public void SetRemoteObject(GameObject go)
    {
        _RemoteObject = go;
        if (!go.TryGetComponent(out _RemoteRigidbody))
            Debug.Log("Warning: No Rigidbody component on _RemoteObject    " + go.name);
    }
}

public class ModulationSource : MonoBehaviour
{
    public InputObjects _Objects;
    public float _InputMin = 0f;
    public float _InputMax = 1f;
    public float _OutputValue = 0;
    protected float _PreviousValue = 0;
    protected bool _HoldTempValue = false;
    protected bool _Colliding = false;
    protected PhysicMaterial _ColliderMaterial;

    void Start() { }

    void Update() { }

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

    public void SetInputCollision(bool colliding, PhysicMaterial material)
    {
        _Colliding = colliding;
        _ColliderMaterial = material;
    }

    public virtual void ProcessCollisionValue(Collision collision) { }

    public static float Map(float val, float inMin, float inMax, float outMin, float outMax)
    {
        return outMin + ((outMax - outMin) / (inMax - inMin)) * (val - inMin);
    }
}

public class BlankModulation : ModulationSource
{

}
