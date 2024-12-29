using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using cowsins;
using UnityEngine.Serialization;

namespace HEAVYART.TopDownShooter.Netcode {
    public class PlayerMovement : NetworkBehaviour {
        [Tooltip("Play speed of the footsteps."), SerializeField, Range(.1f, .95f)] private float footstepSpeed;
        [SerializeField] private SoundMenago soundMenago;
        [FormerlySerializedAs("jumpSFX")] [SerializeField] private AudioClip jumpSfx;
        [FormerlySerializedAs("landSFX")] [SerializeField] private AudioClip landSfx;
        [FormerlySerializedAs("stepSFX")] [SerializeField] private AudioClip[] stepSfx;

        [Serializable]
        public class Events
        {
                [FormerlySerializedAs("OnMove")] public UnityEvent onMove;
                [FormerlySerializedAs("OnJump")] public UnityEvent onJump;
        }

        public Events events;

        public enum DirectionalJumpMethod
{
            None, InputBased, ForwardMovement
        }

        [HideInInspector] public WeaponController weaponController;
        private float _stepTimer;
        public float aimingSensitivityMultiplier = .5f;

        private Vector3 _playerScale;
        private RaycastHit _slopeHit;
        [Tooltip("Distance from the bottom of the player to detect ground"), SerializeField, Min(0)] private float groundCheckDistance;
        private const float MaxSlopeAngle = 55f;

        [Min(0.01f)]
        [Tooltip("Max speed the player can reach. Velocity is clamped by this value.")] public float maxSpeedAllowed = 40;
        public Transform orientation;
        [Tooltip("This is where the parent of your camera should be attached.")]
        public Transform playerCam;

        public bool Grounded { get; private set; }
        public LayerMask whatIsGround;
        public ShooterInputControls InputActions;
        private bool _crouching;
        private bool _initalized;
        private bool _jumping;
        private bool _hasJumped;
        [Tooltip("Capacity to gain speed."), SerializeField]
        private float acceleration = 4500;
        [Tooltip("Slide Friction Amount."), SerializeField]
        private float slideCounterMovement = 0.2f;
        private const float Threshold = 0.01f;
        [Tooltip("Counter movement."), SerializeField]
        private float frictionForceAmount = 0.175f;
        [Tooltip("Maximum allowed speed.")]
        public float currentSpeed = 20;

        private float _angle;
        private bool _cancellingGrounded;
        private Vector3 _normalVector = Vector3.up;
        private float _coyoteTimer;
        [Range(0, .3f), Tooltip("Coyote jump allows users to perform more satisfactory and responsive jumps, especially when jumping off surfaces")] public float coyoteJumpTime;
        public bool canCoyote;
        public bool ReadyToJump { get; private set; } = true;

        [Range(0, 1), SerializeField]
        private float controlAirborne = .5f;
        [HideInInspector] public bool isCrouching;
        [Min(0.01f)]
        public float runSpeed, walkSpeed, crouchSpeed, crouchTransitionSpeed;
        private bool _allowMoveWhileSliding;
        [HideInInspector] public float x;
        [HideInInspector] public float y;
        private Rigidbody _rb;
        [Tooltip("Default field of view of your camera"), Range(1, 179)] public float normalFOV;
        [HideInInspector] public float mousex;
        [HideInInspector] public float mousey;
        private const float SensitivityX = 4f;
        private const float SensitivityY = 4f;
        private float _desiredX;
        private float _xRotation;
        [Tooltip("Maximum Vertical Angle for the camera"), Range(20, 89f)]
        public float maxCameraAngle = 89f;
        [Tooltip("The higher this value is, the higher you will get to jump."), SerializeField]
        private float jumpForce = 550f;
        [Tooltip("Method to apply on jumping when the player is not grounded, related to the directional jump")]
        public DirectionalJumpMethod directionalJumpMethod;
        [Tooltip("Force applied on an object in the direction of the directional jump"), SerializeField]
        private float directionalJumpForce;
        [Tooltip("Interval between jumping")][Min(.25f), SerializeField] private float jumpCooldown = .25f;
        [SerializeField, Tooltip("Distance to detect a roof. If an obstacle is detected within this distance, the player will not be able to uncrouch")] private float roofCheckDistance = 3.5f;
        [Tooltip("Fade Speed - Start Transition for the field of view")] public float fadeInFOVAmount;

        private void Awake() {
            GetComponent<PlayerBehaviour>();
            _rb = GetComponent<Rigidbody>();
            _rb.freezeRotation = true;
            _rb.useGravity = false;
            InputActions = new ShooterInputControls();
            InputActions.Player.Enable();
            weaponController = GetComponent<WeaponController>();
        }

        private void Start() {
            _playerScale = transform.localScale;
        }

