/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using UnityEngine;

namespace Oculus.Interaction
{
    /// <summary>
    /// A Transformer that moves the target in a 1-1 fashion with the GrabPoint.
    /// Updates transform the target in such a way as to maintain the target's
    /// local positional and rotational offsets from the GrabPoint.
    /// </summary>
    public class OneGrabTransformRigidbody : MonoBehaviour, ITransformer
    {

        private IGrabbable _grabbable;
        private Rigidbody _grabbableRigidBody;
        private Pose _grabDeltaInLocalSpace;
        public float _PositionDamping = 0.1f;
        public float PositionDamping { get { return _RotationDamping / 8; }}
        public float _RotationDamping = 0.1f;
        public float RotationDamping { get { return 1 / _RotationDamping * 10; }}
        public Vector3 _Velocity;
        public Vector3 _AngularVelocity;

        public void Initialize(IGrabbable grabbable)
        {
            _grabbable = grabbable;
            _grabbableRigidBody = grabbable.Transform.gameObject.GetComponent<Rigidbody>();
            if (grabbable.Transform.gameObject.TryGetComponent(out _grabbableRigidBody))
                _Velocity = Vector3.zero;
        }

        public void BeginTransform()
        {
            Pose grabPoint = _grabbable.GrabPoints[0];
            var targetTransform = _grabbable.Transform;
            _grabDeltaInLocalSpace = new Pose(targetTransform.InverseTransformVector(grabPoint.position - targetTransform.position),
                                            Quaternion.Inverse(grabPoint.rotation) * targetTransform.rotation);
        }

        public void UpdateTransform()
        {
            Pose grabPoint = _grabbable.GrabPoints[0];
            Transform objectTransform = _grabbable.Transform;

            if (_grabbableRigidBody != null)
            {
                Quaternion targetRotation = grabPoint.rotation * _grabDeltaInLocalSpace.rotation;
                targetRotation = Quaternion.Lerp(objectTransform.rotation, targetRotation, Time.deltaTime * RotationDamping);
                _AngularVelocity = targetRotation.eulerAngles - objectTransform.rotation.eulerAngles;
                _grabbableRigidBody.MoveRotation(targetRotation);

                Vector3 targetPosition = grabPoint.position - _grabbable.Transform.TransformVector(_grabDeltaInLocalSpace.position);
                targetPosition = Vector3.SmoothDamp(_grabbableRigidBody.position, targetPosition, ref _Velocity, PositionDamping);
                _grabbableRigidBody.MovePosition(targetPosition);
            }
            else
            {
                objectTransform.rotation = grabPoint.rotation * _grabDeltaInLocalSpace.rotation;
                objectTransform.position = grabPoint.position - objectTransform.TransformVector(_grabDeltaInLocalSpace.position);
            }            
        }

        public void EndTransform()
        {
            _Velocity = Vector3.zero;
        }
    }
}
