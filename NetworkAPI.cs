using System.Collections.Generic;
using System.Threading;

namespace AtomicFramework
{
    public class NetworkAPI
    {
        internal Dictionary<ushort, NetworkChannel> channels = [];
        private readonly SemaphoreSlim listenLock = new(1);

        internal readonly string GUID;

        internal NetworkAPI(string GUID)
        {
            this.GUID = GUID;
        }

        public NetworkChannel OpenChannel(ushort channel)
        {
            if (channels.ContainsKey(channel))
                return channels[channel];

            NetworkChannel chan = NetworkingManager.instance!.OpenListen(GUID, channel);

            listenLock.Wait();
            channels[channel] = chan;
            listenLock.Release();

            return chan;
        }

        public void CloseChannel(ushort channel)
        {
            listenLock.Wait();

            if (channels.TryGetValue(channel, out NetworkChannel chan))
            {
                chan.Close();
                channels.Remove(channel);
            }

            listenLock.Release();
        }
    }
}
