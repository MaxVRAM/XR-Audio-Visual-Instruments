using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BehaviourClass : MonoBehaviour
{
    public List<GameObject> _Sliders;

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public void AddSlider(GameObject go)
    {
        _Sliders.Add(go);
    }
}
