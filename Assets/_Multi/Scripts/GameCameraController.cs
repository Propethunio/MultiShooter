using Unity.Netcode;
using UnityEngine;

public class GameCameraController : MonoBehaviour {

    private Vector3 movementVelocity = Vector3.zero;
    private bool isActivated;

    void FixedUpdate() {
        if(GameManager.Instance.gameState != GameState.ActiveGame || !isActivated) return;

        NetworkObject localPlayer = GameManager.Instance.userControl.localPlayer;

        if(GameManager.Instance.userControl.localPlayer != null) {
            Vector3 offset = SettingsManager.Instance.camera.offset;
            float angle = SettingsManager.Instance.camera.angle;
            float dampTime = SettingsManager.Instance.camera.dampTime;
            transform.position = Vector3.SmoothDamp(transform.position, localPlayer.transform.position + offset, ref movementVelocity, dampTime);
            transform.rotation = Quaternion.Euler(angle, 0, 0);
        }
    }

    public void ActivateCameraMovement() {
        isActivated = true;
    }

    public void StopCameraMovement() {
        isActivated = false;
    }
}