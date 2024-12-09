using UnityEngine;

public class BoneAngleAdjuster : MonoBehaviour {

    public Vector3 offset;

    void LateUpdate() {
        if(offset != Vector3.zero)
            transform.localRotation *= Quaternion.Euler(offset);
    }
}