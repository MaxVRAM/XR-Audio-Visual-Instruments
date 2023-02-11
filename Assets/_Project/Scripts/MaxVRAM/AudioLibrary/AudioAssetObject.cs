using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// < summary >
/// Scriptable Object for creating audio clip assets with assignable types and other properties.
/// Will be used for expanding audio library paradigm to make it easier to manage and
/// assign audio assets to interactive synthesis elements.
/// < summary >

namespace MaxVRAM.Audio.Library
{
    [CreateAssetMenu(fileName = "AudioAssetObject", menuName = "ScriptableObjects/AudioAssetObject")]
    public class AudioAssetObject : ScriptableObject
    {
        [SerializeField] private int _ClipEntityIndex;
        [SerializeField] private AudioClip _Clip;
        [SerializeField] private AudioClipType _ClipType;

        public int ClipEntityIndex { get { return _ClipEntityIndex; } set { _ClipEntityIndex = value; } }
        public Library.AudioClipType ClipType { get { return _ClipType; } }
        public AudioClip Clip { get { return _Clip; } }
        public bool ValidClip { get { return _Clip != null; } }

    }
}


