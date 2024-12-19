using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.GraphicsBuffer;
using UnityEngine.Events;
using cowsins;

namespace HEAVYART.TopDownShooter.Netcode {
    public class PlayerMovement : NetworkBehaviour {

        [System.Serializable]
        public class Events // Store your events
{
            public UnityEvent OnMove, OnJump, OnLand, OnCrouch, OnStopCrouch, OnSprint, OnSpawn, OnSlide, OnStartWallRun, OnStopWallRun,
                                OnWallBounce, OnStartDash, OnDashing, OnEndDash, OnStartClimb, OnClimbing, OnEndClimb,
                                OnStartGrapple, OnGrappling, OnStopGrapple, OnGrappleEnabled;
        }

        public Events events;

        public enum DirectionalJumpMethod // Different methods to determine the jump method to apply
{
            None, InputBased, ForwardMovement
        }

        [HideInInspector] public WeaponController weaponController;

        public float aimingSensitivityMultiplier = .5f;

        private Vector3 playerScale;
        public Vector3 PlayerScale { get { return playerScale; } }
        private RaycastHit slopeHit;
        [Tooltip("Distance from the bottom of the player to detect ground"), SerializeField, Min(0)] private float groundCheckDistance;
        private float maxSlopeAngle = 55f;
        [Min(0.01f)]
        [Tooltip("Max speed the player can reach. Velocity is clamped by this value.")] public float maxSpeedAllowed = 40;
        public Transform orientation;
        [Tooltip("This is where the parent of your camera should be attached.")]
        public Transform playerCam;
        [HideInInspector] public bool grounded { get; private set; }
        public LayerMask whatIsGround;
        private PlayerBehaviour playerBehaviour;
        [HideInInspector] public ShooterInputControls inputActions;
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
        [HideInInspector] public float x;
        [HideInInspector] public float y;
        private Rigidbody rb;
        [Tooltip("Default field of view of your camera"), Range(1, 179)] public float normalFOV;
        [HideInInspector] public float mousex;
        [HideInInspector] public float mousey;
        float sensitivity_x = 4f;
        float sensitivity_y = 4f;
        private float desiredX;
        private float xRotation;
        [Tooltip("Maximum Vertical Angle for the camera"), Range(20, 89f)]
        public float maxCameraAngle = 89f;
        [Tooltip("The higher this value is, the higher you will get to jump."), SerializeField]
        private float jumpForce = 550f;
        [Tooltip("Method to apply on jumping when the player is not grounded, related to the directional jump")]
        public DirectionalJumpMethod directionalJumpMethod;
        [Tooltip("Force applied on an object in the direction of the directional jump"), SerializeField]
        private float directionalJumpForce;
        [Tooltip("Interval between jumping")][Min(.25f), SerializeField] private float jumpCooldown = .25f;
        public float RoofCheckDistance { get { return roofCheckDistance; } }
        [SerializeField, Tooltip("Distance to detect a roof. If an obstacle is detected within this distance, the player will not be able to uncrouch")] private float roofCheckDistance = 3.5f;
        [Tooltip("Fade Speed - Start Transition for the field of view")] public float fadeInFOVAmount;








        void Awake() {
            playerBehaviour = GetComponent<PlayerBehaviour>();
            rb = GetComponent<Rigidbody>();
            rb.freezeRotation = true;
            rb.useGravity = false;
            inputActions = new ShooterInputControls();
            inputActions.Player.Enable();
            weaponController = GetComponent<WeaponController>();
        }

        private void Start() {
            playerScale = transform.localScale;
            if(IsOwner == false) rb.isKinematic = true;
        }

        private void Update() {
            if(!initalized || GameManager.Instance.gameState != GameState.ActiveGame) return;
            CheckGroundedWithRaycast();
            crouching = inputActions.Player.Crouching.IsPressed();
            jumping = inputActions.Player.Jumping.WasPressedThisFrame();
            Vector2 moveInput = inputActions.Player.Movement.ReadValue<Vector2>();
            x = moveInput.x;
            y = moveInput.y;
            mousex = Mouse.current.delta.x.ReadValue();
            mousey = Mouse.current.delta.y.ReadValue();
        }

        public void Init() {
            initalized = true;
        }

        public void Movement(bool move) {

            if(!IsPlayerOnSlope()) rb.AddForce(Vector3.down * 30.19f, ForceMode.Acceleration);

            if(rb.linearVelocity.magnitude > maxSpeedAllowed) rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, maxSpeedAllowed);

            if(!IsPlayerOnSlope()) rb.AddForce(Vector3.down * Time.deltaTime * 10);

