using cowsins;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HEAVYART.TopDownShooter.Netcode
{
    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }
        public NetworkObjectsControl userControl { get; private set; }
        public NetworkObjectsSpawner spawnControl { get; private set; }
        public UIController UI { get; private set; }
        public InGameUI gameUI { get; private set; }

        public GameState gameState { get; private set; }

        public Action OnNetworkReady;
        public Action OnGameStart;
        public Action OnGameEnd;
        public Action OnDisconnect;

        public double gameStartTime { get; private set; }
        public double gameEndTime { get; private set; }

        private HashSet<ulong> processedDeaths = new HashSet<ulong>();

        public Dictionary<ulong, LeaderboardUserProfile> leaderboard = new Dictionary<ulong, LeaderboardUserProfile>();

        private void Awake()
        {
            Instance = this;

            userControl = GetComponent<NetworkObjectsControl>();
            spawnControl = GetComponent<NetworkObjectsSpawner>();
            UI = FindFirstObjectByType<UIController>();
            gameUI = FindAnyObjectByType<InGameUI>();

            gameState = GameState.WaitingForPlayers;

            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectCallback;
            NetworkManager.Singleton.GetComponent<UnityTransport>().OnTransportEvent += OnTransportEvent;

            SceneLoadManager.Instance.UnsubscribeNetworkSceneUpdates();

            StartCoroutine(WaitForNetworkReady());

            //Run Localhost (Offline mode)
            if (LobbyManager.Instance.isOfflineMode == true)
                NetworkManager.Singleton.StartHost();
        }

        IEnumerator WaitForNetworkReady()
        {
            //OnNetworkSpawn is not working if gameObject was placed on scene instead of Instantiate
            //Here is the way to avoid this problem
            while (IsSpawned == false) yield return 0;

            OnNetworkReady?.Invoke();
        }

        private new void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectCallback;
                NetworkManager.Singleton.GetComponent<UnityTransport>().OnTransportEvent -= OnTransportEvent;
            }
        }

        [Rpc(SendTo.Everyone)]
        void HideLoadingRpc() {
            gameUI.HideLoading();
        }

        void FixedUpdate()
        {
            //Handle waiting for players
            if (gameState == GameState.WaitingForPlayers)
            {
                int connectedPlayersCount = userControl.playerSceneObjects.Count;
                int expectedPlayersCount = LobbyManager.Instance.players.Count;

                //Room is full
                if (connectedPlayersCount == expectedPlayersCount)
                {
                    HideLoadingRpc();

                    float networkDelay = 0.5f;

                    double startTime =
                        NetworkManager.ServerTime.Time
                        + SettingsManager.Instance.gameplay.delayBeforeCountdown //a little time for user to figure out what's going on around, after scene has been loaded
                        + SettingsManager.Instance.gameplay.countdownTime // 3..2..1..GO!
                        + networkDelay;

                    double endTime = startTime + SettingsManager.Instance.gameplay.gameDuration;

                    //Broadcast start countdown command
                    if (IsServer) StartCountdownRpc(startTime, endTime);
                }
            }

            //Handle countdown
            if (gameState == GameState.WaitingForCountdown)
            {
                if (NetworkManager.ServerTime.Time >= gameStartTime)
                {
                    //Start game
                    gameState = GameState.ActiveGame;
                    OnGameStart?.Invoke();
                }
            }

            //Handle active game
            if (gameState == GameState.ActiveGame)
            {
                if (NetworkManager.ServerTime.Time >= gameEndTime)
                {
                    //End game
                    gameState = GameState.GameIsOver;
                    OnGameEnd?.Invoke();
                }
            }
        }

        [Rpc(SendTo.Everyone)]
        private void StartCountdownRpc(double gameStartTime, double gameEndTime)
        {
            //Receive and apply

            this.gameStartTime = gameStartTime;
            this.gameEndTime = gameEndTime;

            gameState = GameState.WaitingForCountdown;
        }

        [Rpc(SendTo.Everyone)]
        public void RegisterCharacterDeathRPC(ulong killedID, bool byOtherPlayer, ulong killerID = 0) {

            if(processedDeaths.Contains(killedID)) {
                return;
            }

            processedDeaths.Add(killedID);

            if(byOtherPlayer) {
                if(leaderboard.ContainsKey(killerID))
                {
                    leaderboard[killerID].score++;
                }
                AddKillToUI(killedID, true, killerID);
            } else {
                AddKillToUI(killedID, false);
            }

            StartCoroutine(CleanupProcessedDeaths(killedID));
        }

        private IEnumerator CleanupProcessedDeaths(ulong killedID) {
            yield return new WaitForSeconds(.2f);
            processedDeaths.Remove(killedID);
        }

        void AddKillToUI(ulong killedPlayerID, bool byOhter, ulong ohterPlayerID = 0) {
            string killerID = null;
            string killedID = leaderboard[killedPlayerID].userName;

            if(byOhter && leaderboard.ContainsKey(ohterPlayerID)) {
                killerID = leaderboard[ohterPlayerID].userName;
            }

            gameUI.ShowKill(killerID, killedID);
        }

        public void AddLeaderboardUser(LeaderboardUserProfile userProfile)
        {
            if (leaderboard.ContainsKey(userProfile.id) == false)
                leaderboard.Add(userProfile.id, userProfile);
        }

        private void OnClientDisconnectCallback(ulong clientID)
        {
            if (clientID == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log("Local client has been closed");
                OnDisconnect?.Invoke();

                gameState = GameState.GameIsOver;
                OnGameEnd?.Invoke();
            }

            if (clientID == 0)
            {
                Debug.Log("Server has been closed.");
                OnDisconnect?.Invoke();

                gameState = GameState.GameIsOver;
                OnGameEnd?.Invoke();
            }
        }

        public void QuitGame()
        {
            LobbyManager.Instance.QuitLobby();
            NetworkManager.Singleton.Shutdown();

            SceneLoadManager.Instance.LoadRegularScene("MainMenu", true);
        }

        public void RestartCurrentScene()
        {
            LobbyManager.Instance.QuitLobby();
            NetworkManager.Singleton.Shutdown();

            SceneLoadManager.Instance.LoadRegularScene(SceneManager.GetActiveScene().name, false);
        }

        private void OnTransportEvent(NetworkEvent eventType, ulong clientId, ArraySegment<byte> payload, float receiveTime)
        {
            //On connection lost (no internet)
            if (eventType == NetworkEvent.TransportFailure)
            {
                Debug.Log("Disconnected");
                LobbyManager.Instance.QuitLobby();
                SceneLoadManager.Instance.LoadRegularScene("MainMenu", true);
            }
        }

        void OnApplicationPause(bool pauseStatus)
        {
            //On hide application
            //Disconnects immediate
            if (pauseStatus == true) QuitGame();
        }
    }
}
