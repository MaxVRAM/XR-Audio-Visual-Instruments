using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

public class VFX_SpawnerParticlesScript : MonoBehaviour
{
    public GameObject _SpawnerObject;
    protected Rigidbody _SpawnerRigidBody;
    public VisualEffect _SpawnerVFX;
    protected int _VelocityID;
    protected int _AngularVelocityID;

    void Awake()
    {
        _SpawnerVFX = GetComponent<VisualEffect>();
    }

    // Start is called before the first frame update
    void Start()
    {
        if (_SpawnerVFX == null) _SpawnerVFX = GetComponent<VisualEffect>();
        if (_SpawnerVFX == null || _SpawnerObject == null || !_SpawnerObject.TryGetComponent(out _SpawnerRigidBody))
            enabled = false;
        if (enabled)
        {
            _VelocityID = Shader.PropertyToID("SpawnerVelocity");
            _AngularVelocityID = Shader.PropertyToID("SpawnerAngularVelocity");
        }
    }

    // Update is called once per frame
    void Update()
    {
        _SpawnerVFX.SetVector3(_VelocityID, _SpawnerRigidBody.velocity);
        _SpawnerVFX.SetVector3(_AngularVelocityID, _SpawnerRigidBody.angularVelocity);
    }
}
