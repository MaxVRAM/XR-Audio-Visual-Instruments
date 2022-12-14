using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class EmitterAttachmentLine : MonoBehaviour
{
    LineRenderer _Line;
    public EmitterAuthoring _Emitter;

    // Start is called before the first frame update
    void Start()
    {
        _Line = GetComponent<LineRenderer>();
        _Line.positionCount = 2;
        _Line.SetPosition(0, _Emitter.transform.position);
    }

    // Update is called once per frame
    void Update()
    {
        if (_Emitter._Speaker != null)
        {
            if (Vector3.SqrMagnitude(_Emitter.transform.position - _Emitter._Speaker.transform.position) > .1f)
            {
                _Line.enabled = true;
                _Line.SetPosition(0, _Emitter.transform.position);
                _Line.SetPosition(1, _Emitter._Speaker.transform.position);
            }
            else
            {
                _Line.enabled = false;
            }
        }
        else
        {
            _Line.enabled = false;
        }
    }
}