        private void Update() {
            if(!IsOwner) return;
            if(!_initalized || GameManager.Instance.gameState != GameState.ActiveGame) return;
            CheckGroundedWithRaycast();
            _crouching = InputActions.Player.Crouching.IsPressed();
            _jumping = InputActions.Player.Jumping.WasPressedThisFrame();
            var moveInput = InputActions.Player.Movement.ReadValue<Vector2>();
            x = moveInput.x;
            y = moveInput.y;
            mousex = Mouse.current.delta.x.ReadValue();
            mousey = Mouse.current.delta.y.ReadValue();
        }

        public void Init() {
            _initalized = true;
        }

        public void Movement(bool move) {

            if(!IsPlayerOnSlope()) _rb.AddForce(Vector3.down * 30.19f, ForceMode.Acceleration);

            if(_rb.linearVelocity.magnitude > maxSpeedAllowed) _rb.linearVelocity = Vector3.ClampMagnitude(_rb.linearVelocity, maxSpeedAllowed);

            if(!IsPlayerOnSlope()) _rb.AddForce(Vector3.down * Time.deltaTime * 10);

            var relativeVelocity = FindVelRelativeToLook();
            float xRelativeVelocity = relativeVelocity.x, yRelativeVelocity = relativeVelocity.y;

            FrictionForce(x, y, relativeVelocity);

            if(x > 0 && xRelativeVelocity > currentSpeed || x < 0 && xRelativeVelocity < -currentSpeed) x = 0;
            if(y > 0 && yRelativeVelocity > currentSpeed || y < 0 && yRelativeVelocity < -currentSpeed) y = 0;

            var multiplier = (!Grounded) ? controlAirborne : 1;
            var multiplierV = (!Grounded) ? controlAirborne : 1;

            var multiplier2 = (weaponController.weapon != null) ? weaponController.weapon.weightMultiplier : 1;

            if(_rb.linearVelocity.sqrMagnitude < .02f) _rb.linearVelocity = Vector3.zero;

            if(!move) {
                if(Grounded) _rb.linearVelocity = Vector3.zero;
                return;
            }
            if(isCrouching && _rb.linearVelocity.magnitude >= crouchSpeed && !_allowMoveWhileSliding) return;

            if(IsPlayerOnSlope()) {
                _rb.AddForce(GetSlopeDirection() * acceleration * Time.deltaTime * multiplier / multiplier2);

                _rb.useGravity = false;

                if(_rb.linearVelocity.y > 0) _rb.AddForce(Vector3.down * 20, ForceMode.Force);
            } else {
                _rb.AddForce((orientation.transform.forward * y * acceleration * Time.deltaTime * multiplier * multiplierV / multiplier2), ForceMode.Force);
                _rb.AddForce((orientation.transform.right * x * acceleration * Time.deltaTime * multiplier / multiplier2), ForceMode.Force);
            }
        }

        private bool IsPlayerOnSlope() {
            if (!Physics.Raycast(transform.position, Vector3.down, out _slopeHit,
                    _playerScale.y + groundCheckDistance)) return false;
            var angle = Vector3.Angle(Vector3.up, _slopeHit.normal);
            return angle < MaxSlopeAngle && angle != 0;
        }

        private Vector2 FindVelRelativeToLook() {
            var lookAngle = orientation.transform.eulerAngles.y;
            var velocity = _rb.linearVelocity;

            var localVel = Quaternion.Euler(0, -lookAngle, 0) * velocity;
            return new Vector2(localVel.x, localVel.z);
        }

        private void FrictionForce(float x, float y, Vector2 mag) {
            if(!Grounded || _jumping || _hasJumped) return;

            if(_crouching) {
                _rb.AddForce(acceleration * Time.deltaTime * -_rb.linearVelocity.normalized * slideCounterMovement);
                return;
            }

            if(Math.Abs(mag.x) > Threshold && Math.Abs(x) < 0.05f || (mag.x < -Threshold && x > 0) || (mag.x > Threshold && x < 0)) {
                _rb.AddForce(acceleration * orientation.transform.right * Time.deltaTime * -mag.x * frictionForceAmount);
            }
            if(Math.Abs(mag.y) > Threshold && Math.Abs(y) < 0.05f || (mag.y < -Threshold && y > 0) || (mag.y > Threshold && y < 0)) {
                _rb.AddForce(acceleration * orientation.transform.forward * Time.deltaTime * -mag.y * frictionForceAmount);
            }

            var flatVelocity = new Vector3(_rb.linearVelocity.x, 0, _rb.linearVelocity.z);
            if (!(flatVelocity.magnitude > currentSpeed)) return;
            var limitedVelocity = flatVelocity.normalized * currentSpeed;
            _rb.linearVelocity = new Vector3(limitedVelocity.x, _rb.linearVelocity.y, limitedVelocity.z);
        }

