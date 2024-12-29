using System;
using System.Collections;
using cowsins;
using HEAVYART.TopDownShooter.Netcode;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;
#if UNITY_EDITOR
using UnityEditor.Presets;
#endif

#region others

[Serializable]
public class Events
{
    public UnityEvent OnShoot,
        OnReload,
        OnFinishReload,
        OnAim,
        OnAiming,
        OnStopAim,
        OnHit,
        OnInventorySlotChanged,
        OnEquipWeapon;
}

[Serializable]
public class Effects
{
    public GameObject grassImpact, metalImpact, mudImpact, woodImpact, enemyImpact;
}

[Serializable]
public class CustomShotMethods
{
    public Weapon_SO weapon;
    public UnityEvent OnShoot;
}

#endregion

namespace cowsins
{
    public class WeaponController : NetworkBehaviour
    {
        public delegate void PerformShootStyle();

        [SerializeField] private SoundMenago soundMenago;

        [SerializeField] private CamShake CamShake;

        [Tooltip("An array that includes all your initial weapons.")]
        public Weapon_SO[] initialWeapons;

        public WeaponIdentification[] inventory;

        public UISlot[] slots;

        public Weapon_SO weapon;

        public GameObject UISlotPrefab;

        [Tooltip("Attach your main camera")] public Camera mainCamera;

        [Tooltip("Attach your camera pivot object")]
        public Transform cameraPivot;

        [Tooltip("Attach your weapon holder")] public Transform weaponHolder;


        [Tooltip("max amount of weapons you can have")]
        public int inventorySize;

        [SerializeField] [HideInInspector] public bool isAiming;

        [Tooltip("If true you won´t have to press the reload button when you run out of bullets")]
        public bool autoReload;

        [Tooltip("If false, hold to aim, and release to stop aiming.")]
        public bool alternateAiming;

        [Tooltip("What objects should be hit")]
        public LayerMask hitLayer;

        [Tooltip("Do you want to resize your crosshair on shooting ? ")] [SerializeField]
        private bool resizeCrosshair;

        [Tooltip("Do not draw the crosshair when aiming a weapon")]
        public bool removeCrosshairOnAiming;

        [SerializeField] private Animator holsterMotionObject;

        public Effects effects;

        public Events events;

        [Tooltip("Used for weapons with custom shot method. Here, " +
                 "you can attach your scriptable objects and assign the method you want to call on shoot. " +
                 "Please only assign those scriptable objects that use custom shot methods, Otherwise it won´t work or you will run into issues.")]
        public CustomShotMethods[] customShot;

        public UnityEvent customMethod;

        public bool canShoot;

        public int currentWeapon;

        public WeaponIdentification id;

        public bool holding;

        [SerializeField] private UIController UIController;

#if UNITY_EDITOR
        public Preset crosshairPreset;
#endif

        [HideInInspector] public bool selectingWeapon;

        private float aimingCamShakeMultiplier, crouchingCamShakeMultiplier = 1;

        private float aimingOutSpeed;

        private float aimingSpeed;

        private Vector3 aimRot;

        private AudioClips audioSFX;

        private int bulletsPerFire;

        private float camShakeAmount;

        private float coolSpeed;

        private float damagePerBullet;

        private float evaluationProgress, evaluationProgressX;

        private Transform[] firePoint;

        private float fireRate;

        private AudioClip fireSFX;

        private RaycastHit hit;

        private ShooterInputControls inputActions;

        private GameObject muzzleVFX;

        private float penetrationAmount;

        public PerformShootStyle performShootStyle;

        private ReduceAmmo reduceAmmo;

        private Reload reload;

        private float reloadTime;

        private float spread;

        private PlayerStats stats;

        private WeaponAnimator weaponAnimator;

        public bool Reloading { get; set; }

        public Animator HolsterMotionObject => holsterMotionObject;

        public bool shooting { get; private set; }

        private void Start()
        {
            InitialSettings();
            CreateInventoryUI();
            GetInitialWeapons();
            inputActions = gameObject.GetComponent<PlayerMovement>().InputActions;
        }

