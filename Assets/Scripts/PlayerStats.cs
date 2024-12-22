/// <summary>
/// This script belongs to cowsins� as a part of the cowsins� FPS Engine. All rights reserved. 
/// </summary>

using UnityEngine;
using UnityEngine.Events;
using HEAVYART.TopDownShooter.Netcode;
using Unity.Netcode;

namespace cowsins
{
    [System.Serializable]
    public class PlayerStats : NetworkBehaviour, IDamageable
    {
        public Collider mainCollider;
        [HideInInspector] public bool wasDamagedByOtherPlayer;
        [HideInInspector] public ulong killerID;

        [SerializeField] private MoveCamera moveCamera;

        [SerializeField] UIController UIController;

        [System.Serializable]
        public class Events
        {
            public UnityEvent OnDeath, OnDamage, OnHeal;
        }

        #region variables

        [ReadOnly] public float health, shield;

        public float maxHealth, maxShield, damageMultiplier, healMultiplier;

        [Tooltip("Turn on to apply damage on falling from great height")]
        public bool takesFallDamage;

        [Tooltip("Minimum height ( in units ) the player has to fall from in order to take damage"), SerializeField,
         Min(1)]
        private float minimumHeightDifferenceToApplyDamage;

        [Tooltip("How the damage will increase on landing if the damage on fall is going to be applied"),
         SerializeField]
        private float fallDamageMultiplier;

        [SerializeField] private bool enableAutoHeal = false;

        public bool EnableAutoHeal
        {
            get { return enableAutoHeal; }
        }

        [SerializeField, Min(0)] private float healRate;

        [SerializeField] private float healAmount;


        [SerializeField] private bool restartAutoHealAfterBeingDamaged = false;

        public bool RestartAutoHealAfterBeingDamaged
        {
            get { return restartAutoHealAfterBeingDamaged; }
        }

        [SerializeField] private float restartAutoHealTime;

        public float? height = null;

        [HideInInspector] public bool isDead;

        private PlayerMovement player;

        private PlayerStats stats;

        private PlayerStates states;

        public Events events;

        private CharacterAnimationController characterAnimationController;

        [SerializeField] private GameObject weaponHolder;

        #endregion


        private void Start()
        {
            GetAllReferences();
            // Apply basic settings 
            health = maxHealth;
            shield = maxShield;
            damageMultiplier = 1;
            healMultiplier = 1;

            UIController.HealthSetUp(health, shield, maxHealth, maxShield);

            GrantControl();

            if (enableAutoHeal)
                StartAutoHeal();
        }

        [ClientRpc]
        void ToggleCursorClientRpc(bool value)
        {
            if (!IsOwner) return;

            if (Cursor.visible == value) return;

            Cursor.visible = value;
            if (Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.SetCursor(null, new Vector2(Screen.width / 2, Screen.height / 2), CursorMode.Auto);
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
            }
        }

        private void Update()
        {
            ToggleCursorClientRpc(isDead || GameManager.Instance.gameState == GameState.GameIsOver);
            Controllable = controllable;

            if (stats.isDead) return; // If player is alive, continue

            // Manage fall damage
            if (!takesFallDamage) return;
            ManageFallDamage();
        }

        [ServerRpc(RequireOwnership =
            false)] // This makes the method only callable from a client but executed on the server
        public void DestroyObjectServerRpc()
        {
            // Destroy the object on the server side
            DestroyObjectOnServer();
        }

        // This method is executed on the server to destroy the object and then notify all clients
        private void DestroyObjectOnServer()
        {
            // Destroy the object on the server
            Destroy(gameObject);

            // Call the RpcDestroyObject() to notify all clients to destroy the object
            DestroyObjectClientRpc();
        }

        // This ClientRpc is called from the server to destroy the object on all clients
        [ClientRpc] // This makes the method run on all clients
        private void DestroyObjectClientRpc()
        {
            // Destroy the object on all clients
            Destroy(gameObject);
        }

        // This method should be called to destroy the object either from the server or client
        public void DestroySelf()
        {
            if (IsServer)
            {
                // If we're on the server, directly destroy the object
                DestroyObjectOnServer();
            }
            else
            {
                // If we're on the client, tell the server to destroy the object
                DestroyObjectServerRpc();
            }
        }

        /// <summary>
        /// Our Player Stats is IDamageable, which means it can be damaged
        /// If so, call this method to damage the player
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void DamageServerRpc(float _damage, bool isHeadshot, bool byOhterPlayer = false, ulong killerID = 0)
        {
            if (byOhterPlayer)
            {
                wasDamagedByOtherPlayer = true;
                this.killerID = killerID;
            }


            // Ensure damage is a positive value
            var damage = Mathf.Abs(_damage);

            // Trigger damage event (only on the server)
            events.OnDamage.Invoke();

            // Apply damage to health
            health -= damage;

            // Clamp health to ensure it doesn't drop below 0
            health = Mathf.Max(health, 0);

            // Notify all clients about the updated health
            UpdateHealthClientRpc(health);

            // Handle death on the server
            if (health <= 0)
            {
                Die();
            }

            // Handle auto-healing
            if (enableAutoHeal && restartAutoHealAfterBeingDamaged)
            {
                CancelInvoke(nameof(AutoHeal));
                InvokeRepeating(nameof(AutoHeal), restartAutoHealTime, healRate);
            }
        }

