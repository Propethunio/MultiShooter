using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HEAVYART.TopDownShooter.Netcode
{
    public class TargetMarkerController : MonoBehaviour
    {
        public float activityDuration = 0.25f;
        public float fadeDuration = 0.5f;
        public float colorLerpFactor = 0.25f;
        public float aimingDistance = 10;
        public Renderer targetMarker;

        private bool IsLocalPlayer => _identityControl.IsLocalPlayer;

        private CharacterIdentityControl _identityControl;
        private WeaponControlSystem _weaponControlSystem;

        private float _lastActivationTime;

        private Color _targetMarkerDefaultColor;
        private Color _targetMarkerInactiveColor;

        private void Awake()
        {
            _identityControl = GetComponent<CharacterIdentityControl>();
            _weaponControlSystem = GetComponent<WeaponControlSystem>();

            targetMarker.gameObject.SetActive(true);

            _targetMarkerDefaultColor = targetMarker.material.color;
            _targetMarkerInactiveColor = _targetMarkerDefaultColor;
            _targetMarkerInactiveColor.a = 0;
        }

        private void FixedUpdate()
        {
            if (Time.time > _lastActivationTime + activityDuration)
            {
                if (Time.time > _lastActivationTime + activityDuration + fadeDuration)
                    targetMarker.gameObject.SetActive(false);
                else
                    targetMarker.material.color = Color.Lerp(targetMarker.material.color, _targetMarkerInactiveColor, colorLerpFactor);
            }

            if (!Physics.Raycast(_weaponControlSystem.lineOfSightTransform.position,
                    _weaponControlSystem.lineOfSightTransform.forward, out var hit, aimingDistance)) return;
            
            var otherCharacter = hit.transform.GetComponent<TargetMarkerController>();

            if (otherCharacter == null) return;
            if (_identityControl.IsLocalPlayer || otherCharacter.IsLocalPlayer)
                otherCharacter.EnableTargetMarker();
        }

        private void EnableTargetMarker()
        {
            _lastActivationTime = Time.time;

            targetMarker.gameObject.SetActive(true);
            targetMarker.material.color = _targetMarkerDefaultColor;
        }
    }
}
