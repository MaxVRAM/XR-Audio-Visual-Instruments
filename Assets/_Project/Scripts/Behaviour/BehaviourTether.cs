using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BehaviourTether : BehaviourClass
{
    public bool _TetherActive = true;
    public float _TetherLength;
    public float _TetherSpringAmount;
    public float _TetherSprintSpeed;
    public bool _LineVisible = true;
    public LineRenderer _LinePrototype;

    void Start()
    {

    }

    void Update()
    {
        
    }

    public override void UpdateBehaviour(BehaviourClass behaviour)
    {
        if (behaviour.GetType() != typeof(BehaviourTether))
            return;
        else
        {
            
        }
    }
}
