using Unity.Netcode;
using UnityEngine;

namespace HEAVYART.TopDownShooter.Netcode {
    public class PlayerBehaviour : NetworkBehaviour {
        private HealthController _healthController;
        private PlayerMovement _rigidbodyCharacterController;
        private CharacterIdentityControl _identityControl;
        private Camera _mainCamera;

        private Vector3 _currentMovementInput;

        private void Awake() {
            GameManager.Instance.userControl.AddPlayerObject(NetworkObject);
        }

        private void Start() {
            _healthController = GetComponent<HealthController>();
            GetComponent<ModifiersControlSystem>();
            _rigidbodyCharacterController = GetComponent<PlayerMovement>();
            _identityControl = GetComponent<CharacterIdentityControl>();

            var modelIndex = _identityControl.spawnParameters.Value.ModelIndex;
            var config = SettingsManager.Instance.player.configs[modelIndex];

            _healthController.Initialize(config.health);
            _healthController.OnDeath += () => {
                GetComponent<CharacterEffectsController>().RunDestroyScenario(true);

                if(IsOwner) GameManager.Instance.UI.ShowEndOfGamePopup();
            };

            gameObject.name = "Player: " + _identityControl.spawnParameters.Value.Name;
            _rigidbodyCharacterController.Init();
        }
    }
}