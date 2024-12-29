using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace HEAVYART.TopDownShooter.Netcode {
    public class PlayerBehaviour : NetworkBehaviour {

        private float movementSpeed = 5;
        private float smoothMovementTime = 0.1f;

        private WeaponControlSystem weaponControlSystem;
        [HideInInspector] public ShooterInputControls inputActions;
        private HealthController healthController;
        private ModifiersControlSystem modifiersControlSystem;
        private PlayerMovement rigidbodyCharacterController;
        private CharacterIdentityControl identityControl;

        private Camera mainCamera;
        private Plane plane;

        private Vector3 movementVelocity = Vector3.zero;
        private Vector3 currentMovementInput;

        private void Awake() {
            //Register spawned player object (bots need it to find player)
            GameManager.Instance.userControl.AddPlayerObject(NetworkObject);
        }

        private void Start() {
            //Basic components
            weaponControlSystem = GetComponent<WeaponControlSystem>();
            healthController = GetComponent<HealthController>();
            modifiersControlSystem = GetComponent<ModifiersControlSystem>();
            rigidbodyCharacterController = GetComponent<PlayerMovement>();
            identityControl = GetComponent<CharacterIdentityControl>();

            //Camera and aiming
            plane = new Plane(Vector3.up, weaponControlSystem.lineOfSightTransform.localPosition);
            //mainCamera = Camera.main;
            //mainCamera.GetComponent<GameCameraController>().ActivateCameraMovement();

            //Settings
            int modelIndex = identityControl.spawnParameters.Value.ModelIndex;
            PlayerConfig config = SettingsManager.Instance.player.configs[modelIndex];

            //Health
            healthController.Initialize(config.health);
            healthController.OnDeath += () => {
                GetComponent<CharacterEffectsController>().RunDestroyScenario(true);

                if(IsOwner == true) GameManager.Instance.UI.ShowEndOfGamePopup();
            };

            //Inputs
            //inputActions = new ShooterInputControls();
            //inputActions.Player.Enable();

            movementSpeed = config.movementSpeed;
            gameObject.name = "Player: " + identityControl.spawnParameters.Value.Name;
            rigidbodyCharacterController.Init();
        }

        void FixedUpdate() {
            if(IsOwner == false) return;

            //Stop any movement when game ends
            //if(GameManager.Instance.gameState == GameState.GameIsOver) rigidbodyCharacterController.Stop();

            //Stop any movement when player is dead
            //if(healthController.isAlive == false) rigidbodyCharacterController.Stop();

            //Wait for game to start
            //if(GameManager.Instance.gameState != GameState.ActiveGame) return;

            //HandleKeyboardInput();
        }

        private void HandleKeyboardInput() {
            Vector2 mouseInput = inputActions.Player.Look.ReadValue<Vector2>();
            Ray ray = mainCamera.ScreenPointToRay(mouseInput);

            if(plane.Raycast(ray, out float enter)) {
                Vector3 hitPoint = ray.GetPoint(enter);
                Vector3 lookDirection = hitPoint - transform.position;
                lookDirection.y = 0;

                //Point line of sight in direction of cursor
                weaponControlSystem.lineOfSightTransform.localRotation = Quaternion.LookRotation(lookDirection);

                //Draw line of sight direction
                if(Application.isEditor)
                    Debug.DrawRay(weaponControlSystem.lineOfSightTransform.position, weaponControlSystem.lineOfSightTransform.forward, Color.red);
            }

            //Keyboard inputs
            Vector2 positionInput = inputActions.Player.Move.ReadValue<Vector2>().normalized;
            currentMovementInput = Vector3.SmoothDamp(currentMovementInput, positionInput, ref movementVelocity, smoothMovementTime);

            //Update movement speed according to currently active modifiers
            float currentSpeed = modifiersControlSystem.CalculateSpeedMultiplier() * movementSpeed;

            //Move (using physics)

            //rigidbodyCharacterController.Movement(positionInput != Vector2.zero);

            //Fire weapon
            if(inputActions.Player.Fire.inProgress)
                weaponControlSystem.Fire();
        }
    }
}