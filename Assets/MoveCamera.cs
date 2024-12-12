namespace cowsins {
    using Unity.Netcode;
    using UnityEngine;


    public class MoveCamera : NetworkBehaviour {

        [SerializeField] Transform playerModel;

        [Tooltip("Reference to our head = height of the camera"), SerializeField]
        private Transform head;

        private void Start() {
            Debug.Log(IsOwner);
            if(IsOwner) {
                gameObject.SetActive(true);
            } else {
                gameObject.SetActive(false);
            }
        }

        private void Update() {
            transform.position = head.transform.position;
            playerModel.transform.localRotation = Quaternion.Euler(0, transform.localEulerAngles.y, 0);
        }
    }
}