        private void Update()
        {
            HandleUI();
            HandleAimingMotion();
            ManageWeaponMethodsInputs();
            HandleRecoil();
            HandleHeatRatio();
        }

        public void Aim()
        {
            if (GameManager.Instance.gameState != GameState.ActiveGame) return;

            isAiming = true;

            if (weapon.applyBulletSpread) spread = weapon.aimSpreadAmount;
            aimingCamShakeMultiplier = weapon.camShakeAimMultiplier;

            events.OnAiming.Invoke();

            var cameraDistance = mainCamera.nearClipPlane + weapon.aimDistance;
            var cameraCenter =
                mainCamera.ScreenToWorldPoint(new Vector3(Screen.width / 2f, Screen.height / 2f, cameraDistance));
            id.aimPoint.position = Vector3.Lerp(id.aimPoint.position, cameraCenter, aimingSpeed * Time.deltaTime);
            id.aimPoint.localRotation = Quaternion.Lerp(id.aimPoint.localRotation, Quaternion.Euler(aimRot),
                aimingSpeed * Time.deltaTime);
        }

        public void StopAim()
        {
            if (weapon != null && weapon.applyBulletSpread) spread = weapon.spreadAmount;

            isAiming = false;
            aimingCamShakeMultiplier = 1;

            if (id == null) return;
            id.aimPoint.localPosition = Vector3.Lerp(id.aimPoint.localPosition, id.originalAimPointPos,
                aimingSpeed * Time.deltaTime);
            id.aimPoint.localRotation = Quaternion.Lerp(id.aimPoint.localRotation,
                Quaternion.Euler(id.originalAimPointRot), aimingOutSpeed * Time.deltaTime);

            weaponHolder.localRotation = Quaternion.Lerp(weaponHolder.transform.localRotation,
                Quaternion.Euler(Vector3.zero), aimingOutSpeed * Time.deltaTime);
        }

        public void ForceAimReset()
        {
            isAiming = false;
            if (id?.aimPoint)
            {
                id.aimPoint.localPosition = id.originalAimPointPos;
                id.aimPoint.localRotation = Quaternion.Euler(id.originalAimPointRot);
            }

            weaponHolder.localRotation = Quaternion.Euler(Vector3.zero);
        }

        public void SetCrouchCamShakeMultiplier()
        {
            if (weapon)
                crouchingCamShakeMultiplier = weapon.camShakeCrouchMultiplier;
        }

        public void ResetCrouchCamShakeMultiplier()
        {
            crouchingCamShakeMultiplier = 1;
        }

        private void HandleAimingMotion()
        {
            aimingOutSpeed = weapon != null ? aimingSpeed : 2;
            if (isAiming && weapon != null)
                mainCamera.fieldOfView =
                    Mathf.Lerp(mainCamera.fieldOfView, weapon.aimingFOV, aimingSpeed * Time.deltaTime);
        }

        private void HandleHitscanProjectileShot()
        {
            if (!IsOwner || GameManager.Instance.gameState != GameState.ActiveGame) return;

            foreach (var p in firePoint)
            {
                canShoot = false;
                bulletsPerFire = weapon.bulletsPerFire;
                StartCoroutine(HandleShooting());

                if (!weapon.showBulletShells || (int)weapon.shootStyle == 2) continue;
                var bulletShell = Instantiate(weapon.bulletGraphics, p.position, mainCamera.transform.rotation);
                var shellRigidbody = bulletShell.GetComponent<Rigidbody>();
                var torque = Random.Range(-15f, 15f);
                var shellForce = mainCamera.transform.right * 5 + mainCamera.transform.up * 5;
                shellRigidbody.AddTorque(mainCamera.transform.right * torque, ForceMode.Impulse);
                shellRigidbody.AddForce(shellForce, ForceMode.Impulse);
            }

            if (weapon.timeBetweenShots == 0)
                soundMenago.PlaySound(fireSFX, 0, weapon.pitchVariationFiringSFX, true, 1, transform.position, false);

            Invoke(nameof(CanShoot), fireRate);
        }

