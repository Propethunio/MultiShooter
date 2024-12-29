using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace HEAVYART.TopDownShooter.Netcode
{
    public class HealthController : NetworkBehaviour
    {
        private float CurrentHealth { get; set; }
        private float MaxHealth { get; set; }

        public bool IsAlive => CurrentHealth > 0;

        public Action OnDeath;

        private ModifiersControlSystem _modifiersControlSystem;
        private CharacterIdentityControl _identityControl;

        public void Awake()
        {
            _modifiersControlSystem = GetComponent<ModifiersControlSystem>();
            _identityControl = GetComponent<CharacterIdentityControl>();
        }

        public void Initialize(float maxHealth)
        {
            CurrentHealth = maxHealth;
            MaxHealth = maxHealth;
        }

        private void FixedUpdate()
        {
            if (IsAlive == false) return;

            var updatedHealth = _modifiersControlSystem.HandleHealthModifiers(CurrentHealth, OnDeathEvent);
            CurrentHealth = Mathf.Clamp(updatedHealth, 0, MaxHealth);
        }

        private void OnDeathEvent(ActiveModifierData activeModifier)
        {
            if (_identityControl.IsOwner)
                ConfirmCharacterDeathRpc();
        }

        [Rpc(SendTo.Everyone)]
        private void ConfirmCharacterDeathRpc()
        {
            CurrentHealth = 0;
            OnDeath?.Invoke();
        }
    }
}
