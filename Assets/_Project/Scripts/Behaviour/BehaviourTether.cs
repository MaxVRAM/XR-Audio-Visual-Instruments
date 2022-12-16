using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BehaviourTether : BehaviourClass
{
    public bool _LinkLocalToRemote = true;

    void Start()
    {
        if (_LinkLocalToRemote && _Objects._LocalRigidbody != null && _Objects._RemoteRigidbody != null)
        {
            
        }
    }

    void Update()
    {
        
    }
}
