using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BehaviourClass : MonoBehaviour
{
    public List<GameObject> _EmitterHosts;

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public void AddHost(GameObject go)
    {
        if (!_EmitterHosts.Contains(go))
            _EmitterHosts.Add(go);
    }
    public void RemoveHost(GameObject go)
    {
        if (_EmitterHosts.Contains(go))
            _EmitterHosts.Remove(go);
    }

    public void ProcessNewHost()
    {
        
    }
}