        private void CustomShot()
        {
            if (!weapon.continuousFire)
            {
                canShoot = false;
                Invoke(nameof(CanShoot), fireRate);
            }

            customMethod?.Invoke();
        }

        private void SelectCustomShotMethod()
        {
            foreach (var t in customShot)
            {
                if (t.weapon != weapon) continue;
                customMethod = t.OnShoot;
                return;
            }

            Debug.LogError(
                "Appropriate weapon scriptable object not found in the custom shot array (under the events tab). Please, configure the weapon scriptable object and the suitable method to fix this error");
        }

        private IEnumerator HandleShooting()
        {
            var style = (int)weapon.shootStyle;
            weaponAnimator.StopWalkAndRunMotion();

            reduceAmmo?.Invoke();

            var i = 0;
            while (i < bulletsPerFire)
            {
                if (weapon == null) yield break;
                shooting = true;

                CamShake.ShootShake(camShakeAmount * aimingCamShakeMultiplier * crouchingCamShakeMultiplier);

                if (weapon.applyFOVEffectOnShooting)
                {
                    var fovAdjustment = isAiming ? weapon.AimingFOVValueToSubtract : weapon.FOVValueToSubtract;
                    mainCamera.fieldOfView -= fovAdjustment;
                }

                foreach (var p in firePoint)
                    if (muzzleVFX != null)
                        SendFireRPC(p.position, mainCamera.transform.rotation);

                CowsinsUtilities.ForcePlayAnim("shooting", inventory[currentWeapon].GetComponentInChildren<Animator>());
                if (weapon.timeBetweenShots != 0)
                    soundMenago.PlaySound(fireSFX, 0, weapon.pitchVariationFiringSFX, true, 1, transform.position,
                        false);

                ProgressRecoil();

                switch (style)
                {
                    case 0:
                        HitscanShot();
                        break;
                    case 1:
                        yield return new WaitForSeconds(weapon.shootDelay);
                        ProjectileShot();
                        break;
                }

                yield return new WaitForSeconds(weapon.timeBetweenShots);
                i++;
            }

            shooting = false;
        }

        [Rpc(SendTo.Everyone)]
        private void SendFireRPC(Vector3 position, Quaternion rotation)
        {
            SpawnVFX(position, rotation);
        }

        private void SpawnVFX(Vector3 position, Quaternion rotation)
        {
            Instantiate(muzzleVFX, position, rotation);
        }

        private void HitscanShot()
        {
            events.OnShoot.Invoke();
            if (resizeCrosshair && UIController.crosshair != null)
                UIController.crosshair.Resize(weapon.crosshairResize * 100);

            Transform hitObj;

            var dir = CowsinsUtilities.GetSpreadDirection(spread, mainCamera);
            var ray = new Ray(mainCamera.transform.position, dir);

            if (!Physics.Raycast(ray, out hit, weapon.bulletRange, hitLayer)) return;
            var dmg = damagePerBullet * stats.damageMultiplier;
            Hit(hit.collider.gameObject.layer, dmg, hit, true);
            hitObj = hit.collider.transform;

            var newRay = new Ray(hit.point, ray.direction);
            RaycastHit newHit;

            if (Physics.Raycast(newRay, out newHit, penetrationAmount, hitLayer))
                if (hitObj != newHit.collider.transform)
                {
                    var dmg_ = damagePerBullet * stats.damageMultiplier * weapon.penetrationDamageReduction;
                    Hit(newHit.collider.gameObject.layer, dmg_, newHit, true);
                }

            if (weapon.bulletTrail == null) return;

            foreach (var p in firePoint) SendTrailRPC(p.position, hit.point);
        }

        [Rpc(SendTo.Everyone)]
        private void SendTrailRPC(Vector3 startPosition, Vector3 hitPoint)
        {
            SpawnTrail(startPosition, hitPoint);
        }

