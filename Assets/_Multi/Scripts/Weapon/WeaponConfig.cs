using System;
using UnityEngine;

[Serializable]
public struct WeaponConfig {

    public string title;

    public WeaponType weaponType;
    public WeaponGrip weaponGrip;

    public float damage;
    public float bulletSpeed;
    public float fireRate;

    [Range(0, 1)] public float accuracyRange;

    public Transform bulletPrefab;
    public Transform muzzleFlashPrefab;
    public bool useLocalBullets;
}