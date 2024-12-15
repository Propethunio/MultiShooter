/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using cowsins;
using System.Collections.Generic;
using HEAVYART.TopDownShooter.Netcode;
using System.Net.Mail;
using System.Threading;
using Unity.Netcode;
#if UNITY_EDITOR
using UnityEditor.Presets;
#endif

#region others
[System.Serializable]
public class Events {
    public UnityEvent OnShoot, OnReload, OnFinishReload, OnAim, OnAiming, OnStopAim, OnHit, OnInventorySlotChanged, OnEquipWeapon;
}
[System.Serializable]
public class Effects {
    public GameObject grassImpact, metalImpact, mudImpact, woodImpact, enemyImpact;
}
[System.Serializable]
public class CustomShotMethods {
    public Weapon_SO weapon;
    public UnityEvent OnShoot;
}
#endregion
namespace cowsins {
    public class WeaponController : NetworkBehaviour {
        //References
        [SerializeField] CamShake CamShake;

        [Tooltip("An array that includes all your initial weapons.")] public Weapon_SO[] initialWeapons;

        public WeaponIdentification[] inventory;

        public UISlot[] slots;

        public Weapon_SO weapon;

        public GameObject UISlotPrefab;

        [Tooltip("Attach your main camera")] public Camera mainCamera;

        [Tooltip("Attach your camera pivot object")] public Transform cameraPivot;

        private Transform[] firePoint;

        [Tooltip("Attach your weapon holder")] public Transform weaponHolder;

        //Variables

        [Tooltip("max amount of weapons you can have")] public int inventorySize;

        [SerializeField, HideInInspector]
        public bool isAiming;

        private Vector3 aimRot;

        private bool reloading;
        public bool Reloading { get { return reloading; } set { reloading = value; } }

        [Tooltip("If true you won´t have to press the reload button when you run out of bullets")] public bool autoReload;

        private float reloadTime;

        private float coolSpeed;

        [Tooltip("If false, hold to aim, and release to stop aiming.")] public bool alternateAiming;

        [Tooltip("What objects should be hit")] public LayerMask hitLayer;

        [Tooltip("Do you want to resize your crosshair on shooting ? "), SerializeField] private bool resizeCrosshair;

        [Tooltip("Do not draw the crosshair when aiming a weapon")] public bool removeCrosshairOnAiming;

        [SerializeField] private Animator holsterMotionObject;

        public Animator HolsterMotionObject {
            get { return holsterMotionObject; }
        }

        public bool shooting { get; private set; } = false;

        private float spread;

        private float aimingCamShakeMultiplier, crouchingCamShakeMultiplier = 1;

        private float damagePerBullet;

        private float penetrationAmount;

        private float camShakeAmount;

        // Effects
        public Effects effects;

        public Events events;

        [Tooltip("Used for weapons with custom shot method. Here, " +
            "you can attach your scriptable objects and assign the method you want to call on shoot. " +
            "Please only assign those scriptable objects that use custom shot methods, Otherwise it won´t work or you will run into issues.")]
        public CustomShotMethods[] customShot;

        public UnityEvent customMethod;

        // Internal Use
        private int bulletsPerFire;

        public bool canShoot;

        RaycastHit hit;

        public int currentWeapon;

        private AudioClips audioSFX;

        public WeaponIdentification id;

        private PlayerStats stats;

        private WeaponAnimator weaponAnimator;

        private GameObject muzzleVFX;

        private float fireRate;

        public bool holding;

        public delegate void PerformShootStyle();

        public PerformShootStyle performShootStyle;

        private delegate IEnumerator Reload();

        private Reload reload;

        private delegate void ReduceAmmo();

        private ReduceAmmo reduceAmmo;

        private AudioClip fireSFX;

        private ShooterInputControls inputActions;

        [SerializeField] UIController UIController;

        private void Start() {
            InitialSettings();
            CreateInventoryUI();
            GetInitialWeapons();
            inputActions = gameObject.GetComponent<PlayerMovement>().inputActions;
        }

        private void Update() {
            HandleUI();
            HandleAimingMotion();
            ManageWeaponMethodsInputs();
            HandleRecoil();
            HandleHeatRatio();
        }

