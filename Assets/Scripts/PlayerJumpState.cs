using HEAVYART.TopDownShooter.Netcode;
using UnityEngine;

namespace cowsins
{
    public class PlayerJumpState : PlayerBaseState
    {
        public PlayerJumpState(PlayerStates currentContext, PlayerStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory)
        {
        }

        private PlayerMovement player;

        private PlayerStats stats;

        private ShooterInputControls inputActions;

        public override void EnterState()
        {
            player = _ctx.GetComponent<PlayerMovement>();
            stats = _ctx.GetComponent<PlayerStats>();
            player.events.OnJump.Invoke();
            player.Jump();
            inputActions = player.inputActions;
        }

        public override void UpdateState()
        {
            CheckSwitchState();
            HandleMovement();
        }

        public override void FixedUpdateState()
        {
        }

        public override void ExitState()
        {
        }

        public override void CheckSwitchState()
        {
            if (player.ReadyToJump && inputActions.Player.Jumping.WasPressedThisFrame() && player.grounded)
            {
                SwitchState(_factory.Jump());
                return;
            }

            if (stats.health <= 0)
            {
                SwitchState(_factory.Die());
                return;
            }

            if (!player.grounded) return;
            SwitchState(_factory.Default());
        }

        void HandleMovement()
        {
            player.Movement(stats.controllable);
            player.Look();
        }
    }
}