using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace Synth
{
    [RequireComponent(typeof(AudioSource))]
    public class SynthSpeaker : MonoBehaviour
    {
        // DOTS
        protected EntityManager _EntityManger;
        protected Entity _SpeakerEntity;


        // Synth Speaker fields
        public int _GrainPoolSize = 100;
        [SerializeField] protected State _State = State.Unregistered;
        protected int _Index = int.MaxValue;
        protected int _SampleRate;
        protected float _VolumeFade = 0;
        protected Grain[] _GrainDataArray;


        // Game-world fields
        protected AudioSource _AudioSource;
        protected MeshRenderer _MeshRenderer;

        // Runtime info



        protected Entity CreateEntity()
        {
            return new Entity();
        }

        private void Awake()
        {

        }
        void Start()
        {

        }
        void Update()
        {

        }
        private void OnDestroy()
        {

        }

        private void ProcessState(State state)
        {
            if (state == _State)
                return;

            _State = state;

            switch (_State)
            {
                case State.Retired:
                    Destroy(gameObject);
                    break;
                case State.Unregistered:
                    Destroy(gameObject);
                    break;
                case State.Disconnected:
                    Destroy(gameObject);
                    break;
                case State.Connected:
                    Destroy(gameObject);
                    break;
                case State.Active:
                    Destroy(gameObject);
                    break;
            }

                 
            //_TargetVolume = currentActiveState ? 1 : 0;
            //_AudioSource.volume = Mathf.Lerp(_AudioSource.volume, _TargetVolume, Time.deltaTime * _VolumeSmoothing);
            //if (_TargetVolume == 0 && _AudioSource.volume < .005f)
            //    _AudioSource.volume = 0;
            //if (_MeshRenderer != null)
            //    _MeshRenderer.enabled = currentActiveState;

        }
    }

    public struct SpeakerIndex : IComponentData
    {
        public int Value;
    }
 
}
