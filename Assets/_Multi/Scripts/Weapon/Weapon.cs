using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HEAVYART.TopDownShooter.Netcode
{
    public class Weapon : NetworkBehaviour
    {
        public WeaponType weaponType;

        [Space()]
        public WeaponModelTransformKeeper weaponModelTransformKeeper;

        [Space()]
        public Transform targetingTransform;
        public List<Transform> gunDirectionTransforms = new List<Transform>();

        public WeaponGrip weaponGrip => weaponConfig.weaponGrip;

        public Action OnFire;

        private WeaponConfig weaponConfig;
        private float lastFireTime = 0;

        private CharacterIdentityControl identityControl;
        private ModifiersControlSystem modifiersControlSystem;

        private void Awake()
        {
            weaponConfig = SettingsManager.Instance.weapon.GetWeaponConfig(weaponType);
            identityControl = transform.root.GetComponent<CharacterIdentityControl>();
            modifiersControlSystem = transform.root.GetComponent<ModifiersControlSystem>();
        }

        public void Fire()
        {
            float currentFireRate = weaponConfig.fireRate * modifiersControlSystem.CalculateFireRateMultiplier();

            //Wait for next fire
            if (lastFireTime + currentFireRate < Time.time)
            {
                //Create and send bullet for every "gun" in weapon (could be few "guns" in a shotgun)
                for (int i = 0; i < gunDirectionTransforms.Count; i++)
                {
                    BulletParameters bulletParameters = new BulletParameters();

                    //Bullet owner. defaultOwnerID if it's bot. Requires to register scores in leaderboard.
                    bulletParameters.ownerID = identityControl.isBot ? SettingsManager.Instance.ai.defaultOwnerID : OwnerClientId;

                    //Current character (scene object) ID.
                    bulletParameters.senderID = NetworkObjectId;

                    bulletParameters.gunIndex = (byte)i;
                    bulletParameters.speed = weaponConfig.bulletSpeed;
                    bulletParameters.startTime = NetworkManager.Singleton.ServerTime.Time;
                    bulletParameters.startPosition = transform.position;

                    //Set bullet direction according to accuracy settings and active modifiers
                    float range = (1f - weaponConfig.accuracyRange) * modifiersControlSystem.CalculateAccuracyMultiplier();
                    Vector3 accuracyOffset = new Vector3(Random.Range(-range, range), 0, Random.Range(-range, range));
                    bulletParameters.direction = (gunDirectionTransforms[i].forward + accuracyOffset).normalized;

                    //Add instant damage modifier (command)
                    bulletParameters.AddModifier(new InstantDamage() { damage = weaponConfig.damage });

                    //Broadcast this message to clients
                    SendFireRPC(bulletParameters);

                    lastFireTime = Time.time;
                }
            }
        }

        [Rpc(SendTo.Everyone)]
        private void SendFireRPC(BulletParameters bulletParameters)
        {
            //Receive and apply
            int gunIndex = bulletParameters.gunIndex;

            //Play fire animation
            OnFire?.Invoke();

            //Process ammo spawn
            Transform instantiatedBullet = Instantiate(weaponConfig.bulletPrefab, weaponModelTransformKeeper.firePointTransform.position, gunDirectionTransforms[gunIndex].rotation);
            instantiatedBullet.GetComponent<Bullet>().Initialize(bulletParameters, weaponModelTransformKeeper.firePointTransform, weaponConfig.muzzleFlashPrefab);
        }

        public void ShowWeapon()
        {
            weaponModelTransformKeeper.weaponModel.gameObject.SetActive(true);
        }

        public void HideWeapon()
        {
            weaponModelTransformKeeper.weaponModel.gameObject.SetActive(false);
        }
    }
}
