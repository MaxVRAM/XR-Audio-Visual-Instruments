using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.Text.RegularExpressions;
using System.IO;
using Unity.Collections;
using Unity.Entities;


namespace PlaneWaver
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioLibrary : MonoBehaviour
    {
        #region FIELDS & PROPERTIES

        public static AudioLibrary Instance;

        private bool _Initialised = false;
        public AudioSource _PreviewAudioSource;
        [SerializeField] private string _AudioFilePath = "Assets/_Project/Audio/Wav/";
        [SerializeField] private string _AudioAssetPath = "Assets/_Project/Audio/Assets/";
        [SerializeField] private string _AssetFilter = "t:AudioClip";
        [SerializeField] private string _Extension = ".wav";
        [SerializeField] private List<AudioAsset> _AudioAssets = new List<AudioAsset>();
        public List<AudioAsset> AudioAssets { get { return _AudioAssets; } }
        public int LibrarySize { get { return _AudioAssets.Count; } }

        #endregion

        #region LIBRARY INITIALISATION

        private void Awake()
        {
            Instance = this;
            if (InitialiseLibrary())
                BuildAudioEntities(SynthComponentType.AudioClip);
        }

        public bool InitialiseLibrary()
        {
            //Resources.LoadAll("", typeof(AudioAssetObject));

            if (_Initialised)
                return true;

            for (int i = _AudioAssets.Count - 1; i >= 0; i--)
                if (!_AudioAssets[i].ValidClip)
                    _AudioAssets.RemoveAt(i);

            if (_AudioAssets.Count == 0)
            {
                Debug.LogError("No AudioAsset objects found in the Audio Library.");
                _Initialised = false;
                return false;
            }

            _Initialised = true;
            return true;
        }

        #endregion

        #region AUDIO ENTITIES
        
        public void BuildAudioEntities(string entityName)
        {
            if (!_Initialised)
                return;

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            foreach (AudioAsset audioAsset in _AudioAssets)
            {
                Entity audioClipSamplesEntity = entityManager.CreateEntity();

                AudioClip clip = audioAsset.Clip;
                int clipChannels = clip.channels;
                float[] clipData = new float[clip.samples];
                clip.GetData(clipData, 0);

                using BlobBuilder blobBuilder = new BlobBuilder(Allocator.Temp);
                ref FloatBlobAsset audioClipBlobAsset = ref blobBuilder.ConstructRoot<FloatBlobAsset>();

                BlobBuilderArray<float> sampleArray = blobBuilder.Allocate(ref audioClipBlobAsset.array, (clipData.Length / clipChannels));
                GetAudioClipSampleBlob(ref sampleArray, clipData, clipChannels);

                BlobAssetReference<FloatBlobAsset> audioClipBlobAssetRef = blobBuilder.CreateBlobAssetReference<FloatBlobAsset>(Allocator.Persistent);
                entityManager.AddComponentData(audioClipSamplesEntity, new AudioClipDataComponent 
                { 
                    _ClipDataBlobAsset = audioClipBlobAssetRef,
                    _ClipIndex = audioAsset.ClipEntityIndex
                });

#if UNITY_EDITOR
                entityManager.SetName(audioClipSamplesEntity, entityName + "." + audioAsset.ClipEntityIndex + "." + clip.name);
#endif
            }
        }

        private static void GetAudioClipSampleBlob(ref BlobBuilderArray<float> audioBlob, float[] samples, int channels)
        {
            for (int s = 0; s < samples.Length - 1; s += channels)
            {
                audioBlob[s / channels] = 0;
                for (int c = 0; c < channels; c++)
                    audioBlob[s / channels] += samples[s + c] / channels;
            }
        }

        #endregion

        #region ASSET MANAGEMENT

        public void BuildAudioAssets()
        {
            if (!AssetDatabase.IsValidFolder(_AudioAssetPath))
                Directory.CreateDirectory(_AudioAssetPath);

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
                clipTypeString = Regex.Replace(clipTypeString, @"[^\w]*", string.Empty);
                string clipName = fileName.Substring(fileName.IndexOf("_") + 1);
                clipName = clipName.Remove(clipName.IndexOf(_Extension)).ToLower();

                AudioAsset newAudioAsset = (AudioAsset)ScriptableObject.CreateInstance(typeof(AudioAsset));
                AudioClip clip = (AudioClip)AssetDatabase.LoadAssetAtPath(assetFiles[i], typeof(AudioClip));

                string clipTypeFolder = _AudioAssetPath;
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
