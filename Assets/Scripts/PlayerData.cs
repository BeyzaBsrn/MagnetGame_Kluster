using System;
using Unity.Netcode;
using Unity.Collections;

public struct PlayerData : INetworkSerializable, IEquatable<PlayerData>
{
    public ulong ClientId;
    public FixedString32Bytes PlayerName;
    public int PlayerIndex;   // 0-3 arasi
    public int KalanTas;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerName);
        serializer.SerializeValue(ref PlayerIndex);
        serializer.SerializeValue(ref KalanTas);
    }

    public bool Equals(PlayerData other) => ClientId == other.ClientId;
}