        private void SpawnTrail(Vector3 startPosition, Vector3 hitPoint)
        {
            var trail = Instantiate(weapon.bulletTrail, startPosition, Quaternion.identity);
            StartCoroutine(MoveTrail(trail, startPosition, hitPoint));
        }

        private IEnumerator MoveTrail(TrailRenderer trail, Vector3 startPosition, Vector3 hitPoint)
        {
            float time = 0;

            while (time < 1)
            {
                trail.transform.position = Vector3.Lerp(startPosition, hitPoint, time);
                time += Time.deltaTime / trail.time;

                yield return null;
            }

            trail.transform.position = hitPoint;

            Destroy(trail.gameObject, trail.time);
        }

        private void ProjectileShot()
        {
            events.OnShoot.Invoke();
            if (resizeCrosshair && UIController.crosshair != null)
                UIController.crosshair.Resize(weapon.crosshairResize * 100);

            var ray = mainCamera.ViewportPointToRay(new Vector3(.5f, .5f, 0f));
            Vector3 destination = Physics.Raycast(ray, out hit) && !hit.transform.CompareTag("Player")
                ? destination = hit.point + CowsinsUtilities.GetSpreadDirection(weapon.spreadAmount, mainCamera)
                : destination = ray.GetPoint(50f) +
                                CowsinsUtilities.GetSpreadDirection(weapon.spreadAmount, mainCamera);

            foreach (var p in firePoint)
            {
                var bullet = Instantiate(weapon.projectile, p.position, p.transform.rotation);

                if (weapon.explosionOnHit) bullet.explosionVFX = weapon.explosionVFX;

                bullet.hurtsPlayer = weapon.hurtsPlayer;
                bullet.explosionOnHit = weapon.explosionOnHit;
                bullet.explosionRadius = weapon.explosionRadius;
                bullet.explosionForce = weapon.explosionForce;

                bullet.criticalMultiplier = weapon.criticalDamageMultiplier;
                bullet.destination = destination;
                bullet.player = transform;
                bullet.speed = weapon.speed;
                bullet.GetComponent<Rigidbody>().isKinematic = !weapon.projectileUsesGravity ? true : false;
                bullet.damage = damagePerBullet * stats.damageMultiplier;
                bullet.duration = weapon.bulletDuration;
            }
        }

        private void Hit(LayerMask layer, float damage, RaycastHit h, bool damageTarget)
        {
            events.OnHit.Invoke();
            switch (layer)
            {
                case int l when l == LayerMask.NameToLayer("MapBorder"):
                    break;
                case int l when l == LayerMask.NameToLayer("Player"):
                    break;
                default:
                    SpawnImpactRPC(h.point, h.normal,
                        h.collider != null
                            ? h.collider.transform.GetComponent<NetworkObject>()?.NetworkObjectId ?? 0
                            : 0);
                    break;
            }

            if (!damageTarget) return;

            var finalDamage = damage * GetDistanceDamageReduction(h.collider.transform);

            if (h.collider.gameObject.CompareTag("Critical"))
                CowsinsUtilities.GatherDamageableParent(h.collider.transform).DamageServerRpc(
                    finalDamage * weapon.criticalDamageMultiplier, true, true, NetworkManager.Singleton.LocalClientId);

            else if (h.collider.gameObject.CompareTag("BodyShot"))
                CowsinsUtilities.GatherDamageableParent(h.collider.transform).DamageServerRpc(finalDamage, false, true,
                    NetworkManager.Singleton.LocalClientId);

            else if (h.collider.GetComponent<IDamageable>() != null)
                h.collider.GetComponent<IDamageable>().DamageServerRpc(finalDamage, false);
        }

        [Rpc(SendTo.Everyone)]
        private void SpawnImpactRPC(Vector3 position, Vector3 normal, ulong parentNetworkObjectId)
        {
            if (weapon == null || weapon.bulletHoleImpact.groundIMpact == null) return;
            var impactBullet = Instantiate(weapon.bulletHoleImpact.groundIMpact, position,
                Quaternion.LookRotation(normal));

            if (parentNetworkObjectId == 0 ||
                !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(parentNetworkObjectId,
                    out var parentNetworkObject) ||
                parentNetworkObject == null) return;

            if (parentNetworkObject.transform != null && impactBullet != null)
                impactBullet.transform.SetParent(parentNetworkObject.transform);
        }

