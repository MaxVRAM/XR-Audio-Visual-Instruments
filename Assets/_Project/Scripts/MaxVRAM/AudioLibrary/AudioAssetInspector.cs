using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MaxVRAM.Audio;

[CustomEditor(typeof(AudioAsset))]
public class AudioAssetInspector : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        AudioAsset audioAsset = (AudioAsset)target;

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Preview Sound"))
        {
            Debug.Log($"This will play a sound from {audioAsset.name}.");
        }

        GUILayout.EndHorizontal();
    }
}
