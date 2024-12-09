using UnityEngine;

public class MainGamePanelUIController : MonoBehaviour {

    public void StartQuickGame() {
        LobbyParameters lobbyParameters = new LobbyParameters();
        lobbyParameters.playersCount = SettingsManager.Instance.lobby.defaultPlayerCount;
        lobbyParameters.version = SettingsManager.Instance.common.projectVersion;

        LobbyManager.Instance.JoinOrCreateLobby(lobbyParameters);
        MainMenuUIManager.Instance.ShowWaitingForPublicGamePopup();
    }
}