        private void CanShoot()
        {
            canShoot = true;
        }

        private void FinishedSelection()
        {
            selectingWeapon = false;
        }

        public void StartReload()
        {
            StartCoroutine(reload());
        }

        private IEnumerator DefaultReload()
        {
            soundMenago.PlaySound(
                id.bulletsLeftInMagazine == 0 ? weapon.audioSFX.emptyMagReload : weapon.audioSFX.reload, .1f, 0, true,
                1, transform.position);
            Reloading = true;
            yield return new WaitForSeconds(.001f);

            CowsinsUtilities.PlayAnim("reloading", inventory[currentWeapon].GetComponentInChildren<Animator>());

            yield return new WaitForSeconds(reloadTime);

            events.OnFinishReload.Invoke();

            canShoot = true;
            if (!Reloading) yield break;

            events.OnReload.Invoke();

            Reloading = false;

            if (!weapon.limitedMagazines)
            {
                id.bulletsLeftInMagazine = id.magazineSize;
            }
            else
            {
                if (id.totalBullets > id.magazineSize)
                {
                    id.totalBullets = id.totalBullets - (id.magazineSize - id.bulletsLeftInMagazine);
                    id.bulletsLeftInMagazine = id.magazineSize;
                }
                else if (id.totalBullets == id.magazineSize)
                {
                    id.totalBullets = id.totalBullets - (id.magazineSize - id.bulletsLeftInMagazine);
                    id.bulletsLeftInMagazine = id.magazineSize;
                }
                else if (id.totalBullets < id.magazineSize)
                {
                    var bulletsLeft = id.bulletsLeftInMagazine;
                    if (id.bulletsLeftInMagazine + id.totalBullets <= id.magazineSize)
                    {
                        id.bulletsLeftInMagazine = id.bulletsLeftInMagazine + id.totalBullets;
                        if (id.totalBullets - (id.magazineSize - bulletsLeft) >= 0)
                            id.totalBullets = id.totalBullets - (id.magazineSize - bulletsLeft);
                        else id.totalBullets = 0;
                    }
                    else
                    {
                        var ToAdd = id.magazineSize - id.bulletsLeftInMagazine;
                        id.bulletsLeftInMagazine = id.bulletsLeftInMagazine + ToAdd;
                        if (id.totalBullets - ToAdd >= 0) id.totalBullets = id.totalBullets - ToAdd;
                        else id.totalBullets = 0;
                    }
                }
            }
        }

        private IEnumerator OverheatReload()
        {
            canShoot = false;

            var waitTime = weapon.cooledPercentageAfterOverheat;

            CancelInvoke(nameof(CanShoot));

            yield return new WaitUntil(() => id.heatRatio <= waitTime);

            events.OnFinishReload.Invoke();

            Reloading = false;
            canShoot = true;
        }

        private void ReduceDefaultAmmo()
        {
            if (!weapon.infiniteBullets)
            {
                id.bulletsLeftInMagazine -= weapon.ammoCostPerFire;
                if (id.bulletsLeftInMagazine < 0) id.bulletsLeftInMagazine = 0;
            }
        }

        private void ReduceOverheatAmmo()
        {
            id.heatRatio += 1f / id.magazineSize;
        }

        private void HandleHeatRatio()
        {
            if (weapon == null || id.magazineSize == 0 || weapon.reloadStyle == ReloadingStyle.defaultReload) return;

            if (id.heatRatio > 0) id.heatRatio -= Time.deltaTime * coolSpeed;
            if (id.heatRatio > 1) id.heatRatio = 1;
        }

