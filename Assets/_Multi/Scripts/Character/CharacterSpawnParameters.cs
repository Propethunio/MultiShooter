using System;
using Unity.Netcode;
using UnityEngine;

public class CharacterSpawnParameters : INetworkSerializable, IEquatable<CharacterSpawnParameters> {

    public ulong ownerID;
    public string name = string.Empty;
    public Color color;
    public int modelIndex;
    //Add custom parameters here

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
        serializer.SerializeValue(ref ownerID);
        serializer.SerializeValue(ref name);
        serializer.SerializeValue(ref color);
        serializer.SerializeValue(ref modelIndex);
        //And serialize here
    }

    public bool Equals(CharacterSpawnParameters other) {
        return ownerID == other.ownerID;
    }
}