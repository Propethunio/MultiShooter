/// <summary>
/// This script belongs to cowsins� as a part of the cowsins� FPS Engine. All rights reserved. 
/// </summary>using UnityEngine;

using HEAVYART.TopDownShooter.Netcode;
using UnityEngine;

namespace cowsins
{
    public class Bullet : MonoBehaviour
    {
        [HideInInspector] public GameObject explosionVFX;
        [HideInInspector] public Transform player;
        [HideInInspector] public Vector3 destination;
        [HideInInspector] public float speed;
        [HideInInspector] public float damage;
        [HideInInspector] public bool gravity;
        [HideInInspector] public float duration;
        [HideInInspector] public float explosionForce;
        [HideInInspector] public float criticalMultiplier;
        [HideInInspector] public float explosionRadius;
        [HideInInspector] public bool explosionOnHit;
        [HideInInspector] public bool hurtsPlayer;

        private bool projectileHasAlreadyHit = false; // Prevent from double hitting issues

        private void Start()
        {
            transform.LookAt(destination);
            Invoke(nameof(DestroyProjectile), duration);
        }

        private void Update()
        {
            transform.Translate(0.0f, 0.0f, speed * Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (projectileHasAlreadyHit) return;

            if (other.CompareTag("Critical"))
            {
                DamageTarget(other.transform, damage * criticalMultiplier, true);
            }
            else if (other.CompareTag("BodyShot"))
            {
                DamageTarget(other.transform, damage, false);
            }
            else if (other.GetComponent<IDamageable>() != null && !other.CompareTag("Player"))
            {
                DamageTarget(other.transform, damage, false);
            }
            else if (IsGroundOrObstacleLayer(other.gameObject.layer))
            {
                DestroyProjectile();
            }
        }

        private void DamageTarget(Transform target, float dmg, bool isCritical)
        {
            var damageable = CowsinsUtilities.GatherDamageableParent(target);
            if (damageable == null) return;
            damageable.DamageServerRpc(dmg, isCritical);
            projectileHasAlreadyHit = true;
            DestroyProjectile();
        }

        private static bool IsGroundOrObstacleLayer(int layer)
        {
            return layer == LayerMask.NameToLayer("Ground") || layer == LayerMask.NameToLayer("Object")
                                                            || layer == LayerMask.NameToLayer("Grass") ||
                                                            layer == LayerMask.NameToLayer("Metal") ||
                                                            layer == LayerMask.NameToLayer("Mud") ||
                                                            layer == LayerMask.NameToLayer("Wood") ||
                                                            layer == LayerMask.NameToLayer("Enemy");
        }

        private void DestroyProjectile()
        {
            if (explosionOnHit)
            {
                if (explosionVFX != null)
                {
                    var contact = GetComponent<Collider>().ClosestPoint(transform.position);
                    Instantiate(explosionVFX, contact, Quaternion.identity);
                }

                var colliders = Physics.OverlapSphere(transform.position, explosionRadius);

                foreach (var collider in colliders)
                {
                    var damageable = collider.GetComponent<IDamageable>();
                    var rigidbody = collider.GetComponent<Rigidbody>();

                    if (damageable != null)
                    {
                        // Calculate the distance ratio and damage based on the explosion radius
                        var distanceRatio =
                            1 - Mathf.Clamp01(Vector3.Distance(collider.transform.position, transform.position) /
                                              explosionRadius);
                        var dmg = damage * distanceRatio;

                        // Apply damage if the collider is a player and the explosion should hurt the player
                        if (collider.CompareTag("Player") && hurtsPlayer)
                        {
                            damageable.DamageServerRpc(dmg, false);
                        }
                        // Apply damage if the collider is not a player
                        else if (!collider.CompareTag("Player"))
                        {
                            damageable.DamageServerRpc(dmg, false);
                        }
                    }

                    if (rigidbody != null && collider != this)
                    {
                        rigidbody.AddExplosionForce(explosionForce, transform.position, explosionRadius, 5,
                            ForceMode.Force);
                    }
                }
            }

            Destroy(gameObject);
        }
    }
}