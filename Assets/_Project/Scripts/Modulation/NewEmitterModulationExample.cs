using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace PlaneWaver
{
    public class NewEmitterModulationExample : MonoBehaviour
    {
        public Actor _ActorA;

        public GameObject _GameObjectB;
        public Actor _ActorB;

        public ModulationInput _ModulationInput = new();

        public void Start()
        {
            _ActorA = new(transform);
            _ActorB = new(_GameObjectB.transform);
            _ModulationInput.SetBothActors(_ActorA, _ActorB);
        }


        public void Update()
        {
            _ModulationInput.ProcessValue();
        }


        public void OnCollisionEnter(Collision collision)
        {
            _ActorA.LatestCollision = collision;
        }

        public void OnCollisionStay(Collision collision)
        {
            _ActorA.IsColliding = true;
        }

        public void OnCollisionExit(Collision collision)
        {
            _ActorA.IsColliding = false;
        }
    }

}