using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColliderScript : MonoBehaviour
{
    /// <summary>
    /// When true, this object will spawn "attachable" emitters from colliding objects.
    /// </summary>
    public bool _HostAttachableEmittersOnCollision = false;
    public GrainSpeakerAuthoring _Speaker;
    public List<BaseEmitterClass> _SelfEmitters;
    public List<BaseEmitterClass> _AttachableEmitters;
    public List<BaseEmitterClass> _AttachedEmitters;
    public InteractionBase[] _Interactions;
    public int _CollidingCount = 0;
    public List<GameObject> _CollidingObjects;
    public GameObject _ThisGameObject;

    private void Start()
    {
        _ThisGameObject = gameObject;
        
        if (_Speaker == null)
            _Speaker = GetComponentInChildren<GrainSpeakerAuthoring>();

        BaseEmitterClass[] emitters = GetComponentsInChildren<BaseEmitterClass>();

        foreach (var emitter in emitters)
        {
            if (emitter._EmitterSetup == BaseEmitterClass.EmitterSetup.Self)
                _SelfEmitters.Add(emitter);
            else if (emitter._EmitterSetup == BaseEmitterClass.EmitterSetup.Attachable)
                _AttachableEmitters.Add(emitter);
        }

        _Interactions = GetComponentsInChildren<InteractionBase>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        foreach (var interaction in _Interactions)
        {
            interaction.SetCollisionData(collision);
        }

        ColliderScript remoteColliderScript = collision.collider.GetComponent<ColliderScript>();

        if (remoteColliderScript != null)
        {
            if (!_CollidingObjects.Contains(remoteColliderScript.gameObject))
            {
                _CollidingObjects.Add(remoteColliderScript.gameObject);

                if (_HostAttachableEmittersOnCollision)
                    foreach (var attachableEmitter in remoteColliderScript._AttachableEmitters)
                    {
                        GameObject newAttachedEmitter = Instantiate(attachableEmitter.gameObject, gameObject.transform);
                        newAttachedEmitter.GetComponent<BaseEmitterClass>().SetupAttachedEmitter(collision, _Speaker);

                        if (newAttachedEmitter.GetComponent<GrainEmitterAuthoring>() != null)
                            _AttachedEmitters.Add(newAttachedEmitter.GetComponent<BaseEmitterClass>());
                    }
            }
        }

        foreach (var emitter in _SelfEmitters)
        {
            if (emitter._EmitterType == BaseEmitterClass.EmitterType.Burst)
                emitter.NewCollision(collision);
        }

        _CollidingCount++;
    }

    private void OnCollisionStay(Collision collision)
    {
        foreach (var interaction in _Interactions)
        {
            interaction.SetColliding(true, collision.collider.material);
        }

        foreach (var emitter in _SelfEmitters)
        {
            if (emitter._EmitterType == BaseEmitterClass.EmitterType.Grain)
                emitter.UpdateCurrentCollisionStatus(collision);
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        _CollidingCount--;

        foreach (var emitter in _SelfEmitters)
        {
            if (_CollidingCount == 0)
            {
                if (emitter._EmitterType == BaseEmitterClass.EmitterType.Grain)
                    emitter.UpdateCurrentCollisionStatus(null);
            } 
        }

        foreach (var interaction in _Interactions)
        {
            interaction.SetColliding(false, collision.collider.material);
        }

        for (int i = _AttachedEmitters.Count - 1; i >= 0; i--)
        {
            if (_AttachedEmitters[i]._ColldingObject == collision.collider.gameObject)
            {
                Destroy(_AttachedEmitters[i].gameObject);
                _AttachedEmitters.RemoveAt(i);
            }
        }

        _CollidingObjects.Remove(collision.collider.gameObject);
    }
}
