using UnityEngine;

public class CommonSettings : MonoBehaviour {

    public int targetFPS = 60;
    public string projectVersion;

    void Start() {
        Application.targetFrameRate = targetFPS;
    }
}