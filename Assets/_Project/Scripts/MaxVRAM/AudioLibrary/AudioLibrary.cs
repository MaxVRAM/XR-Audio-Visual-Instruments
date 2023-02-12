using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MaxVRAM.Assets;
using System.Text.RegularExpressions;
using System.IO;
using UnityEditor.AssetImporters;
using Unity.Entities.UniversalDelegates;

namespace MaxVRAM.Audio.Library
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioLibrary : MonoBehaviour
    {
        public static AudioLibrary Instance;

        [SerializeField] private bool _Initialised = false;
        
        public AudioSource _PreviewAudioSource;
        [SerializeField] private string _AudioFilePath = "Assets/_Project/Audio/Wav/";
        [SerializeField] private string _AudioAssetPath = "Assets/_Project/Audio/Assets/";
        [SerializeField] private string _AssetFilter = "t:AudioClip";
        [SerializeField] private string _Extension = ".wav";
        [SerializeField] private List<AudioAsset> _AudioAssets = new List<AudioAsset>();

        public List<AudioAsset> AudioAssets { get { return _AudioAssets; } }

        public int LibrarySize { get { return _AudioAssets.Count; } }

        public bool InitialiseLibrary()
        {
            Debug.Log("Initializing Audio Library...");

            //Resources.LoadAll("", typeof(AudioAssetObject));

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
            if (!_Initialised)
                return null;

            AudioClip[] audioClips = new AudioClip[_AudioAssets.Count];

            for (int i = 0; i < _AudioAssets.Count; i++)
                audioClips[i] = _AudioAssets[i].Clip;
            return audioClips;
        }

        public AudioClip GetClip(int index)
        {
            if (!_Initialised || index >= _AudioAssets.Count || _AudioAssets[index] == null)
                return null;

            return _AudioAssets[index].Clip;
        }

        #region ASSET MANAGEMENT

        public void BuildAudioAssets()
        {
            string audioFolder = _AudioFilePath.EndsWith("/") ? _AudioAssetPath : _AudioAssetPath + "/";
            string assetFolder = _AudioAssetPath.EndsWith("/") ? _AudioAssetPath : _AudioAssetPath + "/";

            if (!AssetDatabase.IsValidFolder(assetFolder))
                Directory.CreateDirectory(assetFolder);

            string[] assetGUIDs = AssetDatabase.FindAssets(_AssetFilter, new[] { _AudioFilePath });
            string[] assetFiles = new string[assetGUIDs.Length];
            _AudioAssets = new List<AudioAsset>();

            for (int i = 0; i < assetFiles.Length; i++)
            {
                assetFiles[i] = AssetDatabase.GUIDToAssetPath(assetGUIDs[i]);
                string fileName = assetFiles[i];
                fileName = fileName.Remove(0, assetFiles[i].LastIndexOf('/') + 1);
                string[] typeName = fileName.Split('_');
                string clipTypeString = fileName.Substring(0, fileName.IndexOf("_"));
                clipTypeString = Regex.Replace(clipTypeString, @"[^\w]*", String.Empty);
                string clipName = fileName.Substring(fileName.IndexOf("_") + 1);
                clipName = clipName.Remove(clipName.IndexOf(_Extension)).ToLower();

                AudioAsset newAudioAsset = (AudioAsset)ScriptableObject.CreateInstance(typeof(AudioAsset));
                AudioClip clip = (AudioClip)AssetDatabase.LoadAssetAtPath(assetFiles[i], typeof(AudioClip));

                string clipTypeFolder = assetFolder;
                AudioClipType clipType = GetAudioClipTypeAndPath(clipTypeString, ref clipTypeFolder);

                _AudioAssets.Add(newAudioAsset.AssociateAudioClip(clip, clipType, _AudioAssets.Count()));
                AssetDatabase.CreateAsset(newAudioAsset, clipTypeFolder + clipName + ".asset");
                EditorUtility.SetDirty(newAudioAsset);
            }
            AssetDatabase.Refresh();
            Debug.Log($"Audio Library has been rebuilt with '{_AudioAssets.Count()}' Audio Assets.");
        }

        public AudioClipType GetAudioClipTypeAndPath(string clipTypeString, ref string clipTypeFolder)
        {
            if (!Enum.TryParse(clipTypeString, out AudioClipType clipType))
                clipType = AudioClipType.Default;

            clipTypeFolder += clipType.ToString() + "/";

            if (!AssetDatabase.IsValidFolder(clipTypeFolder))
                Directory.CreateDirectory(clipTypeFolder);

            //Enum.GetName(typeof(AudioClipType), clipType));
            return clipType;
        }

        #endregion
    }

    public enum AudioClipType
    {
        Default = 0,
        OneShot = 1,
        Short = 2,
        Long = 3,
        Loop = 4
    }
}
