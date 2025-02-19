using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HEAVYART.TopDownShooter.Netcode
{
    public class SettingsManager : Singleton<SettingsManager>
    {
        public CommonSettings common;
        public LobbySettings lobby;
        public PlayerSettings player;
        public WeaponSettings weapon;
        public GameplaySettings gameplay;

        void Awake()
        {
            DontDestroyOnLoad(this);
        }
    }
}
