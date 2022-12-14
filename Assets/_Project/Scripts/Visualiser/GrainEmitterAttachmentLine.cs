﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class GrainEmitterAttachmentLine : MonoBehaviour
{
    LineRenderer _Line;
    public ContinuousAuthoring _GrainEmitter;

    // Start is called before the first frame update
    void Start()
    {
        _Line = GetComponent<LineRenderer>();
        _Line.positionCount = 2;
        _Line.SetPosition(0, _GrainEmitter.transform.position);
    }

    // Update is called once per frame
    void Update()
    {
        if (_GrainEmitter._LinkedToSpeaker)
        {
            if (Vector3.SqrMagnitude(_GrainEmitter.transform.position - _GrainEmitter.DynamicallyLinkedSpeaker.transform.position) > .1f)
            {
                _Line.enabled = true;
                _Line.SetPosition(0, _GrainEmitter.transform.position);
                _Line.SetPosition(1, _GrainEmitter.DynamicallyLinkedSpeaker.transform.position);
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
