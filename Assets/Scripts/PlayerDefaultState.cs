using HEAVYART.TopDownShooter.Netcode;
using UnityEngine;

namespace cowsins
{
    public class PlayerDefaultState : PlayerBaseState
    {
        public PlayerDefaultState(PlayerStates currentContext, PlayerStateFactory playerStateFactory)
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
            inputActions = player.inputActions;
        }

        public override void UpdateState()
        {
            HandleMovement();
            if (!stats.controllable) return;
            CheckSwitchState();
        }

        public override void FixedUpdateState()
        {
            player.Movement(stats.controllable);
        }

        public override void ExitState()
        {
        }

        public override void CheckSwitchState()
        {
            // Check Death
            if (stats.health <= 0) SwitchState(_factory.Die());

            // Check Jump
            if (player.ReadyToJump && inputActions.Player.Jumping.WasPressedThisFrame() &&
                (player.grounded || player.canCoyote))
                SwitchState(_factory.Jump());
        }

        private void HandleMovement()
        {
            if (player.x != 0 || player.y != 0) player.events.OnMove.Invoke();
            if (!stats.controllable) return;
            player.Look();
            player.FootSteps();
            player.HandleCoyoteJump();
        }
    }
}