using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ModelController : NetworkBehaviour {

    [SerializeField] SkinnedMeshRenderer skinnedMeshRenderer;
    [SerializeField] List<MeshRenderer> meshRenderersList;
    [SerializeField] List<BoxCollider> modelColliders;

    void Start() {
        if(IsOwner) {
            skinnedMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            foreach(var renderer in meshRenderersList) {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
            foreach(var collider in modelColliders) {
                collider.enabled = false;
            }
        } else {
            gameObject.SetActive(true);
        }
    }
}