using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace HEAVYART.TopDownShooter.Netcode
{
    public class DropController : NetworkBehaviour
    {
        private List<PickUpItemController> _dropElements = new();
        private float _dropChance;

        private CharacterIdentityControl _identityControl;

        private void Start()
        {
            _identityControl = GetComponent<CharacterIdentityControl>();

            if (_identityControl.IsPlayer)
            {
                var modelIndex = _identityControl.spawnParameters.Value.ModelIndex;
                _dropElements = SettingsManager.Instance.player.configs[modelIndex].dropElements;
                _dropChance = SettingsManager.Instance.player.configs[modelIndex].dropChance;
            }
            else
            {
                var modelIndex = _identityControl.spawnParameters.Value.ModelIndex;
                _dropElements = SettingsManager.Instance.ai.configs[modelIndex].dropElements;
                _dropChance = SettingsManager.Instance.ai.configs[modelIndex].dropChance;
            }
        }

        public void Drop()
        {
            if (!IsServer) return;
            if (!(_dropChance > Random.Range(0, 100))) return;
            var elementID = Random.Range(0, _dropElements.Count);
            var offset = Vector3.up * 0.5f;

            var dropGameObject = Instantiate(_dropElements[elementID].gameObject, transform.position + offset, Quaternion.identity);
            dropGameObject.GetComponent<NetworkObject>().Spawn(true);
        }
    }
}
