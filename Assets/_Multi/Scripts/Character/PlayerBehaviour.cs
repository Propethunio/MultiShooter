using Unity.Netcode;
using UnityEngine;

namespace HEAVYART.TopDownShooter.Netcode {
    public class PlayerBehaviour : NetworkBehaviour {

        private PlayerMovement _rigidbodyCharacterController;
        private CharacterIdentityControl _identityControl;

        private void Awake() {
            GameManager.Instance.userControl.AddPlayerObject(NetworkObject);
        }

        private void Start() {
            GetComponent<ModifiersControlSystem>();
            _rigidbodyCharacterController = GetComponent<PlayerMovement>();
            _identityControl = GetComponent<CharacterIdentityControl>();

            var modelIndex = _identityControl.spawnParameters.Value.ModelIndex;
            var config = SettingsManager.Instance.player.configs[modelIndex];

            gameObject.name = "Player: " + _identityControl.spawnParameters.Value.Name;
            _rigidbodyCharacterController.Init();
        }
    }
}