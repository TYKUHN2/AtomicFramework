namespace AtomicFramework
{
    /// <summary>
    /// Struct representing and inbound network message.
    /// </summary>
    /// <param name="data">Data sent within the message.</param>
    /// <param name="player">SteamID of the player that sent the message.</param>
    public readonly struct NetworkMessage(byte[] data, ulong player)
    {
        public readonly byte[] data = data;
        public readonly ulong player = player;
    }
}
