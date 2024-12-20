using HEAVYART.TopDownShooter.Netcode;
using UnityEngine;

namespace cowsins
{
    public class CameraFOVManager : MonoBehaviour
    {
        [SerializeField] private Rigidbody player;

        private float baseFOV;
        private Camera cam;
        private PlayerMovement movement;
        private WeaponController weapon;

        private void Start()
        {
            cam = GetComponent<Camera>();
            movement = player.GetComponent<PlayerMovement>();
            weapon = player.GetComponent<WeaponController>();

            baseFOV = movement.normalFOV;
            cam.fieldOfView = baseFOV;
        }

        private void Update()
        {
            if (weapon.isAiming && weapon.weapon != null)
                return;

            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, baseFOV, Time.deltaTime * movement.fadeInFOVAmount);
        }
    }
}