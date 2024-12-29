using Unity.Netcode;
using UnityEngine;

namespace HEAVYART.TopDownShooter.Netcode
{
    public class BasicNetworkTransform : NetworkBehaviour
    {
        public bool xPosition = true;
        public bool yPosition = true;
        public bool zPosition = true;

        [Space()]
        public bool xRotation = true;
        public bool yRotation = true;
        public bool zRotation = true;

        [Space()]
        public int positionSmoothingFrames = 5;

        public float positionExtrapolationFactor = 1f;

        [Space()]
        public float rotationInterpolationFactor = 0.5f;

        private readonly NetworkVariable<float> _xPositionValue = new (writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<float> _yPositionValue = new (writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<float> _zPositionValue = new (writePerm: NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<float> _xRotationValue = new (writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<float> _yRotationValue = new (writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<float> _zRotationValue = new (writePerm: NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<Vector3> _fullPosition = new (writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<Quaternion> _fullRotation = new (writePerm: NetworkVariableWritePermission.Owner);

        private const float TeleportDistance = 1;
        private Vector3 _previousPosition;
        private Vector3 _extrapolationOffset;
        private Vector3 _positionDampVelocity;

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
                _fullPosition.Value = transform.localPosition;

            transform.localPosition = _fullPosition.Value;
            _previousPosition = transform.localPosition;
        }

        private void FixedUpdate()
        {
            if (IsOwner)
            {

                if (xPosition && yPosition && zPosition)
                {
                    _fullPosition.Value = transform.localPosition;
                }
                else
                {
                    var position = transform.localPosition;
                    if (xPosition) _xPositionValue.Value = position.x;
                    if (yPosition) _yPositionValue.Value = position.y;
                    if (zPosition) _zPositionValue.Value = position.z;
                }

                if (xRotation && yRotation && zRotation)
                {
                    _fullRotation.Value = transform.localRotation;
                }
                else
                {
                    var rotation = transform.localRotation.eulerAngles;
                    if (xRotation) _xRotationValue.Value = rotation.x;
                    if (yRotation) _yRotationValue.Value = rotation.y;
                    if (zRotation) _zRotationValue.Value = rotation.z;
                }
            }
            else
            {

                var position = transform.localPosition;
                if (xPosition && yPosition && zPosition)
                {
                    position = _fullPosition.Value;
                }
                else
                {
                    if (xPosition) position.x = _xPositionValue.Value;
                    if (yPosition) position.y = _yPositionValue.Value;
                    if (zPosition) position.z = _zPositionValue.Value;
                }

                Quaternion rotation;
                if (xRotation && yRotation && zRotation)
                {
                    rotation = _fullRotation.Value;
                }
                else
                {
                    var eulerAngles = Vector3.zero;
                    if (xRotation) eulerAngles.x = _xRotationValue.Value;
                    if (yRotation) eulerAngles.y = _yRotationValue.Value;
                    if (zRotation) eulerAngles.z = _zRotationValue.Value;

                    rotation = Quaternion.Euler(eulerAngles);
                }

                if ((transform.localPosition - position).magnitude > TeleportDistance)
                    transform.localPosition = position;

                _extrapolationOffset = (position - _previousPosition) * positionExtrapolationFactor * positionSmoothingFrames;
                transform.localPosition = Vector3.SmoothDamp(transform.localPosition, position + _extrapolationOffset, ref _positionDampVelocity, positionSmoothingFrames * Time.fixedDeltaTime);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, rotation, rotationInterpolationFactor);

                _previousPosition = position;
            }
        }
    }
}
