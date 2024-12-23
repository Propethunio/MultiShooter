/// <summary>
/// This script belongs to cowsins� as a part of the cowsins� FPS Engine. All rights reserved. 
/// </summary>

namespace cowsins
{
    /// <summary>
    /// Basically used for Player and enemies, which can be hit
    /// </summary>
    public interface IDamageable
    {
        void DamageServerRpc(float damage, bool isHeadshot, bool byOhterPlayer = false, ulong killerID = 0);
    }
}