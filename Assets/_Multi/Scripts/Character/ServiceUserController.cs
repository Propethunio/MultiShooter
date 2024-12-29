using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HEAVYART.TopDownShooter.Netcode
{
    public class ServiceUserController : NetworkBehaviour
    {
        private readonly NetworkVariable<FixedString64Bytes> _synchronizedName = new(writePerm: NetworkVariableWritePermission.Owner);

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
                _synchronizedName.Value = PlayerDataKeeper.name;

            StartCoroutine(RegisterInGameManager());
        }

        private IEnumerator RegisterInGameManager()
        {
            while (GameManager.Instance == null) yield return 0;
            while (_synchronizedName.Value == default) yield return 0;

            GameManager.Instance.userControl.AddUserServiceObject(NetworkObject, _synchronizedName.Value.ToString());

            GameManager.Instance.OnNetworkReady += () =>
            {
                if (IsOwner)
                    StartCoroutine(SpawnPlayerObject());
            };

            gameObject.name = "ServiceUserObject: " + _synchronizedName.Value;

            SceneManager.MoveGameObjectToScene(gameObject, SceneManager.GetActiveScene());
        }

        private IEnumerator SpawnPlayerObject()
        {
            const int delay = 1;
            yield return new WaitForSeconds(delay);

            GameManager.Instance.spawnControl.SpawnPlayerServerRpc(GetLocalPlayerSpawnParameters());
        }

        public CharacterSpawnParameters GetLocalPlayerSpawnParameters()
        {
            return new CharacterSpawnParameters()
            {
                Name = PlayerDataKeeper.name,
                Color = SettingsManager.Instance.player.GetPlayerColor(),
                OwnerID = NetworkManager.Singleton.LocalClientId,
                ModelIndex = PlayerDataKeeper.selectedPrefab
            };
        }
    }
}
