using Unity.Netcode;
using UnityEngine;

namespace HEAVYART.TopDownShooter.Netcode
{
    public class CharacterIdentityControl : NetworkBehaviour
    {
        public bool IsPlayer { get; private set; }
        public bool IsBot { get; private set; }

        public new bool IsLocalPlayer => IsPlayer && IsOwner;
        public new bool IsOwner => spawnParameters.Value.OwnerID == NetworkManager.Singleton.LocalClientId;

        [HideInInspector]
        public NetworkVariable<CharacterSpawnParameters> spawnParameters = new();

        private CharacterSpawnParameters _serverBufferedSpawnParameters;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                spawnParameters.Value = _serverBufferedSpawnParameters;
        }

        public void SetSpawnParameters(CharacterSpawnParameters spawnParameter)
        {
            _serverBufferedSpawnParameters = spawnParameter;
        }

        private void Awake()
        {
            IsPlayer = GetComponent<PlayerBehaviour>() != null;
        }
    }
}