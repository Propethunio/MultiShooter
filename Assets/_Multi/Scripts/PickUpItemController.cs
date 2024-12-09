using Unity.Netcode;
using UnityEngine;

public class PickUpItemController : NetworkBehaviour {

    public ModifierContainerBase container;

    private void OnTriggerEnter(Collider other) {
        if(NetworkManager.Singleton.IsServer == false) return;

        CommandReceiver commandReceiver = other.GetComponent<CommandReceiver>();

        if(commandReceiver != null) {
            commandReceiver.ReceiveModifiersRpc(new ModifierBase[] { container.GetConfig() }, 0, NetworkManager.Singleton.ServerTime.Time);
            NetworkObject.Despawn(true);
        }
    }
}