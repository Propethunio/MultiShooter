using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace HEAVYART.TopDownShooter.Netcode {
    public class RigidbodyCharacterController : NetworkBehaviour {

        private Vector3 playerScale;
        public Vector3 PlayerScale { get { return playerScale; } }
        private RaycastHit slopeHit;
        [Tooltip("Distance from the bottom of the player to detect ground"), SerializeField, Min(0)] private float groundCheckDistance;
        private float maxSlopeAngle = 35f;
        [Min(0.01f)]
        [Tooltip("Max speed the player can reach. Velocity is clamped by this value.")] public float maxSpeedAllowed = 40;
        public Transform orientation;
        [HideInInspector] public bool grounded { get; private set; }
        public LayerMask whatIsGround;
        private PlayerBehaviour playerBehaviour;
        private ShooterInputControls inputActions;
        private bool crouching;
        private bool initalized;
        private bool jumping;
        private bool hasJumped;
        [Tooltip("Capacity to gain speed."), SerializeField]
        private float acceleration = 4500;
        [Tooltip("Slide Friction Amount."), SerializeField]
        private float slideCounterMovement = 0.2f;
        private readonly float threshold = 0.01f;
        [Tooltip("Counter movement."), SerializeField]
        private float frictionForceAmount = 0.175f;
        [Tooltip("Maximum allowed speed.")]
        public float currentSpeed = 20;
        float angle;
        private bool cancellingGrounded;
        private Vector3 normalVector = Vector3.up;
        private float coyoteTimer;
        [Range(0, .3f), Tooltip("Coyote jump allows users to perform more satisfactory and responsive jumps, especially when jumping off surfaces")] public float coyoteJumpTime;
        public bool canCoyote;
        private bool readyToJump = true;
        public bool ReadyToJump { get { return readyToJump; } }
        [Range(0, 1), SerializeField]
        private float controlAirborne = .5f;
        [HideInInspector] public bool isCrouching;
        [Min(0.01f)]
        public float runSpeed, walkSpeed, crouchSpeed, crouchTransitionSpeed;
        private bool allowMoveWhileSliding;
        private float x;
        private float y;
        private Rigidbody rb;

        void Awake() {
            playerBehaviour = GetComponent<PlayerBehaviour>();
            rb = GetComponent<Rigidbody>();
            rb.freezeRotation = true;
            rb.useGravity = false;
        }

        private void Start() {
            playerScale = transform.localScale;
            if(IsOwner == false) rb.isKinematic = true;
        }

        private void Update() {
            if(!initalized) return;
            CheckGroundedWithRaycast();
            crouching = inputActions.Player.Crouching.IsPressed();
            jumping = inputActions.Player.Jumping.WasPressedThisFrame();
            Vector2 moveInput = inputActions.Player.Movement.ReadValue<Vector2>();
            x = moveInput.x;
            y = moveInput.y;
        }

        public void Init() {
            inputActions = playerBehaviour.inputActions;
            initalized = true;
        }

        public void Move(bool move) {
            Debug.Log(grounded);
            if(!IsPlayerOnSlope() || (IsPlayerOnSlope() && rb.linearVelocity.y < 0)) rb.AddForce(Vector3.down * 30.19f, ForceMode.Acceleration);

            if(rb.linearVelocity.magnitude > maxSpeedAllowed) rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, maxSpeedAllowed);

            if(!IsPlayerOnSlope() || (IsPlayerOnSlope() && rb.linearVelocity.y < 0)) rb.AddForce(Vector3.down * Time.deltaTime * 10);

            Vector2 relativeVelocity = FindVelRelativeToLook();
            float xRelativeVelocity = relativeVelocity.x, yRelativeVelocity = relativeVelocity.y;

            //Counteract sliding and sloppy movement
            FrictionForce(x, y, relativeVelocity);

            //If speed is larger than maxspeed, cancel out the input so you don't go over max speed
            if(x > 0 && xRelativeVelocity > currentSpeed || x < 0 && xRelativeVelocity < -currentSpeed) x = 0;
            if(y > 0 && yRelativeVelocity > currentSpeed || y < 0 && yRelativeVelocity < -currentSpeed) y = 0;

            float multiplier = (!grounded) ? controlAirborne : 1;
            float multiplierV = (!grounded) ? controlAirborne : 1;

            float multiplier2 = /*(weaponController.weapon != null) ? weaponController.weapon.weightMultiplier :*/ 1;

            if(rb.linearVelocity.sqrMagnitude < .02f) rb.linearVelocity = Vector3.zero;

            if(!move) {
                if(grounded) rb.linearVelocity = Vector3.zero;
                return;
            }
            if(isCrouching && rb.linearVelocity.magnitude >= crouchSpeed && !allowMoveWhileSliding) return;

            if(IsPlayerOnSlope()) {
                rb.AddForce(GetSlopeDirection() * acceleration * Time.deltaTime * multiplier / multiplier2);

                rb.useGravity = false;

                if(rb.linearVelocity.y > 0) rb.AddForce(Vector3.down * 70, ForceMode.Force);
            } else {


                rb.AddForce((orientation.transform.forward * y * acceleration * Time.deltaTime * multiplier * multiplierV / multiplier2), ForceMode.Force);
                rb.AddForce((orientation.transform.right * x * acceleration * Time.deltaTime * multiplier / multiplier2), ForceMode.Force);

                Debug.Log(rb.linearVelocity);
            }
        }

        private bool IsPlayerOnSlope() {
            if(Physics.Raycast(transform.position, Vector3.down, out slopeHit, playerScale.y + groundCheckDistance)) {
                float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
                return angle < maxSlopeAngle && angle != 0;
            }
            return false;
        }

        public Vector2 FindVelRelativeToLook() {
            float lookAngle = orientation.transform.eulerAngles.y;
            Vector3 velocity = rb.linearVelocity;

            // Convert velocity to local space relative to the player's look direction
            Vector3 localVel = Quaternion.Euler(0, -lookAngle, 0) * velocity;

            return new Vector2(localVel.x, localVel.z);
        }

        private void FrictionForce(float x, float y, Vector2 mag) {
            // Prevent from adding friction on an airborne body
            if(!grounded || jumping || hasJumped) return;

            //Slow down sliding + prevent from infinite sliding
            if(crouching) {
                rb.AddForce(acceleration * Time.deltaTime * -rb.linearVelocity.normalized * slideCounterMovement);
                return;
            }
            // Counter movement ( Friction while moving )
            // Prevent from sliding not on purpose

            if(Math.Abs(mag.x) > threshold && Math.Abs(x) < 0.05f || (mag.x < -threshold && x > 0) || (mag.x > threshold && x < 0)) {
                rb.AddForce(acceleration * orientation.transform.right * Time.deltaTime * -mag.x * frictionForceAmount);
            }
            if(Math.Abs(mag.y) > threshold && Math.Abs(y) < 0.05f || (mag.y < -threshold && y > 0) || (mag.y > threshold && y < 0)) {
                rb.AddForce(acceleration * orientation.transform.forward * Time.deltaTime * -mag.y * frictionForceAmount);
            }

            // Limit diagonal running speed without causing a full stop on landing
            Vector3 flatVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            if(flatVelocity.magnitude > currentSpeed) {
                Vector3 limitedVelocity = flatVelocity.normalized * currentSpeed;
                rb.linearVelocity = new Vector3(limitedVelocity.x, rb.linearVelocity.y, limitedVelocity.z);
            }
        }

        private void CheckGroundedWithRaycast() {
            Vector3 origin = transform.position + Vector3.up * .1f;
            bool wasGrounded = grounded; // Store the previous grounded state

            if(Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundCheckDistance, whatIsGround)) {
                Vector3 normal = hit.normal;
                // Check if the surface is considered as floor based on its normal
                if(IsFloor(normal)) {
                    if(!wasGrounded) {
                        // Trigger landing logic
                        //SoundManager.Instance.PlaySound(sounds.landSFX, 0, 0, false, 0);
                        //events.OnLand.Invoke(); // We have just landed
                        //jumpCount = maxJumps; // Reset jumps left
                        hasJumped = false;
                    }
                    grounded = true;
                    cancellingGrounded = false;
                    normalVector = normal;
                    CancelInvoke(nameof(StopGrounded));
                }
            } else {
                if(grounded || !cancellingGrounded) {
                    // Start the process to unground if not already started
                    cancellingGrounded = true;
                    Invoke(nameof(StopGrounded), Time.deltaTime * 3f); // Delay to allow for edge cases like jumping off platforms
                }
            }
        }

        private void StopGrounded() {
            grounded = false;
            coyoteTimer = coyoteJumpTime;
        }

        private bool IsFloor(Vector3 v) {
            angle = Vector3.Angle(Vector3.up, v);
            return angle < maxSlopeAngle;
        }

        public void HandleCoyoteJump() {
            if(grounded) coyoteTimer = 0;
            else coyoteTimer -= Time.deltaTime;

            if(hasJumped) return;
            canCoyote = coyoteTimer > 0 && readyToJump;
        }

        private void ResetJump() => readyToJump = true;

        private Vector3 GetSlopeDirection() {
            return Vector3.ProjectOnPlane(orientation.forward * y + orientation.right * x, slopeHit.normal).normalized;
        }

        public void Stop() {
            rb.isKinematic = true;
        }
    }
}
