using System.Collections;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public class GameplaySettings : MonoBehaviour {

    public string defaultGameSceneName;

    [Space]
    public float delayBeforeCountdown;
    public float countdownTime;
    public double gameDuration;
    public int botsCount;
    public float botsSpawnRate;

    private IEnumerator Start() {
        yield return new WaitUntil(() => UnityServices.State == ServicesInitializationState.Initialized);
        yield return new WaitUntil(() => AuthenticationService.Instance.IsSignedIn == true);

        if(PlayerDataKeeper.selectedScene == "none")
            PlayerDataKeeper.selectedScene = defaultGameSceneName;
    }
}