using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HEAVYART.TopDownShooter.Netcode
{
    //Session user object. While player objects on scene are destroyable, this one is not.
    //Could be used for any commands to communicate with other players, like voting, chat, etc.
    public class ServiceUserController : NetworkBehaviour
    {
        private NetworkVariable<FixedString64Bytes> synchronizedName = new NetworkVariable<FixedString64Bytes>(writePerm: NetworkVariableWritePermission.Owner);

        private void Awake()
        {
            //Keep object safe between main menu and game scene
            DontDestroyOnLoad(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
                synchronizedName.Value = PlayerDataKeeper.name;

            StartCoroutine(RegisterInGameManager());
        }

        private IEnumerator RegisterInGameManager()
        {
            //Wait for game manager (scene could be loading at the moment, so we have to wait)
            while (GameManager.Instance == null) yield return 0;

            //Wait for our synchronized name to initialize (on other clients side)
            while (synchronizedName.Value == default) yield return 0;

            GameManager.Instance.userControl.AddUserServiceObject(NetworkObject, synchronizedName.Value.ToString());

            //Wait for GameManager become ready for network commands
            GameManager.Instance.OnNetworkReady += () =>
            {
                //Spawn player
                if (IsOwner == true)
                    StartCoroutine(SpawnPlayerObject());
            };

            gameObject.name = "ServiceUserObject: " + synchronizedName.Value;

            //Move current gameObject from DontDestroyOnLoad to current scene
            SceneManager.MoveGameObjectToScene(gameObject, SceneManager.GetActiveScene());
        }

        private IEnumerator SpawnPlayerObject()
        {
            //Wait for scene to initialize and spawn player after short delay
            int delay = 1;
            yield return new WaitForSeconds(delay);

            GameManager.Instance.spawnControl.SpawnPlayerServerRpc(GetLocalPlayerSpawnParameters());
        }

        public CharacterSpawnParameters GetLocalPlayerSpawnParameters()
        {
            return new CharacterSpawnParameters()
            {
                name = PlayerDataKeeper.name,
                color = SettingsManager.Instance.player.GetPlayerColor(),
                ownerID = NetworkManager.Singleton.LocalClientId,
                modelIndex = PlayerDataKeeper.selectedPrefab
            };
        }
    }
}
