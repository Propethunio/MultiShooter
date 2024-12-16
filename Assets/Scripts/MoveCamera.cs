namespace cowsins {
    using Unity.Netcode;
    using UnityEngine;


    public class MoveCamera : NetworkBehaviour {

        [SerializeField] Transform playerModel;

        [Tooltip("Reference to our head = height of the camera"), SerializeField]
        private Transform head;
        [SerializeField] Transform deathHead;

        private void Start() {
            if(IsOwner) {
                gameObject.SetActive(true);
            } else {
                gameObject.SetActive(false);
            }
        }

        private void Update() {
            transform.position = head.transform.position - new Vector3(0f, -0.1f, 0f);
            playerModel.transform.localRotation = Quaternion.Euler(transform.localEulerAngles);
        }

        public void ChangeToDeath() {
            head = deathHead;
        }
    }
}