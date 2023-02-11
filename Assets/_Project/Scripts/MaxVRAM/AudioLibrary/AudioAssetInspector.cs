using MaxVRAM.Audio.Library;
using UnityEditor;
using UnityEngine;
using MaxVRAM.Audio.Library;

[CustomEditor(typeof(AudioLibrary))]
public class AudioAssetInspector : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        AudioLibrary audioLibrary = (AudioLibrary)target;

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Preview Sound"))
        {
            if (audioLibrary._AudioSource != null && audioLibrary._AudioAssetObjects != null)
            {
                AudioAssetObject audioAsset = audioLibrary._AudioAssetObjects[0];
                if (audioAsset != null)
                {
                    Debug.Log($"Playing audio asset preview: {audioAsset.Clip.name}.");
                    audioLibrary._AudioSource.clip = audioAsset.Clip;
                    audioLibrary._AudioSource.Play();
                }
            }
        }

        GUILayout.EndHorizontal();
    }
}
