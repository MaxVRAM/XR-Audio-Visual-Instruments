using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using Oculus.Interaction;

public class VFX_SpawnerParticlesScript : MonoBehaviour
{
    public GameObject _SpawnerObject;
    protected Rigidbody _SpawnerRigidBody;
    public VisualEffect _SpawnerVFX;
    protected int _VelocityID;
    protected int _AngularVelocityID;
    public OneGrabTransformRigidbody _GrabTransform;
    public Vector3 _Velocity;
    public Vector3 _AngularVelocity;
    public float _VelocitySmoothing = 0.1f;
    public float VelocitySmoothing { get { return 1 / _VelocitySmoothing * 10; }}
    public float _AngularSmoothing = 0.1f;
    public float AngularSmoothing { get { return 1 / _AngularSmoothing * 10; }}

    void Awake()
    {
        _SpawnerVFX = GetComponent<VisualEffect>();
    }

    // Start is called before the first frame update
    void Start()
    {
        if (_SpawnerVFX == null) _SpawnerVFX = GetComponent<VisualEffect>();
        if (_SpawnerObject == null || !_SpawnerObject.TryGetComponent(out _SpawnerRigidBody))
            enabled = false;
        if (enabled)
        {
            _GrabTransform = _SpawnerObject.GetComponent<OneGrabTransformRigidbody>();
            _VelocityID = Shader.PropertyToID("SpawnerVelocity");
            // _AngularVelocityID = Shader.PropertyToID("SpawnerAngularVelocity");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (_GrabTransform != null)
        {
            _Velocity = Vector3.Lerp(_Velocity, _GrabTransform._Velocity, Time.deltaTime * VelocitySmoothing);
            // _AngularVelocity = Vector3.Lerp(_AngularVelocity, _GrabTransform._AngularVelocity, Time.deltaTime * AngularSmoothing);
        }
        else
        {
            _Velocity = _SpawnerRigidBody.velocity;
            // _AngularVelocity = _SpawnerRigidBody.angularVelocity;

        }
        _SpawnerVFX.SetVector3(_VelocityID, _Velocity);
        // _SpawnerVFX.SetVector3(_AngularVelocityID, _AngularVelocity);
    }
}