        public void UnHolster(GameObject weaponObj, bool playAnim)
        {
            canShoot = true;

            weaponObj.SetActive(true);
            id = weaponObj.GetComponent<WeaponIdentification>();

            switch ((int)weapon.shootStyle)
            {
                case 0:
                    performShootStyle = HandleHitscanProjectileShot;
                    break;
                case 1:
                    performShootStyle = HandleHitscanProjectileShot;
                    break;
                case 3:
                    performShootStyle = CustomShot;
                    break;
            }

            weaponObj.GetComponentInChildren<Animator>().enabled = true;
            if (playAnim)
                CowsinsUtilities.PlayAnim("unholster", inventory[currentWeapon].GetComponentInChildren<Animator>());

            Invoke("FinishedSelection", .5f);

            if (weapon.shootStyle == ShootStyle.Custom) SelectCustomShotMethod();
            else customMethod = null;

            GetAttachmentsModifiers();

            weaponAnimator.StopWalkAndRunMotion();

            if (weapon.reloadStyle == ReloadingStyle.defaultReload)
            {
                reload = DefaultReload;
                reduceAmmo = ReduceDefaultAmmo;
            }
            else
            {
                reload = OverheatReload;
                reduceAmmo = ReduceOverheatAmmo;
                coolSpeed = weapon.coolSpeed;
            }

            firePoint = inventory[currentWeapon].FirePoint;

            if (weapon.infiniteBullets || weapon.reloadStyle == ReloadingStyle.Overheat)
                UIController.DetectReloadMethod(false, !weapon.infiniteBullets);
            else
                UIController.DetectReloadMethod(true, false);

            if ((int)weapon.shootStyle == 2) UIController.DetectReloadMethod(false, false);

            UIController.SetWeaponDisplay(weapon);
        }

        private void HandleUI()
        {
            if (weapon == null)
            {
                UIController.DisableWeaponUI();
                return;
            }

            UIController.EnableDisplay();

            if (weapon.reloadStyle == ReloadingStyle.defaultReload)
            {
                if (!weapon.infiniteBullets)
                {
                    var activeReloadUI = id.bulletsLeftInMagazine == 0 && !autoReload && !weapon.infiniteBullets;
                    var activeLowAmmoUI = id.bulletsLeftInMagazine < id.magazineSize / 3.5f &&
                                          id.bulletsLeftInMagazine > 0;

                    if (weapon.limitedMagazines)
                        UIController.UpdateBullets(id.bulletsLeftInMagazine, id.totalBullets, activeReloadUI,
                            activeLowAmmoUI);
                    else
                        UIController.UpdateBullets(id.bulletsLeftInMagazine, id.magazineSize, activeReloadUI,
                            activeLowAmmoUI);
                }
                else
                {
                    UIController.UpdateBullets(id.bulletsLeftInMagazine, id.totalBullets, false, false);
                }
            }
            else
            {
                UIController.UpdateHeatRatio(id.heatRatio);
            }

            if (UIController.crosshair == null) return;

            RaycastHit hit_;
            if ((Physics.Raycast(mainCamera.transform.position, mainCamera.transform.forward, out hit_,
                    weapon.bulletRange) && hit_.transform.CompareTag("Enemy")) ||
                (Physics.Raycast(mainCamera.transform.position, mainCamera.transform.forward, out hit_,
                    weapon.bulletRange) && hit_.transform.CompareTag("Critical")))
                UIController.crosshair.SpotEnemy(true);
            else UIController.crosshair.SpotEnemy(false);
        }

        private void CreateInventoryUI()
        {
            slots = new UISlot[inventorySize];
            var j = 0;
            while (j < inventorySize)
            {
                var slot = Instantiate(UISlotPrefab, Vector3.zero, Quaternion.identity,
                    UIController.inventoryContainer.transform);
                slot.GetComponent<UISlot>().id = j;
                slots[j] = slot.GetComponent<UISlot>();
                j++;
            }
        }