        [ClientRpc]
        private void UpdateHealthClientRpc(float updatedHealth)
        {
            // Update local health variable
            health = updatedHealth;

            // Notify UI about the health change (only on clients)
            UIController.UpdateHealthUI(health, shield, true);
        }

        public void Heal(float healAmount)
        {
            var adjustedHealAmount = Mathf.Abs(healAmount * healMultiplier);

            // If we are at full health and shield, do not heal
            if ((maxShield > 0 && shield == maxShield) || (maxShield == 0 && health == maxHealth))
            {
                return;
            }

            // Trigger heal event
            events.OnHeal.Invoke();

            // Calculate effective healing for health
            var effectiveHealForHealth = Mathf.Min(adjustedHealAmount, maxHealth - health);
            health += effectiveHealForHealth;

            // Calculate remaining heal amount after health is full
            var remainingHeal = adjustedHealAmount - effectiveHealForHealth;

            // Apply remaining heal to shield if applicable
            if (remainingHeal > 0 && maxShield > 0)
            {
                shield = Mathf.Min(shield + remainingHeal, maxShield);
            }

            // Notify UI about the health change
            UIController.UpdateHealthUI(health, shield, false);
        }

        /// <summary>
        /// Perform any actions On death
        /// </summary>
        private void Die()
        {
            if (!IsServer)
                return; // Ensure this logic only executes on the server

            if (isDead) return;

            if (wasDamagedByOtherPlayer)
            {
                GameManager.Instance.RegisterCharacterDeathRPC(GetComponent<NetworkObject>().OwnerClientId, true,
                    killerID);
            }
            else
            {
                GameManager.Instance.RegisterCharacterDeathRPC(GetComponent<NetworkObject>().OwnerClientId, false);
            }

            isDead = true;

            // Play death animation on the server
            characterAnimationController.PlayDeathAnimation();

            // Notify all clients about the player's death
            HandlePlayerDeathClientRpc();
        }

        [ClientRpc]
        private void HandlePlayerDeathClientRpc()
        {
            mainCollider.enabled = false;

            // Disable the weapon holder
            weaponHolder.SetActive(false);

            // Switch the camera to death mode
            moveCamera.ChangeToDeath();

            // Play death animation locally (optional, if not handled by the server)
            characterAnimationController.PlayDeathAnimation();

            UIController.ShowEndOfGamePopup();
        }

        /// <summary>
        /// Basically find everything the script needs to work
        /// </summary>
        private void GetAllReferences()
        {
            stats = GetComponent<PlayerStats>();
            states = GetComponent<PlayerStates>();
            player = GetComponent<PlayerMovement>();
            characterAnimationController = GetComponent<CharacterAnimationController>();
        }

        /// <summary>
        /// While airborne, if you exceed a certain time, damage on fall will be applied
        /// </summary>
        private void ManageFallDamage()
        {
            switch (player.grounded)
            {
                // Grab current player height
                case false when transform.position.y > height:
                case false when height == null:
                    height = transform.position.y;
                    break;
                // Check if we landed, as well if our current height is lower than the original height. If so, check if we should apply damage
                case true when height != null && transform.position.y < height:
                {
                    var currentHeight = transform.position.y;

                    // Transform nullable variable into a non nullable float for later operations
                    var noNullHeight = height ?? default(float);

                    var heightDifference = noNullHeight - currentHeight;

                    // If the height difference is enough, apply damage
                    if (heightDifference > minimumHeightDifferenceToApplyDamage)
                        DamageServerRpc(heightDifference * fallDamageMultiplier, false);

                    // Reset height
                    height = null;
                    break;
                }
            }
        }

        private void StartAutoHeal()
        {
            InvokeRepeating(nameof(AutoHeal), healRate, healRate);
        }

        private void AutoHeal()
        {
            if (shield >= maxShield && health >= maxHealth) return;

            Heal(healAmount);
        }

        public bool controllable { get; private set; } = true;

        public static bool Controllable { get; private set; }


        public void GrantControl() => controllable = true;

        public void LoseControl() => controllable = false;

        public void ToggleControl() => controllable = !controllable;

        public void CheckIfCanGrantControl()
        {
            if (isDead) return;
            GrantControl();
        }

        public void Respawn(Vector3 respawnPosition)
        {
            isDead = false;
            states.ForceChangeState(states._States.Default());
            health = maxHealth;
            shield = maxShield;
            transform.position = respawnPosition;
        }
    }
}