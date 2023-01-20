using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SurfaceProperties : MonoBehaviour
{
    [Range(0, 1)]
    public float _Rigidity = 1;
    public bool _ApplyToChildren = false;
    [SerializeField]
    protected bool _IsSurfaceChild = false;
    [SerializeField]
    protected List<SurfaceProperties> _ChildSurfaces;

    void Start()
    {
        if (!_IsSurfaceChild && _ApplyToChildren)
            foreach (Collider collider in GetComponentsInChildren<Collider>())
                if (!collider.gameObject.TryGetComponent(out SurfaceProperties _))
                {
                    SurfaceProperties surface = collider.gameObject.AddComponent<SurfaceProperties>();
                    surface._IsSurfaceChild = true;
                    surface._Rigidity = _Rigidity;
                    _ChildSurfaces.Add(surface);
                }
    }

    void Update()
    {
    }
}
