using System;

namespace AtomicFramework
{
    internal abstract class IDiscoveryHandler
    {
        internal string[] Mods
        {
            get; private protected set;
        } = [];

        internal event Action<byte[]>? OnOutbound;
        internal event Action<string, ushort, ushort>? OnDiscovery;
        internal event Action<string[]>? OnRequired;
        internal event Action? OnMods;

        internal abstract void Receive(NetworkMessage message);
        internal abstract void GetPort(string GUID, int channel);
        internal abstract void GetRequired();

        internal virtual void Ready()
        {}

        protected void PushMessage(byte[] data)
        {
            OnOutbound?.Invoke(data);
        }

        protected void PushDiscovery(string GUID, ushort channel, ushort port)
        {
            OnDiscovery?.Invoke(GUID, channel, port);
        }

        protected void NotifyMods()
        {
            OnMods?.Invoke();
        }

        protected void PushRequired(string[] required)
        {
            OnRequired?.Invoke(required);
        }
    }
}
