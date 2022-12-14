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
    public SpeakerAuthoring _Speaker;
    public List<BaseEmitterClass> _SelfEmitters;
    public List<BaseEmitterClass> _ContactEmitters;
    public InteractionBase[] _Interactions;
    public bool _HostContactEmitters = false;
    public List<BaseEmitterClass> _HostedContactEmitters;
    public List<GameObject> _CollidingObjects;
    protected GameObject _ThisGameObject;

    private void Start()
    {
        _ThisGameObject = gameObject;
        
        if (_Speaker == null)
            _Speaker = GetComponentInChildren<SpeakerAuthoring>();

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

    public void ResetEmitterInteractions(GameObject primaryObject, GameObject secondaryObject, Collision collision)
    {
        foreach (BaseEmitterClass emitter in _SelfEmitters)
            emitter.ResetEmitter(primaryObject, secondaryObject, collision);
    }

    public void UpdateEmitterSpeaker(SpeakerAuthoring speaker)
    {
        if (speaker != null)
        {
            _Speaker = speaker;
            foreach (BaseEmitterClass emitter in _SelfEmitters)
                emitter.UpdateLinkedSpeaker(speaker);
        }
        else if (_Speaker != null)
            foreach (BaseEmitterClass emitter in _SelfEmitters)
                emitter.UpdateLinkedSpeaker(_Speaker);
    }

    private void OnCollisionEnter(Collision collision)
    {
        foreach (InteractionBase interaction in _Interactions)
            interaction.ProcessCollision(collision);

        foreach (BaseEmitterClass emitter in _SelfEmitters)
            if (emitter._EmitterType == BaseEmitterClass.EmitterType.Burst)
                emitter.UpdateCollision(collision);

        EmitterActionManager remoteActionManager = collision.collider.GetComponent<EmitterActionManager>();
        if (remoteActionManager != null && !_CollidingObjects.Contains(remoteActionManager.gameObject))
        {
            _CollidingObjects.Add(collision.collider.gameObject);
            // Instantiate any contact emitters that are registered with the colliding object
            if (_HostContactEmitters)
                foreach (BaseEmitterClass emitter in remoteActionManager._ContactEmitters)
                {
                    GameObject newEmitterObject = Instantiate(emitter.gameObject, gameObject.transform);
                    BaseEmitterClass newEmitter = newEmitterObject.GetComponent<BaseEmitterClass>();
                    newEmitter.ResetEmitter(null, null, collision);
                    newEmitter.UpdateLinkedSpeaker(_Speaker);
                    // We only need to keep track of continuous contact, not burst emitters.
                    if (newEmitter._EmitterType == BaseEmitterClass.EmitterType.Continuous)
                        _HostedContactEmitters.Add(newEmitterObject.GetComponent<BaseEmitterClass>());
                }
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        foreach (InteractionBase interaction in _Interactions)
            interaction.UpdateCollideStatus(true, collision.collider.material);

        foreach (BaseEmitterClass emitter in _ContactEmitters)
            if (emitter._EmitterType == BaseEmitterClass.EmitterType.Continuous)
                emitter.UpdateCollision(collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        foreach (InteractionBase interaction in _Interactions)
            interaction.UpdateCollideStatus(false, collision.collider.material);

        if (_CollidingObjects.Count == 0)
            foreach (BaseEmitterClass emitter in _ContactEmitters)
                emitter.UpdateCollision(null);

        for (int i = _HostedContactEmitters.Count - 1; i >= 0; i--)
        {
            if (_HostedContactEmitters[i]._SecondaryObject == collision.collider.gameObject)
            {
                Destroy(_HostedContactEmitters[i].gameObject);
                _HostedContactEmitters.RemoveAt(i);
            }
        }

        _CollidingObjects.Remove(collision.collider.gameObject);
    }
}
