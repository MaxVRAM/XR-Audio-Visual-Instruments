using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EmitterActionManager : MonoBehaviour
{
    /// <summary>
    /// When true, this object will spawn "contact" emitters from colliding objects.
    /// </summary>
    public GrainSpeakerAuthoring _Speaker;
    public List<BaseEmitterClass> _SelfEmitters;
    public List<BaseEmitterClass> _ContactEmitters;
    public InteractionBase[] _Interactions;
    public bool _HostContactEmitters = false;
    public List<BaseEmitterClass> _HostedEmitters;
    public List<GameObject> _CollidingObjects;
    protected GameObject _ThisGameObject;

    private void Start()
    {
        _ThisGameObject = gameObject;
        
        if (_Speaker == null)
            _Speaker = GetComponentInChildren<GrainSpeakerAuthoring>();

        BaseEmitterClass[] emitters = GetComponentsInChildren<BaseEmitterClass>();

        foreach (BaseEmitterClass emitter in emitters)
            AddNewEmitter(emitter);

        _Interactions = GetComponentsInChildren<InteractionBase>();
    }

    public void AddNewEmitter(BaseEmitterClass emitter)
    {
        if (emitter._ContactEmitter && !_ContactEmitters.Contains(emitter))
            _ContactEmitters.Add(emitter);
        else if (!_SelfEmitters.Contains(emitter))
            _SelfEmitters.Add(emitter);
    }

    private void OnCollisionEnter(Collision collision)
    {
        foreach (InteractionBase interaction in _Interactions)
        {
            interaction.SetCollisionData(collision);
        }

        foreach (BaseEmitterClass emitter in _SelfEmitters)
        {
            if (emitter._EmitterType == BaseEmitterClass.EmitterType.Burst)
                emitter.NewCollision(collision);
        }

        EmitterActionManager remoteActionManager = collision.collider.GetComponent<EmitterActionManager>();
        if (remoteActionManager != null && !_CollidingObjects.Contains(remoteActionManager.gameObject))
        {
            _CollidingObjects.Add(remoteActionManager.gameObject);

            // Instantiate any contact emitters that are registered with the colliding object
            if (_HostContactEmitters)
                foreach (BaseEmitterClass contactEmitter in remoteActionManager._ContactEmitters)
                {
                    GameObject newEmitter = Instantiate(contactEmitter.gameObject, gameObject.transform);
                    newEmitter.GetComponent<BaseEmitterClass>().SetupContactEmitter(collision, _Speaker);
                    if (newEmitter.GetComponent<ContinuousEmitterAuthoring>() != null)
                        _HostedEmitters.Add(newEmitter.GetComponent<BaseEmitterClass>());
                }
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        foreach (InteractionBase interaction in _Interactions)
        {
            interaction.SetColliding(true, collision.collider.material);
        }

        foreach (BaseEmitterClass emitter in _SelfEmitters)
        {
            if (emitter._EmitterType == BaseEmitterClass.EmitterType.Continuous &&
                emitter._ContactEmitter)
            {
                emitter.UpdateCurrentCollisionStatus(collision);
            }
        }   
    }

    private void OnCollisionExit(Collision collision)
    {
        if (_CollidingObjects.Count == 0)
            foreach (BaseEmitterClass emitter in _SelfEmitters)
            {
                if (emitter._EmitterType == BaseEmitterClass.EmitterType.Continuous)
                    emitter.UpdateCurrentCollisionStatus(null);
            }

        foreach (InteractionBase interaction in _Interactions)
        {
            interaction.SetColliding(false, collision.collider.material);
        }

        for (int i = _HostedEmitters.Count - 1; i >= 0; i--)
        {
            if (_HostedEmitters[i]._SecondaryObject == collision.collider.gameObject)
            {
                Destroy(_HostedEmitters[i].gameObject);
                _HostedEmitters.RemoveAt(i);
            }
        }
        _CollidingObjects.Remove(collision.collider.gameObject);
    }
}
