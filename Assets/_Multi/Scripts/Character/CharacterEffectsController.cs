using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HEAVYART.TopDownShooter.Netcode
{
    public class CharacterEffectsController : NetworkBehaviour
    {
        public float delayBeforeDrop = 1;
        public float delayBeforeStartMoving = 2;
        public float moveDownTime = 2;

        public void RunDestroyScenario(bool stopCameraMovement)
        {
            StartCoroutine(ProcessCharacterDestroySteps(stopCameraMovement));
        }

        private IEnumerator ProcessCharacterDestroySteps(bool stopCameraMovement)
        {
            GameManager.Instance.userControl.RemoveNetworkObject(GetComponent<NetworkObject>());
            GetComponent<CapsuleCollider>().enabled = false;

            yield return new WaitForSeconds(delayBeforeDrop);
            GetComponent<DropController>().Drop();

            yield return new WaitForSeconds(delayBeforeStartMoving);

            if (stopCameraMovement)
                if (GameManager.Instance.userControl == null)
                    if (Camera.main != null)
                        Camera.main.GetComponent<GameCameraController>().StopCameraMovement();

            while (moveDownTime > 0)
            {
                if (IsOwner)
                    transform.position += Vector3.down * Time.deltaTime;

                moveDownTime -= Time.deltaTime;
                yield return 0;
            }

            if (IsServer)
            {
                NetworkObject.Despawn();
            }
        }
    }
}