        public void HandleInventory()
        {
            if (inputActions.Player.Reloading.IsPressed()) return;
            if (inputActions.Player.Scrolling.ReadValue<Vector2>().y > 0 ||
                (inputActions.Player.ChangeWeapons.WasPressedThisFrame() &&
                 inputActions.Player.ChangeWeapons.ReadValue<float>() < 0))
            {
                ForceAimReset();
                if (currentWeapon < inventorySize - 1)
                {
                    currentWeapon++;
                    SelectWeapon();
                }
            }

            if (inputActions.Player.Scrolling.ReadValue<Vector2>().y < 0 ||
                (inputActions.Player.ChangeWeapons.WasPressedThisFrame() &&
                 inputActions.Player.ChangeWeapons.ReadValue<float>() > 0))
            {
                ForceAimReset();
                if (currentWeapon > 0)
                {
                    currentWeapon--;
                    SelectWeapon();
                }
            }
        }

        public void SelectWeapon()
        {
            canShoot = false;
            selectingWeapon = true;
            UIController.crosshair.SpotEnemy(false);
            events.OnInventorySlotChanged.Invoke();
            weapon = null;

            foreach (var weapon_ in inventory)
                if (weapon_ != null)
                {
                    weapon_.gameObject.SetActive(false);
                    weapon_.GetComponentInChildren<Animator>().enabled = false;
                    if (weapon_ == inventory[currentWeapon])
                    {
                        weapon = inventory[currentWeapon].weapon;

                        weapon_.GetComponentInChildren<Animator>().enabled = true;
                        UnHolster(weapon_.gameObject, true);

#if UNITY_EDITOR
                        UIController.crosshair.GetComponent<CrosshairShape>().currentPreset = weapon.crosshairPreset;
                        CowsinsUtilities.ApplyPreset(
                            UIController.crosshair.GetComponent<CrosshairShape>().currentPreset,
                            UIController.crosshair.GetComponent<CrosshairShape>());
#endif
                    }
                }

            foreach (var slot in slots)
            {
                slot.transform.localScale = slot.initScale;
                slot.GetComponent<CanvasGroup>().alpha = .2f;
            }

            slots[currentWeapon].transform.localScale = slots[currentWeapon].transform.localScale * 1.2f;
            slots[currentWeapon].GetComponent<CanvasGroup>().alpha = 1;

            CancelInvoke(nameof(CanShoot));

            events.OnEquipWeapon.Invoke();
        }

        private void GetInitialWeapons()
        {
            if (initialWeapons.Length == 0) return;

            var i = 0;
            while (i < initialWeapons.Length)
            {
                InstantiateWeapon(initialWeapons[i], i, null, null);
                i++;
            }

            weapon = initialWeapons[0];
        }

        public void InstantiateWeapon(Weapon_SO newWeapon, int inventoryIndex, int? _bulletsLeftInMagazine,
            int? _totalBullets)
        {
            var weaponPicked = Instantiate(newWeapon.weaponObject, weaponHolder);
            weaponPicked.transform.localPosition = newWeapon.weaponObject.transform.localPosition;

            if (inventory[inventoryIndex] != null) Destroy(inventory[inventoryIndex].gameObject);
            inventory[inventoryIndex] = weaponPicked;

            if (inventoryIndex == currentWeapon)
                weapon = newWeapon;
            else weaponPicked.gameObject.SetActive(false);

            inventory[inventoryIndex].bulletsLeftInMagazine = _bulletsLeftInMagazine ?? newWeapon.magazineSize;
            inventory[inventoryIndex].totalBullets = _totalBullets ??
                                                     (newWeapon.limitedMagazines
                                                         ? newWeapon.magazineSize * newWeapon.totalMagazines
                                                         : newWeapon.magazineSize);

            slots[inventoryIndex].weapon = newWeapon;
            slots[inventoryIndex].GetImage();
#if UNITY_EDITOR
            UIController.crosshair.GetComponent<CrosshairShape>().currentPreset = weapon?.crosshairPreset;
            if (UIController.crosshair.GetComponent<CrosshairShape>().currentPreset)
                CowsinsUtilities.ApplyPreset(UIController.crosshair.GetComponent<CrosshairShape>().currentPreset,
                    UIController.crosshair.GetComponent<CrosshairShape>());
#endif

            if (weapon?.shootStyle == ShootStyle.Custom) SelectCustomShotMethod();
            else customMethod = null;

            if (inventoryIndex == currentWeapon) SelectWeapon();
        }

