using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KillUI : MonoBehaviour {

    [SerializeField] TextMeshProUGUI killerText;
    [SerializeField] TextMeshProUGUI killedText;
    [SerializeField] Image icon;
    [SerializeField] Sprite skullIcon;

    public void Setup(string killed, bool selfKill, string killer = "") {
        if (selfKill) { 
            icon.sprite = skullIcon;
        }

        killerText.text = killer;
        killedText.text = killed;
    }
}