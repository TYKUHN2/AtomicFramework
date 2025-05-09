namespace AtomicFramework
{
    public readonly struct NetworkMessage(byte[] data, ulong player)
    {
        public readonly byte[] data = data;
        public readonly ulong player = player;
    }
}
