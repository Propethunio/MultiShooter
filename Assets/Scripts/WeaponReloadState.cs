using HEAVYART.TopDownShooter.Netcode;

namespace cowsins
{
    public class WeaponReloadState : WeaponBaseState
    {
        private WeaponController controller;

        private PlayerStats stats;

        private ShooterInputControls inputActions;

        public WeaponReloadState(WeaponStates currentContext, WeaponStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory)
        {
        }

        public override void EnterState()
        {
            controller = _ctx.GetComponent<WeaponController>();
            stats = _ctx.GetComponent<PlayerStats>();
            controller.StartReload();

            inputActions = _ctx.GetComponent<PlayerMovement>().InputActions;
        }

        public override void UpdateState()
        {
            CheckSwitchState();
            if (!stats.controllable) return;
            CheckStopAim();
        }

        public override void FixedUpdateState()
        {
        }

        public override void ExitState()
        {
        }

        public override void CheckSwitchState()
        {
            if (!controller.Reloading) SwitchState(_factory.Default());
        }

        private void CheckStopAim()
        {
            if (!inputActions.Player.Aiming.IsPressed() || !controller.weapon.allowAimingIfReloading)
                controller.StopAim();
        }
    }
}