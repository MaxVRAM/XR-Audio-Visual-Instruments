using Unity.VisualScripting;
using UnityEngine;

public class BehaviourTether : BehaviourClass
{
    public Joint _Joint;
    public bool _LineVisible = true;
    public bool _TetherActive = true;
    public AttachmentLine _Attachment;

    void Start()
    {
        if (_Joint == null)
        {
            enabled = false;
            gameObject.SetActive(false);
            return;
        }

        _Joint.connectedBody = _ControllerObject.GetComponent<Rigidbody>();

        if (_Attachment == null && !TryGetComponent(out _Attachment))
            _Attachment = gameObject.AddComponent<AttachmentLine>();
        if (_Attachment._TransformA == null)
            _Attachment._TransformA = _SpawnedObject.transform;
        if (_Attachment._TransformB == null)
            _Attachment._TransformB = _ControllerObject.transform;
        _Attachment._Active = true;
        enabled = true;
        gameObject.SetActive(true);
    }

    void Update()
    {
    }

    public override void UpdateBehaviour(BehaviourClass behaviour)
    {
        BehaviourTether newBehaviour = behaviour as BehaviourTether;
        if (newBehaviour != null && _ControllerObject != null)
        {
            _Joint.connectedBody = _ControllerObject.GetComponent<Rigidbody>();
            gameObject.SetActive(true);
        }
    }
}
