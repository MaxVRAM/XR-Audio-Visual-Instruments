using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;


/// <summary>
//      Base interaface for managing synth entities
/// <summary>

public class SynthEntityBase : MonoBehaviour
{
    #region FIELDS & PROPERTIES

    public enum SynthEntityType { Blank, Speaker, Host, Emitter };

    protected EntityManager _EntityManager;
    protected EntityArchetype _Archetype;
    protected Entity _Entity;
    [SerializeField] protected SynthEntityType _EntityType = SynthEntityType.Blank;
    [SerializeField] protected int _EntityIndex = int.MaxValue;
    [SerializeField] protected bool _EntityInitialised = false;
    [SerializeField] protected bool _ManagerInitialised = false;
    public int EntityIndex { get { return _EntityIndex; } }

    #endregion

    #region PRIMARY ENTITY UPDATE LOOP

    public bool PrimaryUpdate()
    {
        if (_EntityType == SynthEntityType.Blank)
            return false;

        if (!ManagerReady())
            return false;

        if (!EntityReady())
            return false;

        UpdateComponents();
        return true;
    }

    public virtual void InitialiseComponents() { }
    public virtual void UpdateComponents() { }

    #endregion

    #region ENTIY MANAGEMENT

    public void SetIndex(int index)
    {
        _EntityIndex = index;
        SetEntityType();
        name = $"{Enum.GetName(typeof(SynthEntityType), _EntityType)}.{_EntityIndex}.{transform.parent.name}";
        SetEntityName();
    }

    public virtual void SetEntityType() { }

    public bool SetEntityName()
    {
#if UNITY_EDITOR
        if (_Entity == Entity.Null)
            return false;

        if (_EntityManager.GetName(_Entity) != name)
        {
            _EntityManager.SetName(_Entity, name);
            return false;
        }
#endif
        return true;
    }

    public bool ManagerReady()
    {
        if (_EntityManager == null || _Archetype == null)
        {
            _ManagerInitialised = false;
            _EntityInitialised = false;
            return false;
        }
        if (!_ManagerInitialised)
        {
            _ManagerInitialised = true;
            _EntityInitialised = false;
            return false; // force additional frame wait before populating
        }
        return true;
    }

    public bool EntityReady()
    {
        if (!_EntityInitialised)
        {
            if (!ManagerReady())
                return false;

            if (EntityIndex == int.MaxValue)
                return false;

            if (_Entity == Entity.Null)
            {
                // Debug.Log($"Creating new entity: {name}");
                _Entity = _EntityManager.CreateEntity(_Archetype);
                return false;
            }

            if (!SetEntityName())
                return false;

            InitialiseComponents();
            _EntityInitialised = true;
            return false;
        }
        else return true;
    }

    private void OnDestroy()
    {
        DestroyEntity();
    }

    public void DestroyEntity()
    {
        Deregister();
        try
        {
            if (_EntityManager != null && World.All.Count != 0 && _Entity != null)
            {
                _EntityManager.DestroyEntity(_Entity);
            }
        }
        catch (Exception ex) when (ex is NullReferenceException)
        {
            Debug.Log($"Failed to destroy entity: {ex.Message}");
        }
    }

    public virtual void Deregister() { }

    #endregion
}

