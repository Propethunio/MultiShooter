using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace HEAVYART.TopDownShooter.Netcode
{
    public class CommandReceiver : NetworkBehaviour
    {
        private ModifiersControlSystem _modifiersControlSystem;

        private void Awake()
        {
            _modifiersControlSystem = GetComponent<ModifiersControlSystem>();
        }

        [Rpc(SendTo.Everyone)]
        public void ReceiveBulletHitRpc(ModifierBase[] modifiers, ulong ownerID, double startTime)
        {
            foreach (var t in modifiers)
            {
                _modifiersControlSystem.AddModifier(t, ownerID, startTime);
            }
        }

        [Rpc(SendTo.Everyone)]
        public void ReceiveModifiersRpc(ModifierBase[] modifiers, ulong ownerID, double startTime)
        {
            foreach (var t in modifiers)
            {
                _modifiersControlSystem.AddModifier(t, ownerID, startTime);
            }
        }
    }
}
