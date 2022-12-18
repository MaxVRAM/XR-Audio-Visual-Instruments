using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class AttachmentLine : MonoBehaviour
{
    LineRenderer _Line;
    public bool _Active;
    public GameObject _ObjectA;
    public GameObject _ObjectB;

    void Start()
    {
        _Line = GetComponent<LineRenderer>();
        _Line.positionCount = 2;
        _Line.SetPosition(0, _ObjectA.transform.position);
    }

    void Update()
    {
        if (_Active)
            if (Vector3.SqrMagnitude(_ObjectA.transform.position - _ObjectB.transform.position) > .1f)
            {
                _Line.enabled = true;
                _Line.SetPosition(0, _ObjectA.transform.position);
                _Line.SetPosition(1, _ObjectB.transform.position);
            }
            else
                _Line.enabled = false;
        else
            _Line.enabled = false;
    }
}
