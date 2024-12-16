namespace cowsins {
    using HEAVYART.TopDownShooter.Netcode;
    using Unity.Services.Lobbies.Models;
    using UnityEngine;
    public class WeaponDefaultState : WeaponBaseState {
        private WeaponController controller;

        private PlayerStats stats;

        private PlayerMovement player;

        private float holdProgress;

        private float noBulletIndicator;

        private bool holdingEmpty = false;

        private ShooterInputControls inputActions;

        public WeaponDefaultState(WeaponStates currentContext, WeaponStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory) { }

        public override void EnterState() {
            controller = _ctx.GetComponent<WeaponController>();
            stats = _ctx.GetComponent<PlayerStats>();
            player = _ctx.GetComponent<PlayerMovement>();

            holdProgress = 0;
            holdingEmpty = false;

            inputActions = player.inputActions;
        }


        public override void UpdateState() {
            if(!stats.controllable) return;
            HandleInventory();
            if(controller.weapon == null) return;
            CheckSwitchState();
            CheckAim();
        }

        public override void FixedUpdateState() { }

        public override void ExitState() { }
        public override void CheckSwitchState() {
            if(controller.canShoot &&
                (controller.id.bulletsLeftInMagazine > 0 || controller.weapon.shootStyle == ShootStyle.Melee) && !controller.selectingWeapon) {
                switch(controller.weapon.shootMethod) {
                    case ShootingMethod.Press:
                        if(inputActions.Player.Firing.IsPressed() && !controller.holding) {
                            controller.holding = true; // We are holding 
                            SwitchState(_factory.Shoot());
                        }
                        break;
                    case ShootingMethod.PressAndHold:
                        if(inputActions.Player.Firing.IsPressed()) SwitchState(_factory.Shoot());
                        break;
                    case ShootingMethod.HoldAndRelease:
                        if(!inputActions.Player.Firing.IsPressed()) {
                            if(holdProgress > 100) SwitchState(_factory.Shoot());
                            holdProgress = 0;
                        }
                        if(inputActions.Player.Firing.IsPressed()) {
                            Debug.Log(holdProgress);
                            holdProgress += Time.deltaTime * controller.weapon.holdProgressSpeed;
                            controller.holding = true;
                        }
                        break;
                    case ShootingMethod.HoldUntilReady:
                        if(!inputActions.Player.Firing.IsPressed()) holdProgress = 0;
                        if(inputActions.Player.Firing.IsPressed()) {
                            holdProgress += Time.deltaTime * controller.weapon.holdProgressSpeed;
                            if(holdProgress > 100) SwitchState(_factory.Shoot());
                        }
                        break;
                }
            }

            if(controller.weapon.audioSFX.emptyMagShoot != null) {
                if(controller.id.bulletsLeftInMagazine <= 0 && inputActions.Player.Firing.IsPressed() && noBulletIndicator <= 0 && !holdingEmpty) {
                    //SoundManager.Instance.PlaySound(controller.weapon.audioSFX.emptyMagShoot, 0, .15f, true, 0);
                    noBulletIndicator = (controller.weapon.shootMethod == ShootingMethod.HoldAndRelease || controller.weapon.shootMethod == ShootingMethod.HoldUntilReady) ? 1 : controller.weapon.fireRate;
                    holdingEmpty = true;
                }

                if(noBulletIndicator > 0) noBulletIndicator -= Time.deltaTime;

                if(!inputActions.Player.Firing.IsPressed()) holdingEmpty = false;
            }

            if(controller.weapon.infiniteBullets) return;

            if(controller.weapon.reloadStyle == ReloadingStyle.defaultReload && !controller.shooting) {
                if(CheckIfReloadSwitch(controller))
                    SwitchState(_factory.Reload());
            } else {
                if(controller.id.heatRatio >= 1) SwitchState(_factory.Reload());
            }

        }

        private void CheckAim() {
            if(inputActions.Player.Aiming.IsPressed() && controller.weapon.allowAim) controller.Aim();
            CheckStopAim();
        }

        private void CheckStopAim() { if(!inputActions.Player.Aiming.IsPressed()) controller.StopAim(); }

        private void HandleInventory() => controller.HandleInventory();

        private bool CheckIfReloadSwitch(WeaponController controller) {
            return inputActions.Player.Reloading.IsPressed() && (int)controller.weapon.shootStyle != 2 && controller.id.bulletsLeftInMagazine < controller.id.magazineSize && controller.id.totalBullets > 0
                        || controller.id.bulletsLeftInMagazine <= 0 && controller.autoReload && (int)controller.weapon.shootStyle != 2 && controller.id.bulletsLeftInMagazine < controller.id.magazineSize && controller.id.totalBullets > 0;
        }
    }
}