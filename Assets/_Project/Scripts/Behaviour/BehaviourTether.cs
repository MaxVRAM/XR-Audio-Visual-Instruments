using UnityEngine;

public class BehaviourTether : BehaviourClass
{
    public bool _TetherActive = true;
    public float _TetherLength;
    public float _TetherSpringAmount;
    public float _TetherSpringSpeed;
    public bool _LineVisible = true;
    public GameObject _LinePrototype;

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