        private float GetDistanceDamageReduction(Transform target)
        {
            if (!weapon.applyDamageReductionBasedOnDistance) return 1;
            if (Vector3.Distance(target.position, transform.position) > weapon.minimumDistanceToApplyDamageReduction)
                return weapon.minimumDistanceToApplyDamageReduction /
                    Vector3.Distance(target.position, transform.position) * weapon.damageReductionMultiplier;
            return 1;
        }

        private void ManageWeaponMethodsInputs()
        {
            if (!inputActions.Player.Firing.IsPressed()) holding = false;
        }

        private void HandleRecoil()
        {
            if (weapon == null || !weapon.applyRecoil)
            {
                cameraPivot.localRotation = Quaternion.Lerp(cameraPivot.localRotation, Quaternion.Euler(Vector3.zero),
                    3 * Time.deltaTime);
                return;
            }

            var speed = weapon == null ? 10 : weapon.recoilRelaxSpeed * 3;
            if (!inputActions.Player.Firing.IsPressed() || Reloading || !PlayerStats.Controllable)
            {
                cameraPivot.localRotation = Quaternion.Lerp(cameraPivot.localRotation, Quaternion.Euler(Vector3.zero),
                    speed * Time.deltaTime);
                evaluationProgress = 0;
                evaluationProgressX = 0;
            }

            if (weapon == null || Reloading || !PlayerStats.Controllable) return;

            if (inputActions.Player.Firing.IsPressed())
            {
                var xamount = weapon.applyDifferentRecoilOnAiming && isAiming
                    ? weapon.xRecoilAmountOnAiming
                    : weapon.xRecoilAmount;
                var yamount = weapon.applyDifferentRecoilOnAiming && isAiming
                    ? weapon.yRecoilAmountOnAiming
                    : weapon.yRecoilAmount;

                cameraPivot.localRotation = Quaternion.Lerp(cameraPivot.localRotation,
                    Quaternion.Euler(new Vector3(-weapon.recoilY.Evaluate(evaluationProgress) * yamount,
                        -weapon.recoilX.Evaluate(evaluationProgressX) * xamount, 0)), 10 * Time.deltaTime);
            }
        }

        private void ProgressRecoil()
        {
            if (weapon.applyRecoil)
            {
                evaluationProgress += 1f / weapon.magazineSize;
                evaluationProgressX += 1f / weapon.magazineSize;
            }
        }

        private void InitialSettings()
        {
            stats = GetComponent<PlayerStats>();
            weaponAnimator = GetComponent<WeaponAnimator>();
            inventory = new WeaponIdentification[inventorySize];
            currentWeapon = 0;
            canShoot = true;
            mainCamera.fieldOfView = GetComponent<PlayerMovement>().normalFOV;
        }

        private void GetAttachmentsModifiers()
        {
            fireSFX = weapon.audioSFX.shooting[Random.Range(0, weapon.audioSFX.shooting.Length - 1)];

            aimRot = weapon.aimingRotation;
            muzzleVFX = weapon.muzzleVFX;

            var baseReloadTime = weapon.reloadTime;
            reloadTime = baseReloadTime;

            var baseAimSpeed = weapon.aimingSpeed;
            aimingSpeed = baseAimSpeed;

            var baseFireRate = weapon.fireRate;
            fireRate = baseFireRate;

            var baseDamagePerBullet = weapon.damagePerBullet;
            damagePerBullet = baseDamagePerBullet;

            var baseCamShakeAmount = weapon.camShakeAmount;
            camShakeAmount = baseCamShakeAmount;

            var basePenetrationAmount = weapon.penetrationAmount;
            penetrationAmount = basePenetrationAmount;
        }

        private delegate IEnumerator Reload();

        private delegate void ReduceAmmo();
    }
}