using System.Collections.Generic;
using UnityEngine;

public class AISettings : MonoBehaviour {

    public List<AIConfig> configs = new List<AIConfig>();

    public ulong defaultOwnerID => 1000;
}