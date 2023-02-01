using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Synth
{
    public class SynthManager : MonoBehaviour
    {
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
    }

    public enum State
    {
        Retired = 0,
        Unregistered = 1,
        Disconnected = 2,
        Connected = 3,
        Active = 4
    }
}

