using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AtomicFramework
{
    /// <summary>
    /// Virtual network channel
    /// </summary>
    public class NetworkChannel
    {
        internal readonly HSteamListenSocket socket;

        private readonly Dictionary<ulong, HSteamNetConnection> connections = [];

        /// <summary>
        /// Fired when a message is received on the channel.
        /// </summary>
        public event Action<NetworkMessage>? OnMessage;

        /// <summary>
        /// Fired when a player connects to this channel.
        /// </summary>
        public event Action<ulong>? OnConnected;

        /// <summary>
        /// Fired when a player disconnects from this channel.
        /// </summary>
        public event Action<ulong>? OnDisconnect;

        /// <summary>
        /// Fired when this channel fails to connect to a player.
        /// </summary>
        public event Action<ulong, bool>? OnConnectionFailed;

        /// <summary>
        /// Delegate type for inbound connection filters.
        /// </summary>
        /// <param name="player">SteamID of player trying to connect.</param>
        /// <returns>Whether to accept the connection.</returns>
        public delegate bool ConnectionFilter(ulong player);

        /// <summary>
        /// Called when this channel receives a new connection.
        /// </summary>
        public ConnectionFilter? OnConnection;

        private readonly string GUID;

        /// <summary>
        /// ID of this virtual channel.
        /// </summary>
        public readonly ushort channel;

        internal NetworkChannel(HSteamListenSocket socket, string GUID, ushort channel)
        {
            this.socket = socket;
            this.GUID = GUID;
            this.channel = channel;
        }

        /// <summary>
        /// Send data on this channel to a given player.
        /// </summary>
        /// <param name="player">SteamID of player to send to.</param>
        /// <param name="message">Data to send.</param>
        /// <exception cref="IOException">Player is not connected.</exception>
        public void Send(ulong player, byte[] message)
        {
            if (connections.TryGetValue(player, out HSteamNetConnection conn))
            {
                unsafe
                {
                    fixed (byte* buf = message)
                    {
                        SteamNetworkingSockets.SendMessageToConnection(conn, (IntPtr)buf, (uint)message.Length, 8, out _);
                    }
                }
            }
            else
                throw new IOException();
        }

        /// <summary>
        /// Connect to a given player on this channel.
        /// </summary>
        /// <param name="address">SteamID of player to connect to.</param>
        public void Connect(ulong address)
        {
            if (connections.ContainsKey(address))
                return;

            void OnPort(ushort port)
            {
                SteamNetworkingIdentity identity = new();
                identity.SetSteamID64(address);

                SteamNetworkingSockets.ConnectP2P(ref identity, port, 0, []);
            }

            NetworkingManager.instance!.discovery.GetPort(address, GUID, channel, OnPort);
        }

        /// <summary>
        /// Disconnect a given player from this channel.
        /// </summary>
        /// <param name="player">SteamID of player to connect to.</param>
        public void Disconnect(ulong player)
        {
            if (connections.TryGetValue(player, out HSteamNetConnection conn))
            {
                SteamNetworkingSockets.CloseConnection(conn, 1002, "Connection closed", true);
                connections.Remove(player);
            }
        }

        /// <summary>
        /// Retrieves the current statistics for the connection to the given player.
        /// </summary>
        /// <param name="player">Player to get statistics.</param>
        /// <returns>Connection statistics</returns>
        /// <remarks>
        /// Different channels may return different results for the same player.
        /// </remarks>
        public NetworkStatistics GetStatistics(ulong player)
        {
            if (!connections.TryGetValue(player, out HSteamNetConnection conn))
                return default;

            SteamNetConnectionRealTimeStatus_t status = default;
            SteamNetConnectionRealTimeLaneStatus_t drop = default;
            SteamNetworkingSockets.GetConnectionRealTimeStatus(conn, ref status, 0, ref drop);

            return new(status);
        }

        internal void ReceiveMessage(NetworkMessage message)
        {
            OnMessage?.Invoke(message);
        }

        internal bool ReceiveConnection(ulong player, HSteamNetConnection conn)
        {
            if (OnConnection?.Invoke(player) == true)
            {
                connections[player] = conn;
                return true;
            }
            else
                return false;
        }

        internal void NotifyConnected(ulong player, HSteamNetConnection conn)
        {
            OnConnected?.Invoke(player);
            connections[player] = conn;
        }

        internal void ReceiveDisconnect(ulong player)
        {
            OnDisconnect?.Invoke(player);
            connections.Remove(player);
        }

        internal void ReceiveFailed(ulong player, bool refused)
        {
            OnConnectionFailed?.Invoke(player, refused);
        }

        internal void Close()
        {
            SteamNetworkingSockets.CloseListenSocket(socket);
            NetworkingManager.instance!.NotifyClosed(socket);

            foreach (HSteamNetConnection conn in connections.Values.ToArray())
                SteamNetworkingSockets.CloseConnection(conn, 1000, "Channel closed", true);
        }
    }
}