        private float aimingSpeed;
        public void Aim() {
            isAiming = true;

            if(weapon.applyBulletSpread) spread = weapon.aimSpreadAmount;
            aimingCamShakeMultiplier = weapon.camShakeAimMultiplier;

            events.OnAiming.Invoke();

            float cameraDistance = mainCamera.nearClipPlane + weapon.aimDistance;
            Vector3 cameraCenter = mainCamera.ScreenToWorldPoint(new Vector3(Screen.width / 2f, Screen.height / 2f, cameraDistance));
            id.aimPoint.position = Vector3.Lerp(id.aimPoint.position, cameraCenter, aimingSpeed * Time.deltaTime);
            id.aimPoint.localRotation = Quaternion.Lerp(id.aimPoint.localRotation, Quaternion.Euler(aimRot), aimingSpeed * Time.deltaTime);

        }

        public void StopAim() {
            if(weapon != null && weapon.applyBulletSpread) spread = weapon.spreadAmount;

            isAiming = false;
            aimingCamShakeMultiplier = 1;

            if(id == null) return;
            // Change the position and FOV
            id.aimPoint.localPosition = Vector3.Lerp(id.aimPoint.localPosition, id.originalAimPointPos, aimingSpeed * Time.deltaTime);
            id.aimPoint.localRotation = Quaternion.Lerp(id.aimPoint.localRotation, Quaternion.Euler(id.originalAimPointRot), aimingOutSpeed * Time.deltaTime);

            weaponHolder.localRotation = Quaternion.Lerp(weaponHolder.transform.localRotation, Quaternion.Euler(Vector3.zero), aimingOutSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Forces the Weapon to go back to its initial position 
        /// </summary>
        public void ForceAimReset() {
            isAiming = false;
            if(id?.aimPoint) {
                id.aimPoint.localPosition = id.originalAimPointPos;
                id.aimPoint.localRotation = Quaternion.Euler(id.originalAimPointRot);
            }
            weaponHolder.localRotation = Quaternion.Euler(Vector3.zero);
        }

        public void SetCrouchCamShakeMultiplier() {
            if(weapon)
                crouchingCamShakeMultiplier = weapon.camShakeCrouchMultiplier;
        }

        public void ResetCrouchCamShakeMultiplier() {
            crouchingCamShakeMultiplier = 1;
        }

        private float aimingOutSpeed;

        private void HandleAimingMotion() {
            aimingOutSpeed = (weapon != null) ? aimingSpeed : 2;
            if(isAiming && weapon != null) mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, weapon.aimingFOV, aimingSpeed * Time.deltaTime);
        }

        public void HandleHitscanProjectileShot() {

            if(!IsOwner) {
                return;
            }

            foreach(var p in firePoint) {
                canShoot = false; // since you have already shot, you will have to wait in order to being able to shoot again
                bulletsPerFire = weapon.bulletsPerFire;
                StartCoroutine(HandleShooting());

                // Adding a layer of realism, bullet shells get instantiated and interact with the world
                // We should  first check if we really wanna do this
                if(weapon.showBulletShells && (int)weapon.shootStyle != 2) {
                    var bulletShell = Instantiate(weapon.bulletGraphics, p.position, mainCamera.transform.rotation);
                    Rigidbody shellRigidbody = bulletShell.GetComponent<Rigidbody>();
                    float torque = Random.Range(-15f, 15f);
                    Vector3 shellForce = mainCamera.transform.right * 5 + mainCamera.transform.up * 5;
                    shellRigidbody.AddTorque(mainCamera.transform.right * torque, ForceMode.Impulse);
                    shellRigidbody.AddForce(shellForce, ForceMode.Impulse);
                }
            }
            //if(weapon.timeBetweenShots == 0) SoundManager.Instance.PlaySound(fireSFX, 0, weapon.pitchVariationFiringSFX, true, 0);

            Invoke(nameof(CanShoot), fireRate);
        }

        public void CustomShot() {
            // If we want to use fire Rate
            if(!weapon.continuousFire) {
                canShoot = false;
                Invoke(nameof(CanShoot), fireRate);
            }

            // Continuous fire
            customMethod?.Invoke();
        }
        private void SelectCustomShotMethod() {
            // Iterate through each item in the array
            for(int i = 0; i < customShot.Length; i++) {
                // Assign the on shoot event to the unity event to call it each time we fire
                if(customShot[i].weapon == weapon) {
                    customMethod = customShot[i].OnShoot;
                    return;
                }
            }

            Debug.LogError("Appropriate weapon scriptable object not found in the custom shot array (under the events tab). Please, configure the weapon scriptable object and the suitable method to fix this error");
        }
        private IEnumerator HandleShooting() {
            /// Determine wether we are sending a raycast, aka hitscan weapon, we are spawning a projectile
            int style = (int)weapon.shootStyle;

            weaponAnimator.StopWalkAndRunMotion();

            // Rest the bullets that have just been shot
            reduceAmmo?.Invoke();

            //Determine weapon class / style
            int i = 0;
            while(i < bulletsPerFire) {
                if(weapon == null) yield break;
                shooting = true;

                CamShake.ShootShake(camShakeAmount * aimingCamShakeMultiplier * crouchingCamShakeMultiplier);

                // Determine if we want to add an effect for FOV
                if(weapon.applyFOVEffectOnShooting) {
                    float fovAdjustment = isAiming ? weapon.AimingFOVValueToSubtract : weapon.FOVValueToSubtract;
                    mainCamera.fieldOfView -= fovAdjustment;
                }
                foreach(var p in firePoint) {
                    if(muzzleVFX != null)
                        SendFireRPC(p.position, mainCamera.transform.rotation);
                    //Instantiate(muzzleVFX, p.position, mainCamera.transform.rotation, mainCamera.transform); // VFX
                }
                CowsinsUtilities.ForcePlayAnim("shooting", inventory[currentWeapon].GetComponentInChildren<Animator>());
                //if(weapon.timeBetweenShots != 0) SoundManager.Instance.PlaySound(fireSFX, 0, weapon.pitchVariationFiringSFX, true, 0);

                ProgressRecoil();

                if(style == 0) HitscanShot();
                else if(style == 1) {
                    yield return new WaitForSeconds(weapon.shootDelay);
                    ProjectileShot();
                }

                yield return new WaitForSeconds(weapon.timeBetweenShots);
                i++;
            }
            shooting = false;
            yield break;
        }

        [Rpc(SendTo.Everyone)]
        private void SendFireRPC(Vector3 position, Quaternion rotation) {
            SpawnVFX(position, rotation);
        }

        private void SpawnVFX(Vector3 position, Quaternion rotation) {
            Instantiate(muzzleVFX, position, rotation);
        }
        /// <summary>
        /// Hitscan weapons send a raycast that IMMEDIATELY hits the target.
        /// That is why this shooting method is mostly used for pistols, snipers, rifles or SMGs
        /// </summary>
        private void HitscanShot() {
            events.OnShoot.Invoke();
            if(resizeCrosshair && UIController.crosshair != null) UIController.crosshair.Resize(weapon.crosshairResize * 100);

            Transform hitObj;

            //This defines the first hit on the object
            Vector3 dir = CowsinsUtilities.GetSpreadDirection(spread, mainCamera);
            Ray ray = new Ray(mainCamera.transform.position, dir);

            if(Physics.Raycast(ray, out hit, weapon.bulletRange, hitLayer)) {
                float dmg = damagePerBullet * stats.damageMultiplier;
                Hit(hit.collider.gameObject.layer, dmg, hit, true);
                hitObj = hit.collider.transform;

                //if(hit.transform.TryGetComponent<Rigidbody>(out Rigidbody rb)) {
                //    rb.AddForceAtPosition(ray.direction * 10, hit.point, ForceMode.Impulse);
                //}

                //Handle Penetration
                Ray newRay = new Ray(hit.point, ray.direction);
                RaycastHit newHit;

                if(Physics.Raycast(newRay, out newHit, penetrationAmount, hitLayer)) {
                    if(hitObj != newHit.collider.transform) {
                        float dmg_ = damagePerBullet * stats.damageMultiplier * weapon.penetrationDamageReduction;
                        Hit(newHit.collider.gameObject.layer, dmg_, newHit, true);
                    }
                }

                // Handle Bullet Trails
                if(weapon.bulletTrail == null) return;

                foreach(var p in firePoint) {
                    SendTrailRPC(p.position, hit.point);
                }
            }
        }

        [Rpc(SendTo.Everyone)]
        private void SendTrailRPC(Vector3 startPosition, Vector3 hitPoint) {
            SpawnTrail(startPosition, hitPoint);
        }

        private void SpawnTrail(Vector3 startPosition, Vector3 hitPoint) {
            TrailRenderer trail = Instantiate(weapon.bulletTrail, startPosition, Quaternion.identity);
            StartCoroutine(MoveTrail(trail, startPosition, hitPoint));
        }

        private IEnumerator MoveTrail(TrailRenderer trail, Vector3 startPosition, Vector3 hitPoint) {
            float time = 0;

            while(time < 1) {
                trail.transform.position = Vector3.Lerp(startPosition, hitPoint, time);
                time += Time.deltaTime / trail.time;

                yield return null;
            }

            trail.transform.position = hitPoint;

            Destroy(trail.gameObject, trail.time);
        }
        /// <summary>
        /// projectile shooting spawns a projectile
        /// Add a rigidbody to your bullet gameObject to make a curved trajectory
        /// This method is pretty much always used for grenades, rocket lFaunchers and grenade launchers.
        /// </summary>
        private void ProjectileShot() {
            events.OnShoot.Invoke();
            if(resizeCrosshair && UIController.crosshair != null) UIController.crosshair.Resize(weapon.crosshairResize * 100);

            Ray ray = mainCamera.ViewportPointToRay(new Vector3(.5f, .5f, 0f));
            Vector3 destination = (Physics.Raycast(ray, out hit) && !hit.transform.CompareTag("Player")) ? destination = hit.point + CowsinsUtilities.GetSpreadDirection(weapon.spreadAmount, mainCamera) : destination = ray.GetPoint(50f) + CowsinsUtilities.GetSpreadDirection(weapon.spreadAmount, mainCamera);

            foreach(var p in firePoint) {
                Bullet bullet = Instantiate(weapon.projectile, p.position, p.transform.rotation) as Bullet;

                if(weapon.explosionOnHit) bullet.explosionVFX = weapon.explosionVFX;

                bullet.hurtsPlayer = weapon.hurtsPlayer;
                bullet.explosionOnHit = weapon.explosionOnHit;
                bullet.explosionRadius = weapon.explosionRadius;
                bullet.explosionForce = weapon.explosionForce;

                bullet.criticalMultiplier = weapon.criticalDamageMultiplier;
                bullet.destination = destination;
                bullet.player = this.transform;
                bullet.speed = weapon.speed;
                bullet.GetComponent<Rigidbody>().isKinematic = (!weapon.projectileUsesGravity) ? true : false;
                bullet.damage = damagePerBullet * stats.damageMultiplier;
                bullet.duration = weapon.bulletDuration;
            }
        }

        /// <summary>
        /// If you landed a shot onto an enemy, a hit will occur
        /// This is where that is being handled
        /// </summary>
        private void Hit(LayerMask layer, float damage, RaycastHit h, bool damageTarget) {
            events.OnHit.Invoke();
            GameObject impact = null, impactBullet = null;

            Debug.Log(layer);

            // Check the passed layer
            // If it matches any of the provided layers by FPS Engine, then:
            // Instantiate according effect and rotate it accordingly to the surface.
            // Instantiate bullet holes as well.
            switch(layer) {
                case int l when l == LayerMask.NameToLayer("MapBorder"):
                    break;
                case int l when l == LayerMask.NameToLayer("Player"):
                    break;
                default:
                    SpawnImpactRPC(h.point, h.normal, h.collider != null ? h.collider.transform.GetComponent<NetworkObject>()?.NetworkObjectId ?? 0 : 0);
                    break;
            }

            // Apply damage
            if(!damageTarget) {
                return;
            }
            float finalDamage = damage * GetDistanceDamageReduction(h.collider.transform);

            // Check if a head shot was landed
            if(h.collider.gameObject.CompareTag("Critical")) {
                Debug.Log("CRIT");
                CowsinsUtilities.GatherDamageableParent(h.collider.transform).Damage(finalDamage * weapon.criticalDamageMultiplier, true);
            }
            // Check if a body shot was landed ( for children colliders )
            else if(h.collider.gameObject.CompareTag("BodyShot")) {
                Debug.Log("BODY");
                CowsinsUtilities.GatherDamageableParent(h.collider.transform).Damage(finalDamage, false);
            }
            // Check if the collision just comes from the parent
            else if(h.collider.GetComponent<IDamageable>() != null) {
                h.collider.GetComponent<IDamageable>().Damage(finalDamage, false);
            }
        }

        [Rpc(SendTo.Everyone)]
        private void SpawnImpactRPC(Vector3 position, Vector3 normal, ulong parentNetworkObjectId) {
            // Instantiate the impact effect
            var impact = Instantiate(effects.metalImpact, position, Quaternion.LookRotation(normal));

            // Instantiate the bullet hole
            if(weapon != null && weapon.bulletHoleImpact.groundIMpact != null) {
                var impactBullet = Instantiate(weapon.bulletHoleImpact.groundIMpact, position, Quaternion.LookRotation(normal));

                // Set parent if a valid parent exists
                if(parentNetworkObjectId != 0 && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(parentNetworkObjectId, out var parentNetworkObject)) {
                    impactBullet.transform.SetParent(parentNetworkObject.transform);
                }
            }
        }

        private void CanShoot() => canShoot = true;

        private void FinishedSelection() => selectingWeapon = false;

        public void StartReload() => StartCoroutine(reload());

        /// <summary>
        /// Handle Reloading
        /// </summary>
        private IEnumerator DefaultReload() {

            // Play reload sound
            //SoundManager.Instance.PlaySound(id.bulletsLeftInMagazine == 0 ? weapon.audioSFX.emptyMagReload : weapon.audioSFX.reload, .1f, 0, true, 0);
            reloading = true;
            yield return new WaitForSeconds(.001f);

            // Play animation
            CowsinsUtilities.PlayAnim("reloading", inventory[currentWeapon].GetComponentInChildren<Animator>());

            // Wait reloadTime seconds, assigned in the weapon scriptable object.
            yield return new WaitForSeconds(reloadTime);

            // Reload has finished
            events.OnFinishReload.Invoke();

            canShoot = true;
            if(!reloading) yield break;

            // Run custom event
            events.OnReload.Invoke();

            reloading = false;

            // Set the proper amount of bullets, depending on magazine type.
            if(!weapon.limitedMagazines) id.bulletsLeftInMagazine = id.magazineSize;
            else {
                if(id.totalBullets > id.magazineSize) // You can still reload a full magazine
                {
                    id.totalBullets = id.totalBullets - (id.magazineSize - id.bulletsLeftInMagazine);
                    id.bulletsLeftInMagazine = id.magazineSize;
                } else if(id.totalBullets == id.magazineSize) // You can only reload a single full magazine more
                  {
                    id.totalBullets = id.totalBullets - (id.magazineSize - id.bulletsLeftInMagazine);
                    id.bulletsLeftInMagazine = id.magazineSize;
                } else if(id.totalBullets < id.magazineSize) // You cant reload a whole magazine
                  {
                    int bulletsLeft = id.bulletsLeftInMagazine;
                    if(id.bulletsLeftInMagazine + id.totalBullets <= id.magazineSize) {
                        id.bulletsLeftInMagazine = id.bulletsLeftInMagazine + id.totalBullets;
                        if(id.totalBullets - (id.magazineSize - bulletsLeft) >= 0) id.totalBullets = id.totalBullets - (id.magazineSize - bulletsLeft);
                        else id.totalBullets = 0;
                    } else {
                        int ToAdd = id.magazineSize - id.bulletsLeftInMagazine;
                        id.bulletsLeftInMagazine = id.bulletsLeftInMagazine + ToAdd;
                        if(id.totalBullets - ToAdd >= 0) id.totalBullets = id.totalBullets - ToAdd;
                        else id.totalBullets = 0;
                    }
                }
            }
        }

        private IEnumerator OverheatReload() {
            // Currently reloading
            canShoot = false;

            float waitTime = weapon.cooledPercentageAfterOverheat;

            // Stop being able to shoot, prevents from glitches
            CancelInvoke(nameof(CanShoot));

            // Wait until the heat ratio is appropriate to keep shooting
            yield return new WaitUntil(() => id.heatRatio <= waitTime);

            // Reload has finished
            events.OnFinishReload.Invoke();

            reloading = false;
            canShoot = true;
        }

        // On shooting regular reloading weapons, reduce the bullets in the magazine
        private void ReduceDefaultAmmo() {
            if(!weapon.infiniteBullets) {
                id.bulletsLeftInMagazine -= weapon.ammoCostPerFire;
                if(id.bulletsLeftInMagazine < 0) {
                    id.bulletsLeftInMagazine = 0;
                }
            }
        }


        // On shooting overheat reloading weapons, increase the heat ratio.
        private void ReduceOverheatAmmo() {
            id.heatRatio += (float)1f / id.magazineSize;
        }

        // Handles overheat weapons reloading.
        private void HandleHeatRatio() {
            if(weapon == null || id.magazineSize == 0 || weapon.reloadStyle == ReloadingStyle.defaultReload) return;

            // Handle cooling
            // Dont keep cooling if it is completely cooled
            if(id.heatRatio > 0) id.heatRatio -= Time.deltaTime * coolSpeed;
            if(id.heatRatio > 1) id.heatRatio = 1;
        }

#if UNITY_EDITOR
        public Preset crosshairPreset;
#endif
        /// <summary>
        /// Active your new weapon
        /// </summary>
        public void UnHolster(GameObject weaponObj, bool playAnim) {

            canShoot = true;

            weaponObj.SetActive(true);
            id = weaponObj.GetComponent<WeaponIdentification>();

            // Get Shooting Style 
            // We subscribe our performShootStyle method to different functions depending on the shooting style
            switch((int)weapon.shootStyle) {
                case 0: performShootStyle = HandleHitscanProjectileShot; break;
                case 1: performShootStyle = HandleHitscanProjectileShot; break;
                case 3: performShootStyle = CustomShot; break;
            }

            weaponObj.GetComponentInChildren<Animator>().enabled = true;
            if(playAnim) CowsinsUtilities.PlayAnim("unholster", inventory[currentWeapon].GetComponentInChildren<Animator>());
            //SoundManager.Instance.PlaySound(weapon.audioSFX.unholster, .1f, 0, true, 0);
            Invoke("FinishedSelection", .5f);

            if(weapon.shootStyle == ShootStyle.Custom) SelectCustomShotMethod();
            else customMethod = null;

            GetAttachmentsModifiers();

            weaponAnimator.StopWalkAndRunMotion();

            // Define reloading method
            if(weapon.reloadStyle == ReloadingStyle.defaultReload) {
                reload = DefaultReload;
                reduceAmmo = ReduceDefaultAmmo;
            } else {
                reload = OverheatReload;
                reduceAmmo = ReduceOverheatAmmo;
                coolSpeed = weapon.coolSpeed;
            }

            firePoint = inventory[currentWeapon].FirePoint;

            // UI & OTHERS
            if(weapon.infiniteBullets || weapon.reloadStyle == ReloadingStyle.Overheat) {
                UIController.DetectReloadMethod(false, !weapon.infiniteBullets);
            } else {
                UIController.DetectReloadMethod(true, false);
            }

            if((int)weapon.shootStyle == 2) UIController.DetectReloadMethod(false, false);

            UIController.SetWeaponDisplay(weapon);
        }

        private void HandleUI() {

            // If we dont own a weapon yet, do not continue
            if(weapon == null) {
                UIController.DisableWeaponUI();
                return;
            }

            UIController.EnableDisplay();

            if(weapon.reloadStyle == ReloadingStyle.defaultReload) {
                if(!weapon.infiniteBullets) {

                    bool activeReloadUI = id.bulletsLeftInMagazine == 0 && !autoReload && !weapon.infiniteBullets;
                    bool activeLowAmmoUI = id.bulletsLeftInMagazine < id.magazineSize / 3.5f && id.bulletsLeftInMagazine > 0;
                    // Set different display settings for each shoot style 
                    if(weapon.limitedMagazines) {
                        UIController.UpdateBullets(id.bulletsLeftInMagazine, id.totalBullets, activeReloadUI, activeLowAmmoUI);
                    } else {
                        UIController.UpdateBullets(id.bulletsLeftInMagazine, id.magazineSize, activeReloadUI, activeLowAmmoUI);
                    }
                } else {
                    UIController.UpdateBullets(id.bulletsLeftInMagazine, id.totalBullets, false, false);
                }
            } else {
                UIController.UpdateHeatRatio(id.heatRatio);
            }



            //Crosshair Management
            // If we dont use a crosshair stop right here
            if(UIController.crosshair == null) {
                return;
            }
            // Detect enemies on aiming
            RaycastHit hit_;
            if(Physics.Raycast(mainCamera.transform.position, mainCamera.transform.forward, out hit_, weapon.bulletRange) && hit_.transform.CompareTag("Enemy") || Physics.Raycast(mainCamera.transform.position, mainCamera.transform.forward, out hit_, weapon.bulletRange) && hit_.transform.CompareTag("Critical"))
                UIController.crosshair.SpotEnemy(true);
            else UIController.crosshair.SpotEnemy(false);
        }
        /// <summary>
        /// Procedurally generate the Inventory UI depending on your needs
        /// </summary>
        private void CreateInventoryUI() {
            // Adjust the inventory size 
            slots = new UISlot[inventorySize];
            int j = 0; // Control variable
            while(j < inventorySize) {
                // Load the slot, instantiate it and set it to the slots array
                var slot = Instantiate(UISlotPrefab, Vector3.zero, Quaternion.identity, UIController.inventoryContainer.transform) as GameObject;
                slot.GetComponent<UISlot>().id = j;
                slots[j] = slot.GetComponent<UISlot>();
                j++;
            }
        }

        /// <summary>
        /// Change you current slots, core of the inventory
        /// </summary>
        public void HandleInventory() {
            if(inputActions.Player.Reloading.IsPressed()) return; // Do not change weapons while reloading
                                                                  // Change slot
            if(inputActions.Player.Scrolling.ReadValue<Vector2>().y > 0 || inputActions.Player.ChangeWeapons.WasPressedThisFrame() && inputActions.Player.ChangeWeapons.ReadValue<float>() < 0) {
                ForceAimReset(); // Move Weapon back to the original position
                if(currentWeapon < inventorySize - 1) {
                    currentWeapon++;
                    SelectWeapon();
                }
            }
            if(inputActions.Player.Scrolling.ReadValue<Vector2>().y < 0 || inputActions.Player.ChangeWeapons.WasPressedThisFrame() && inputActions.Player.ChangeWeapons.ReadValue<float>() > 0) {
                ForceAimReset(); // Move Weapon back to the original position
                if(currentWeapon > 0) {
                    currentWeapon--;
                    SelectWeapon();
                }
            }
        }

        [HideInInspector] public bool selectingWeapon;
        public void SelectWeapon() {
            canShoot = false;
            selectingWeapon = true;
            UIController.crosshair.SpotEnemy(false);
            events.OnInventorySlotChanged.Invoke(); // Invoke your custom method
            weapon = null;
            // Spawn the appropriate weapon in the inventory

            foreach(WeaponIdentification weapon_ in inventory) {
                if(weapon_ != null) {
                    weapon_.gameObject.SetActive(false);
                    weapon_.GetComponentInChildren<Animator>().enabled = false;
                    if(weapon_ == inventory[currentWeapon]) {
                        weapon = inventory[currentWeapon].weapon;

                        weapon_.GetComponentInChildren<Animator>().enabled = true;
                        UnHolster(weapon_.gameObject, true);

#if UNITY_EDITOR
                        UIController.crosshair.GetComponent<CrosshairShape>().currentPreset = weapon.crosshairPreset;
                        CowsinsUtilities.ApplyPreset(UIController.crosshair.GetComponent<CrosshairShape>().currentPreset, UIController.crosshair.GetComponent<CrosshairShape>());
#endif
                    }
                }
            }

            // Handle the UI Animations
            foreach(UISlot slot in slots) {
                slot.transform.localScale = slot.initScale;
                slot.GetComponent<CanvasGroup>().alpha = .2f;
            }
            slots[currentWeapon].transform.localScale = slots[currentWeapon].transform.localScale * 1.2f;
            slots[currentWeapon].GetComponent<CanvasGroup>().alpha = 1;

            CancelInvoke(nameof(CanShoot));

            events.OnEquipWeapon.Invoke(); // Invoke your custom method

        }

        private void GetInitialWeapons() {
            if(initialWeapons.Length == 0) return;

            int i = 0;
            while(i < initialWeapons.Length) {
                InstantiateWeapon(initialWeapons[i], i, null, null);
                i++;
            }
            weapon = initialWeapons[0];
        }

        public void InstantiateWeapon(Weapon_SO newWeapon, int inventoryIndex, int? _bulletsLeftInMagazine, int? _totalBullets) {
            // Instantiate Weapon
            var weaponPicked = Instantiate(newWeapon.weaponObject, weaponHolder);
            weaponPicked.transform.localPosition = newWeapon.weaponObject.transform.localPosition;

            // Destroy the Weapon if it already exists in the same slot
            if(inventory[inventoryIndex] != null) Destroy(inventory[inventoryIndex].gameObject);

            // Set the Weapon
            inventory[inventoryIndex] = weaponPicked;

            // Select weapon if it is the current Weapon
            if(inventoryIndex == currentWeapon) {
                weapon = newWeapon;
            } else weaponPicked.gameObject.SetActive(false);


            // if _bulletsLeftInMagazine is null, calculate magazine size. If not, simply assign _bulletsLeftInMagazine
            inventory[inventoryIndex].bulletsLeftInMagazine = _bulletsLeftInMagazine ?? (newWeapon.magazineSize);
            inventory[inventoryIndex].totalBullets = _totalBullets ??
            (newWeapon.limitedMagazines
                ? newWeapon.magazineSize * newWeapon.totalMagazines
                : newWeapon.magazineSize);

            //UI
            slots[inventoryIndex].weapon = newWeapon;
            slots[inventoryIndex].GetImage();
#if UNITY_EDITOR
            UIController.crosshair.GetComponent<CrosshairShape>().currentPreset = weapon?.crosshairPreset;
            if(UIController.crosshair.GetComponent<CrosshairShape>().currentPreset)
                CowsinsUtilities.ApplyPreset(UIController.crosshair.GetComponent<CrosshairShape>().currentPreset, UIController.crosshair.GetComponent<CrosshairShape>());
#endif

            if(weapon?.shootStyle == ShootStyle.Custom) SelectCustomShotMethod();
            else customMethod = null;

            if(inventoryIndex == currentWeapon) SelectWeapon();

        }

        public void ReleaseCurrentWeapon() {
            Destroy(inventory[currentWeapon].gameObject);
            weapon = null;
            slots[currentWeapon].weapon = null;
        }
        private float GetDistanceDamageReduction(Transform target) {
            if(!weapon.applyDamageReductionBasedOnDistance) return 1;
            if(Vector3.Distance(target.position, transform.position) > weapon.minimumDistanceToApplyDamageReduction)
                return (weapon.minimumDistanceToApplyDamageReduction / Vector3.Distance(target.position, transform.position)) * weapon.damageReductionMultiplier;
            else return 1;
        }

        private void ManageWeaponMethodsInputs() {
            if(!inputActions.Player.Firing.IsPressed()) holding = false; // Making sure we are not holding}
        }

        private float evaluationProgress, evaluationProgressX;
        private void HandleRecoil() {
            if(weapon == null || !weapon.applyRecoil) {
                cameraPivot.localRotation = Quaternion.Lerp(cameraPivot.localRotation, Quaternion.Euler(Vector3.zero), 3 * Time.deltaTime);
                return;
            }

            // Going back to normal shooting; 
            float speed = (weapon == null) ? 10 : weapon.recoilRelaxSpeed * 3;
            if(!inputActions.Player.Firing.IsPressed() || reloading || !PlayerStats.Controllable) {
                cameraPivot.localRotation = Quaternion.Lerp(cameraPivot.localRotation, Quaternion.Euler(Vector3.zero), speed * Time.deltaTime);
                evaluationProgress = 0;
                evaluationProgressX = 0;
            }

            if(weapon == null || reloading || !PlayerStats.Controllable) return;

            if(inputActions.Player.Firing.IsPressed()) {
                float xamount = (weapon.applyDifferentRecoilOnAiming && isAiming) ? weapon.xRecoilAmountOnAiming : weapon.xRecoilAmount;
                float yamount = (weapon.applyDifferentRecoilOnAiming && isAiming) ? weapon.yRecoilAmountOnAiming : weapon.yRecoilAmount;

                cameraPivot.localRotation = Quaternion.Lerp(cameraPivot.localRotation, Quaternion.Euler(new Vector3(-weapon.recoilY.Evaluate(evaluationProgress) * yamount, -weapon.recoilX.Evaluate(evaluationProgressX) * xamount, 0)), 10 * Time.deltaTime);
            }
        }

        private void ProgressRecoil() {
            if(weapon.applyRecoil) {
                evaluationProgress += 1f / weapon.magazineSize;
                evaluationProgressX += 1f / weapon.magazineSize;
            }
        }

        private void InitialSettings() {
            stats = GetComponent<PlayerStats>();
            weaponAnimator = GetComponent<WeaponAnimator>();
            inventory = new WeaponIdentification[inventorySize];
            currentWeapon = 0;
            canShoot = true;
            mainCamera.fieldOfView = GetComponent<PlayerMovement>().normalFOV;
        }

        private void GetAttachmentsModifiers() {
            // Grab references for the variables in case their respective attachments are null or not.

            fireSFX = weapon.audioSFX.shooting[Random.Range(0, weapon.audioSFX.shooting.Length - 1)];

            aimRot = weapon.aimingRotation;
            muzzleVFX = weapon.muzzleVFX;

            float baseReloadTime = weapon.reloadTime;
            reloadTime = baseReloadTime;

            float baseAimSpeed = weapon.aimingSpeed;
            aimingSpeed = baseAimSpeed;

            float baseFireRate = weapon.fireRate;
            fireRate = baseFireRate;

            float baseDamagePerBullet = weapon.damagePerBullet;
            damagePerBullet = baseDamagePerBullet;

            float baseCamShakeAmount = weapon.camShakeAmount;
            camShakeAmount = baseCamShakeAmount;

            float basePenetrationAmount = weapon.penetrationAmount;
            penetrationAmount = basePenetrationAmount;
        }
    }
}