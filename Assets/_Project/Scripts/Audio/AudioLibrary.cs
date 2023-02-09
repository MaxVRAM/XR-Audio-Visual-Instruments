using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


#region AUDIO CLIP LIBRARY

// TODO: Create AudioClipSource and AudioClipLibrary objects to add properties to source
// content, making it much easier to create emitter configurations. Adding properties like
// tagging/grouping, custom names/descriptions, and per-clip processing; like volume,
// compression, and eq; are feasible and would drastically benefit workflow.

[Serializable]
public class AudioLibrary : MonoBehaviour
{
    [SerializeField] private bool _Initialised = false;
    [SerializeField] private List<AudioAsset> _AudioAssets;
    public int LibrarySize { get { return _AudioAssets.Count; } }

    public bool InitialiseLibrary()
    {
        Debug.Log("Initializing Audio Library...");

        _AudioAssets = new List<AudioAsset>();
        _AudioAssets = GetComponentsInChildren<AudioAsset>().ToList();

        for (int i = _AudioAssets.Count - 1; i >= 0; i--)
            if (!_AudioAssets[i].ValidClip)
                _AudioAssets.RemoveAt(i);

        if (_AudioAssets.Count == 0)
        {
            Debug.LogError("No AudioAsset objects found in the Audio Library.");
            _Initialised = false;
            return false;
        }

        Debug.Log($"AudioAsset Library built with {_AudioAssets.Count} categorised audio clips.");

        _Initialised = true;
        return true;
    }

    public AudioClip[] GetClipArray()
    {
        AudioClip[] audioClips = new AudioClip[_AudioAssets.Count];

        for (int i = 0; i < _AudioAssets.Count; i++)
            audioClips[i] = _AudioAssets[i].Clip;
        return audioClips;
    }

    public AudioClip GetClipAndSetEntityIndex(int index)
    {
        if (!_Initialised || index >= _AudioAssets.Count || _AudioAssets[index] == null)
            return null;

        _AudioAssets[index].ClipEntityIndex = index;
        return _AudioAssets[index].Clip;
    }
}

#endregion