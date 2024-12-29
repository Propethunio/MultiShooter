using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace HEAVYART.TopDownShooter.Netcode
{
    public class CharacterColorController : NetworkBehaviour
    {
        [Range(0, 1f)]
        public float colorFactor = 0.5f;
        public List<Renderer> renderers = new List<Renderer>();

        private CharacterIdentityControl _identityControl;

        private void Awake()
        {
            _identityControl = GetComponent<CharacterIdentityControl>();
        }

        public override void OnNetworkSpawn()
        {
            if (!_identityControl.IsPlayer) return;
            var targetColor = _identityControl.spawnParameters.Value.Color;
            foreach (var t in renderers)
            {
                t.material.color = Color.Lerp(t.material.color, targetColor, colorFactor);
            }
        }
    }
}
