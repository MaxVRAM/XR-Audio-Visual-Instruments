using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AudioAssetObject", menuName = "ScriptableObjects/AudioAssetObject")]
public class AudioAssetObject : ScriptableObject
{
    public enum AudioClipType
    {
        Generic = 0,
        Hit = 1,
        Short = 2,
        Long = 3,
        Loop = 4,
        Note = 5,
        Phrase = 6,
        Voice = 7
    }

    [SerializeField] private int _ClipEntityIndex;
    [SerializeField] private AudioClip _Clip;
    [SerializeField] private AudioClipType _ClipType;

    public int ClipEntityIndex { get { return _ClipEntityIndex; } set { _ClipEntityIndex = value; } }
    public AudioClipType ClipType { get { return _ClipType; } }
    public AudioClip Clip { get { return _Clip; } }
    public bool ValidClip { get { return _Clip != null; } }
}
