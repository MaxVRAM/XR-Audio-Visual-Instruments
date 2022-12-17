using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CollisionPipe : MonoBehaviour
{
    [SerializeField]
    protected List<HostAuthoring> _HostComponentPipes;

    void Start()
    {
    }

    public CollisionPipe AddHost(HostAuthoring host)
    {
        if (_HostComponentPipes == null) _HostComponentPipes = new List<HostAuthoring>();
        if (_HostComponentPipes.Count == 0 || !_HostComponentPipes.Contains(host))
            _HostComponentPipes.Add(host);
        return this;
    }

    public void RemoveHost(HostAuthoring host)
    {
        if (_HostComponentPipes.Contains(host))
            _HostComponentPipes.Remove(host);
    }

    private void OnCollisionEnter(Collision collision)
    {
        foreach (HostAuthoring host in _HostComponentPipes)
            host.OnCollisionEnter(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        foreach (HostAuthoring host in _HostComponentPipes)
            host.OnCollisionStay(collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        foreach (HostAuthoring host in _HostComponentPipes)
            host.OnCollisionExit(collision);
    }
}
