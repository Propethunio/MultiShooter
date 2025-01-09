using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

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

        private WeaponConfig weaponConfig;


        private void Awake()
        {
            weaponConfig = SettingsManager.Instance.weapon.GetWeaponConfig(weaponType);
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
