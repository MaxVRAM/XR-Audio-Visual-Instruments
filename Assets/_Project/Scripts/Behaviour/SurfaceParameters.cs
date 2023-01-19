using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SurfaceParameters : MonoBehaviour
{
    public enum SurfaceParameter
    {
        Rigidity
    }

    [Range(0, 1)]
    public float _Rigidity = 1;
    public bool _ApplyToChildren = false;
    [SerializeField]
    protected List<SurfaceParameters> _ChildSurfaces;
    [SerializeField]
    protected bool _IsSurfaceChild = false;

    void Start()
    {
        if (!_IsSurfaceChild && _ApplyToChildren)
            foreach (Collider collider in GetComponentsInChildren<Collider>())
                if (!collider.gameObject.TryGetComponent(out SurfaceParameters _))
                {
                    SurfaceParameters surface = collider.gameObject.AddComponent<SurfaceParameters>();
                    surface._IsSurfaceChild = true;
                    surface._Rigidity = _Rigidity;
                    _ChildSurfaces.Add(surface);
                }
    }

    void Update()
    {
    }
}
