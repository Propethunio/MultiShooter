using UnityEngine;

[CreateAssetMenu(fileName = "InstantHeal", menuName = "Modifier Container/Instant Heal")]
public class InstantHealContainer : ModifierContainerBase {

    public float health;

    public override ModifierBase GetConfig() {
        return new InstantHeal() {
            health = health
        };
    }
}