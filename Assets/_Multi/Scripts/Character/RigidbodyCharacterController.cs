using Unity.Netcode;
using UnityEngine;

public class RigidbodyCharacterController : NetworkBehaviour {

    public float gravity = 15.0f;
    public float maxVelocityChange = 10.0f;
    private Rigidbody currentRigidbody;

    void Awake() {
        currentRigidbody = GetComponent<Rigidbody>();
        currentRigidbody.freezeRotation = true;
        currentRigidbody.useGravity = false;
    }

    private void Start() {
        if(IsOwner == false) currentRigidbody.isKinematic = true;
    }

    public void Move(Vector3 direction, float speed) {
        // Calculate how fast we should be moving
        Vector3 targetVelocity = direction;
        targetVelocity = transform.TransformDirection(targetVelocity);
        targetVelocity *= speed;

        // Apply a force that attempts to reach our target velocity
        Vector3 velocity = currentRigidbody.linearVelocity;
        Vector3 velocityChange = (targetVelocity - velocity);
        velocityChange.x = Mathf.Clamp(velocityChange.x, -maxVelocityChange, maxVelocityChange);
        velocityChange.z = Mathf.Clamp(velocityChange.z, -maxVelocityChange, maxVelocityChange);
        velocityChange.y = 0;
        currentRigidbody.AddForce(velocityChange, ForceMode.VelocityChange);


        // We apply gravity manually for more tuning control
        currentRigidbody.AddForce(new Vector3(0, -gravity * currentRigidbody.mass, 0));
    }

    public void Stop() {
        currentRigidbody.isKinematic = true;
    }
}