            Vector2 relativeVelocity = FindVelRelativeToLook();
            float xRelativeVelocity = relativeVelocity.x, yRelativeVelocity = relativeVelocity.y;

            //Counteract sliding and sloppy movement
            FrictionForce(x, y, relativeVelocity);

            //If speed is larger than maxspeed, cancel out the input so you don't go over max speed
            if(x > 0 && xRelativeVelocity > currentSpeed || x < 0 && xRelativeVelocity < -currentSpeed) x = 0;
            if(y > 0 && yRelativeVelocity > currentSpeed || y < 0 && yRelativeVelocity < -currentSpeed) y = 0;

            float multiplier = (!grounded) ? controlAirborne : 1;
            float multiplierV = (!grounded) ? controlAirborne : 1;

            float multiplier2 = (weaponController.weapon != null) ? weaponController.weapon.weightMultiplier : 1;

            if(rb.linearVelocity.sqrMagnitude < .02f) rb.linearVelocity = Vector3.zero;

            if(!move) {
                if(grounded) rb.linearVelocity = Vector3.zero;
                return;
            }
            if(isCrouching && rb.linearVelocity.magnitude >= crouchSpeed && !allowMoveWhileSliding) return;

            if(IsPlayerOnSlope()) {
                rb.AddForce(GetSlopeDirection() * acceleration * Time.deltaTime * multiplier / multiplier2);

                rb.useGravity = false;

                if(rb.linearVelocity.y > 0) rb.AddForce(Vector3.down * 20, ForceMode.Force);
            } else {
                rb.AddForce((orientation.transform.forward * y * acceleration * Time.deltaTime * multiplier * multiplierV / multiplier2), ForceMode.Force);
                rb.AddForce((orientation.transform.right * x * acceleration * Time.deltaTime * multiplier / multiplier2), ForceMode.Force);
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

        public void Jump() {
            readyToJump = false;
            hasJumped = true;
            cancellingGrounded = false;

            //Add jump forces

            rb.AddForce(Vector3.up * jumpForce * 1.5f, ForceMode.Impulse);
            rb.AddForce(normalVector * jumpForce * 0.5f, ForceMode.Impulse);
            // Handle directional jumping
            if(!grounded && directionalJumpMethod != DirectionalJumpMethod.None) {
                if(Vector3.Dot(rb.linearVelocity, new Vector3(x, 0, y)) > .5f)
                    rb.linearVelocity = rb.linearVelocity / 2;
                if(directionalJumpMethod == DirectionalJumpMethod.InputBased) // Input based method for directional jumping
                {
                    rb.AddForce(orientation.right * x * directionalJumpForce, ForceMode.Impulse);
                    rb.AddForce(orientation.forward * y * directionalJumpForce, ForceMode.Impulse);
                }
                if(directionalJumpMethod == DirectionalJumpMethod.ForwardMovement) // Forward Movement method for directional jumping, dependant on orientation
                    rb.AddForce(orientation.forward * Mathf.Abs(y) * directionalJumpForce, ForceMode.VelocityChange);

                //If jumping while falling, reset y velocity.
                if(rb.linearVelocity.y < 0.5f)
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                else if(rb.linearVelocity.y > 0)
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x, rb.linearVelocity.y / 2, rb.linearVelocity.z);
            }

            //SoundManager.Instance.PlaySound(sounds.jumpSFX, 0, 0, false, 0);
            Invoke(nameof(ResetJump), jumpCooldown);
        }

        private void ResetJump() => readyToJump = true;

        private Vector3 GetSlopeDirection() {
            return Vector3.ProjectOnPlane(orientation.forward * y + orientation.right * x, slopeHit.normal).normalized;
        }

        public void Look() {

            float sensM = (weaponController.isAiming) ? aimingSensitivityMultiplier : 1;

            //Handle the camera movement and look based on the inputs received by the user
            float mouseX = (mousex * sensitivity_x * Time.fixedDeltaTime);// * sensM;
            float mouseY = (mousey * sensitivity_y * Time.fixedDeltaTime);// * sensM;

            //Find current look rotation
            Vector3 rot = playerCam.transform.localRotation.eulerAngles;
            desiredX = rot.y + mouseX;

            //Rotate, and also make sure we dont over- or under-rotate.
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -maxCameraAngle, maxCameraAngle);

            //Perform the rotations on: 
            playerCam.transform.localRotation = Quaternion.Euler(xRotation, desiredX, 0); // the camera parent
            orientation.transform.localRotation = Quaternion.Euler(0, desiredX, 0); // the orientation
        }

        public void Stop() {
            rb.isKinematic = true;
        }
    }
}
