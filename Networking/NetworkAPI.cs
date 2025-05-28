using System.Collections.Generic;
using System.Threading;

namespace AtomicFramework
{
    /// <summary>Safe cooperative networking features.</summary>
    public class NetworkAPI
    {
        internal Dictionary<ushort, NetworkChannel> channels = [];
        private readonly SemaphoreSlim listenLock = new(1);

        internal readonly string GUID;

        internal NetworkAPI(string GUID)
        {
            this.GUID = GUID;
        }

        /// <summary>
        /// Opens a new virtual channel.
        /// </summary>
        /// <remarks>
        /// Returns existing channel if already opened.
        /// </remarks>
        /// <param name="channel">ID of virtual channel</param>
        /// <returns>Virtual channel of ID</returns>
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

        /// <summary>
        /// Closes a virtual channel.
        /// </summary>
        /// <param name="channel">ID of virtual channel</param>
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