        private void CheckGroundedWithRaycast() {
            var origin = transform.position + Vector3.up * .1f;
            var wasGrounded = Grounded;

            if(Physics.Raycast(origin, Vector3.down, out var hit, groundCheckDistance, whatIsGround)) {
                var normal = hit.normal;
                if (!IsFloor(normal)) return;
                if(!wasGrounded) {
                    soundMenago.PlaySound(landSfx, 0, 0, false, 1, transform.position);
                    _hasJumped = false;
                }
                Grounded = true;
                _cancellingGrounded = false;
                _normalVector = normal;
                CancelInvoke(nameof(StopGrounded));
            } else
            {
                if (!Grounded && _cancellingGrounded) return;
                _cancellingGrounded = true;
                Invoke(nameof(StopGrounded), Time.deltaTime * 3f);
            }
        }

        private void StopGrounded() {
            Grounded = false;
            _coyoteTimer = coyoteJumpTime;
        }

        private bool IsFloor(Vector3 v) {
            _angle = Vector3.Angle(Vector3.up, v);
            return _angle < MaxSlopeAngle;
        }

        public void HandleCoyoteJump() {
            if(Grounded) _coyoteTimer = 0;
            else _coyoteTimer -= Time.deltaTime;

            if(_hasJumped) return;
            canCoyote = _coyoteTimer > 0 && ReadyToJump;
        }

        public void Jump() {
            ReadyToJump = false;
            _hasJumped = true;
            _cancellingGrounded = false;

            _rb.AddForce(Vector3.up * jumpForce * 1.5f, ForceMode.Impulse);
            _rb.AddForce(_normalVector * jumpForce * 0.5f, ForceMode.Impulse);
            if(!Grounded && directionalJumpMethod != DirectionalJumpMethod.None) {
                if(Vector3.Dot(_rb.linearVelocity, new Vector3(x, 0, y)) > .5f)
                    _rb.linearVelocity /= 2;
                switch (directionalJumpMethod)
                {
                    case DirectionalJumpMethod.InputBased:
                        _rb.AddForce(orientation.right * x * directionalJumpForce, ForceMode.Impulse);
                        _rb.AddForce(orientation.forward * y * directionalJumpForce, ForceMode.Impulse);
                        break;
                    case DirectionalJumpMethod.ForwardMovement:
                        _rb.AddForce(orientation.forward * Mathf.Abs(y) * directionalJumpForce, ForceMode.VelocityChange);
                        break;
                    case DirectionalJumpMethod.None:
                    default:
                        break;
                }

                if (_rb.linearVelocity.y < 0.5f)
                    _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0, _rb.linearVelocity.z);
                else if (_rb.linearVelocity.y > 0)
                    _rb.linearVelocity =
                        new Vector3(_rb.linearVelocity.x, _rb.linearVelocity.y / 2, _rb.linearVelocity.z);
            }

            soundMenago.PlaySound(jumpSfx, 0, 0, false, 1, transform.position);
            Invoke(nameof(ResetJump), jumpCooldown);
        }

        private void ResetJump() => ReadyToJump = true;

        private Vector3 GetSlopeDirection() {
            return Vector3.ProjectOnPlane(orientation.forward * y + orientation.right * x, _slopeHit.normal).normalized;
        }

        public void Look() {
            var mouseX = (mousex * SensitivityX * Time.fixedDeltaTime);// * sensM;
            var mouseY = (mousey * SensitivityY * Time.fixedDeltaTime);// * sensM;

            var rot = playerCam.transform.localRotation.eulerAngles;
            _desiredX = rot.y + mouseX;

            _xRotation -= mouseY;
            _xRotation = Mathf.Clamp(_xRotation, -maxCameraAngle, maxCameraAngle);

            playerCam.transform.localRotation = Quaternion.Euler(_xRotation, _desiredX, 0); // the camera parent
            orientation.transform.localRotation = Quaternion.Euler(0, _desiredX, 0); // the orientation
        }

        public void Stop() {
            _rb.isKinematic = true;
        }

        public void FootSteps() {
            if(!Grounded || _rb.linearVelocity.sqrMagnitude <= .1f) {
                _stepTimer = 1 - footstepSpeed;
                return;
            }

            _stepTimer -= Time.deltaTime * _rb.linearVelocity.magnitude / 15;
            if (!(_stepTimer <= 0)) return;
            _stepTimer = 1 - footstepSpeed;

            if (!Physics.Raycast(playerCam.position, Vector3.down, out var hit, 2.7f, whatIsGround)) return;
            switch(LayerMask.LayerToName(hit.transform.gameObject.layer)) {
                case "Ground":
                    soundMenago.PlaySound(stepSfx[UnityEngine.Random.Range(0, stepSfx.Length)], 0, .15f, true, 1, transform.position);
                    break;
            }
        }
    }
}
