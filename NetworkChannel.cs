using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AtomicFramework
{
    public class NetworkChannel
    {
        internal readonly HSteamListenSocket socket;

        private readonly Dictionary<ulong, HSteamNetConnection> connections = [];

        public event Action<NetworkMessage>? OnMessage;
        public event Action<ulong>? OnConnected;
        public event Action<ulong>? OnDisconnect;
        public event Action<ulong, bool>? OnConnectionFailed;

        public delegate bool ConnectionFilter(ulong player);
        public ConnectionFilter? OnConnection;

        private readonly string GUID;
        private readonly ushort channel;

        internal NetworkChannel(HSteamListenSocket socket, string GUID, ushort channel)
        {
            this.socket = socket;
            this.GUID = GUID;
            this.channel = channel;
        }

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

        public void Disconnect(ulong player)
        {
            if (connections.TryGetValue(player, out HSteamNetConnection conn))
            {
                SteamNetworkingSockets.CloseConnection(conn, 1002, "Connection closed", true);
                connections.Remove(player);
            }
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
