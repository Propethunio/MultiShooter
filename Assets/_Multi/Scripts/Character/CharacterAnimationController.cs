using System.Collections.Generic;
using UnityEngine;

namespace HEAVYART.TopDownShooter.Netcode
{
    public class CharacterAnimationController : MonoBehaviour
    {
        private static readonly int Death = Animator.StringToHash("Death");
        private static readonly int Fire = Animator.StringToHash("Fire");
        private static readonly int Movement = Animator.StringToHash("Movement");
        private static readonly int MovementSpeedMultiplier = Animator.StringToHash("MovementSpeedMultiplier");
        public Animator animator;

        [Space]
        public float spineWeight = 0.1f;
        public float chestWeight = 0.3f;
        public float upperChestWeight = 0.6f;

        [Space]
        public float rotationSmoothness = 5;
        public float layerSwitchSmoothness = 10f;

        private Transform _targetingTransform;
        public Transform lineOfSightTransform;

        private Transform _spine;
        private Transform _chest;
        private Transform _upperChest;

        private Vector3 _movementDirection;
        private float _movementSpeed;
        private Vector3 _previousPosition;
        private List<Vector3> _directions;

        private HealthController _healthController;
        private ModifiersControlSystem _modifiersControlSystem;

        private float[] _animatorLayerWeights;

        private void Awake()
        {
            _spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            _chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            _upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);

            lineOfSightTransform = transform.root.GetComponent<WeaponControlSystem>().lineOfSightTransform;

            _previousPosition = transform.position;
            _movementDirection = Vector3.forward;
            _movementSpeed = 0;

            _directions = new List<Vector3>() { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

            _modifiersControlSystem = GetComponent<ModifiersControlSystem>();
            _healthController = GetComponent<HealthController>();
            _healthController.OnDeath += PlayDeathAnimation;

            _animatorLayerWeights = new float[animator.layerCount];
        }

        private void LateUpdate()
        {
            if (_healthController.isAlive == false)
                return;

            animator.SetFloat(Movement, 0);
            animator.SetFloat(MovementSpeedMultiplier, _modifiersControlSystem.CalculateSpeedMultiplier());

            var targetRotation = Quaternion.LookRotation(FindClosestDirection(lineOfSightTransform.forward));

            if(_movementSpeed > 0.05f)
            {
                var isOppositeDirections = Vector3.Dot(_movementDirection, lineOfSightTransform.forward) < 0;
                animator.SetFloat(Movement, isOppositeDirections ? -1 : 1);
            }

            animator.transform.rotation = Quaternion.Slerp(animator.transform.rotation, targetRotation, rotationSmoothness * Time.deltaTime);

            HandleAiming(_spine, spineWeight);
            HandleAiming(_chest, chestWeight);
            HandleAiming(_upperChest, upperChestWeight);
        }

        private void HandleAiming(Transform bone, float weight)
        {
            if (bone == null)
                return;

            var horizontalLineOfSight = _targetingTransform.position - lineOfSightTransform.position;
            horizontalLineOfSight.Normalize();

            var boneRotation = Quaternion.FromToRotation(animator.transform.forward, horizontalLineOfSight);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, boneRotation, weight) * bone.rotation;
        }

        private void FixedUpdate()
        {
            var movementDelta = transform.position - _previousPosition;
            _movementSpeed = movementDelta.magnitude;

            if (_movementSpeed > 0.01f)
            {
                _movementDirection.y = 0;
                _movementDirection = movementDelta.normalized;
            }

            _previousPosition = transform.position;

            for (var i = 1; i < _animatorLayerWeights.Length; i++)
            {
                var weight = Mathf.MoveTowards(animator.GetLayerWeight(i), _animatorLayerWeights[i], layerSwitchSmoothness * Time.fixedDeltaTime);
                animator.SetLayerWeight(i, weight);
            }
        }

        public void PlayFireAnimation()
        {
            animator.SetTrigger(Fire);
        }

        public void PlayDeathAnimation()
        {
            for (var i = 1; i < _animatorLayerWeights.Length; i++)
            {
                _animatorLayerWeights[i] = 0;
                animator.SetLayerWeight(i, 0);
            }

            animator.SetTrigger(Death);
        }

        public void UpdateWeaponGrip(WeaponGrip weaponGrip, Transform leftHandGripIKTransform)
        {
            for (var i = 1; i < _animatorLayerWeights.Length; i++)
            {
                _animatorLayerWeights[i] = 0;
            }

            if (weaponGrip == WeaponGrip.Rifle)
            {
                _animatorLayerWeights[1] = 1;
                _animatorLayerWeights[3] = 1;
            }

            if (weaponGrip != WeaponGrip.Pistol) return;
            _animatorLayerWeights[0] = 1;
            _animatorLayerWeights[2] = 1;
        }

        public void SetTargetingTransform(Transform targetingTransform)
        {
            this._targetingTransform = targetingTransform;
        }

        private Vector3 FindClosestDirection(Vector3 directionToCompareWith)
        {
            var closestDirection = _directions[0];
            var closestDotProduct = Vector3.Dot(_directions[0], directionToCompareWith);

            for (var i = 1; i < _directions.Count; i++)
            {
                var dotProduct = Vector3.Dot(_directions[i], directionToCompareWith);

                if (!(dotProduct > closestDotProduct)) continue;
                closestDirection = _directions[i];
                closestDotProduct = dotProduct;
            }

            return closestDirection;
        }
    }
}
