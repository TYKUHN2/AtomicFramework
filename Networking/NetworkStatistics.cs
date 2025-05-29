using Steamworks;
using System;

namespace AtomicFramework
{
    /// <summary>
    /// Represents statistics of a connection to a player.
    /// </summary>
    /// <remarks>
    /// Different channels may see different statistics.
    /// </remarks>
    /// <seealso cref="NetworkChannel.GetStatistics"/>
    public readonly struct NetworkStatistics(SteamNetConnectionRealTimeStatus_t status)
    {
        /// <summary>
        /// The ping in milliseconds of the connection.
        /// </summary>
        public readonly int ping = status.m_nPing;

        /// <summary>
        /// The fraction of packets lost in either direction on the connection.
        /// </summary>
        public readonly float packetLoss = 1 - Math.Min(status.m_flConnectionQualityLocal, status.m_flConnectionQualityRemote);

        /// <summary>
        /// The estimated bandwidth of the connection.
        /// </summary>
        public readonly int bandwidth = status.m_nSendRateBytesPerSecond;

        /// <summary>
        /// The number of packets queued or unacknowledged on the connection.
        /// </summary>
        /// <remarks>
        /// As this climbs you are saturating the connection. As this falls, the connection desaturating.
        /// </remarks>
        public readonly int inFlight = status.m_cbPendingUnreliable + status.m_cbPendingReliable + status.m_cbSentUnackedReliable;
    }
}
