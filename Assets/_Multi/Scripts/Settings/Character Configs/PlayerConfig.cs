using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PlayerConfig {

    public string label;

    public GameObject playerPrefab;
    public float movementSpeed = 5;
    public float health;

    [Space()]
    public float dropChance;
    public List<PickUpItemController> dropElements;
}