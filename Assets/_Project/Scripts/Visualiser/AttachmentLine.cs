using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class AttachmentLine : MonoBehaviour
{
    protected LineRenderer _Line;
    public bool _Active;
    public Transform _TransformA;
    public Transform _TransformB;

    void Start()
    {        
        if (!TryGetComponent(out _Line))
        {
            _Line = gameObject.AddComponent<LineRenderer>();
            _Line.widthMultiplier = GrainSynth.Instance._AttachmentLineWidth;
            _Line.enabled = false;
        }
        if (_Line.material == null)
            _Line.material = GrainSynth.Instance._AttachmentLineMat;

        _Line.positionCount = 2;
        _Line.SetPosition(0, _TransformA.transform.position);
    }

    void Update()
    {
        if (_Active)
            if (Vector3.SqrMagnitude(_TransformA.position - _TransformB.position) > .1f)
            {
                _Line.enabled = true;
                _Line.SetPosition(0, _TransformA.position);
                _Line.SetPosition(1, _TransformB.position);
            }
            else
                _Line.enabled = false;
        else
            _Line.enabled = false;
    }
}
