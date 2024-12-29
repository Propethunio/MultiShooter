using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace HEAVYART.TopDownShooter.Netcode
{
    public class CharacterSpawnParameters : INetworkSerializable, IEquatable<CharacterSpawnParameters>
    {
        public ulong OwnerID;
        public string Name = string.Empty;
        public Color Color;
        public int ModelIndex;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref OwnerID);
            serializer.SerializeValue(ref Name);
            serializer.SerializeValue(ref Color);
            serializer.SerializeValue(ref ModelIndex);
        }

        public bool Equals(CharacterSpawnParameters other)
        {
            return other != null && OwnerID == other.OwnerID;
        }
    }
}
