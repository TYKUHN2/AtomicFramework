using Steamworks;
using System.Linq;
using System.Runtime.InteropServices;
using System;
using UnityEngine;
using System.Collections.Generic;
using System.Threading;

namespace AtomicFramework
{
    /// <summary>
    /// Provides networking capabilities.
    /// </summary>
    public class NetworkingManager: MonoBehaviour
    {
        internal static NetworkingManager? instance;

        private readonly SemaphoreSlim listenLock = new(1);

        private readonly List<ushort> ports = [];
        internal readonly Dictionary<HSteamListenSocket, ushort> revPorts = [];
        private readonly Dictionary<HSteamListenSocket, NetworkChannel> sockets = [];
        private readonly Dictionary<HSteamNetConnection, NetworkChannel> connections = [];

        private readonly Callback<SteamNetConnectionStatusChangedCallback_t> statusChanged;

        private readonly HSteamNetPollGroup poll = SteamNetworkingSockets.CreatePollGroup();

        /// <summary>
        /// Access to the <see cref="Discovery">Discovery</see> instance.
        /// </summary>
        public readonly Discovery discovery;

#pragma warning disable CS8618
        private NetworkingManager()
#pragma warning restore CS8618
        {
            statusChanged = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnStatusChanged);
            
            SteamNetworking.AllowP2PPacketRelay(true);

            switch (SteamNetworkingSockets.GetAuthenticationStatus(out _))
            {
                case ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_NeverTried:
                case ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Previously:
                    SteamNetworkingSockets.InitAuthentication();
                    break;
                case ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_CannotTry:
                case ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Failed:
                    Plugin.Logger.LogWarning("Steam authentication failed, networking will be unavailable.");
                    Destroy(this);
                    return;
            }

            switch (SteamNetworkingUtils.GetRelayNetworkStatus(out _))
            {
                case ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_NeverTried:
                case ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Previously:
                    SteamNetworkingUtils.InitRelayNetworkAccess();
                    break;
                case ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_CannotTry:
                case ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Failed:
                    Plugin.Logger.LogWarning("Steam relay inaccessible, networking will be unavailable.");
                    Destroy(this);
                    return;
            }

            LoadingManager.MissionUnloaded += OnMissionUnloaded;

            instance = this;

            discovery = new();

            discovery.ModsAvailable += player =>
            {
                Plugin.Logger.LogDebug($"Player {GetPlayer(player)?.PlayerName ?? "Unknown"} ({player:X}) has mods {string.Join(", ", discovery.GetMods(player))}");
            };

            discovery.Ready += () =>
            {
                foreach (Player player in UnitRegistry.playerLookup.Values.Where(player => player != GameManager.LocalPlayer).ToArray())
                {
                    if (!discovery.Players.Contains(player.SteamID))
                        Plugin.Logger.LogDebug($"Player {player.PlayerName} ({player.SteamID:X}) not using framework.");
                }
            };
        }

        /// <summary>
        /// Utility funtion to convert player SteamID to Player object.
        /// </summary>
        /// <param name="steamid">SteamID to convert.</param>
        /// <returns>Game Player object.</returns>
        public Player? GetPlayer(ulong steamid)
        {
            return UnitRegistry.playerLookup.Values.FirstOrDefault(player => player.SteamID == steamid);
        }

        private void OnDestroy()
        {
            SteamNetworkingSockets.DestroyPollGroup(poll);
        }

        private void FixedUpdate()
        {
            IntPtr[] ptrs = new IntPtr[64];

            while (true)
            {
                int received = SteamNetworkingSockets.ReceiveMessagesOnPollGroup(poll, ptrs, 64);

                for (int i = 0; i < received; i++)
                {
                    unsafe
                    {
                        SteamNetworkingMessage_t* message = (SteamNetworkingMessage_t*)ptrs[i];

                        byte[] data = new byte[message->m_cbSize];
                        Marshal.Copy(message->m_pData, data, 0, data.Length);

                        NetworkChannel channel = connections[message->m_conn];
                        channel.ReceiveMessage(new(data, message->m_identityPeer.GetSteamID64()));

                        SteamNetworkingMessage_t.Release((IntPtr)message);
                    }
                }

                if (received < 64)
                    break;
            }
        }

        private void OnMissionUnloaded()
        {
            foreach (HSteamNetConnection conn in connections.Keys.ToArray())
            {
                SteamNetworkingSockets.GetConnectionInfo(conn, out SteamNetConnectionInfo_t info);

                OnDisconnect(conn, info.m_identityRemote, 0);
            }
        }

        private void OnStatusChanged(SteamNetConnectionStatusChangedCallback_t change)
        {
            switch (change.m_info.m_eState)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                    OnConnection(change.m_info.m_hListenSocket, change.m_hConn, change.m_info.m_identityRemote);
                    break;
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_FindingRoute:
                    OnConnected(change.m_hConn, change.m_info.m_identityRemote);
                    break;
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    if (change.m_eOldState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
                        OnDisconnect(change.m_hConn, change.m_info.m_identityRemote, (ESteamNetConnectionEnd)change.m_info.m_eEndReason);
                    else
                        OnFailedConnection(change.m_hConn, change.m_info.m_identityRemote, (ESteamNetConnectionEnd)change.m_info.m_eEndReason);
                    break;
            }
        }

        internal void Kill(ulong player)
        {
            foreach (HSteamNetConnection conn in connections.Keys.ToArray())
            {
                SteamNetworkingSockets.GetConnectionInfo(conn, out SteamNetConnectionInfo_t info);
                if (info.m_identityRemote.GetSteamID64() == player)
                    OnDisconnect(conn, info.m_identityRemote, (ESteamNetConnectionEnd)1003);
            }
        }
        private void OnConnection(HSteamListenSocket listen, HSteamNetConnection conn, SteamNetworkingIdentity remote)
        {
            NetworkChannel channel = sockets[listen];

            if (channel.ReceiveConnection(remote.GetSteamID64(), conn))
            {
                SteamNetworkingSockets.AcceptConnection(conn);
                connections[conn] = channel;

                SteamNetworkingSockets.SetConnectionPollGroup(conn, poll);
            }
            else
                SteamNetworkingSockets.CloseConnection(conn, 1001, "Connection refused", false);
        }

        private void OnConnected(HSteamNetConnection conn, SteamNetworkingIdentity remote)
        {
            NetworkChannel channel = connections[conn];

            channel.NotifyConnected(remote.GetSteamID64(), conn);
            SteamNetworkingSockets.SetConnectionPollGroup(conn, poll);
        }

        private void OnDisconnect(HSteamNetConnection conn, SteamNetworkingIdentity remote, ESteamNetConnectionEnd _)
        {
            NetworkChannel channel = connections[conn];

            channel.ReceiveDisconnect(remote.GetSteamID64());

            SteamNetworkingSockets.CloseConnection(conn, 1000, "Channel closed", false);

            connections.Remove(conn);
        }

        private void OnFailedConnection(HSteamNetConnection conn, SteamNetworkingIdentity remote, ESteamNetConnectionEnd reason)
        {
            NetworkChannel channel = connections[conn];

            channel.ReceiveFailed(remote.GetSteamID64(), (int)reason == 1001);

            SteamNetworkingSockets.CloseConnection(conn, 0, "", false);

            connections.Remove(conn);
        }

        internal NetworkChannel OpenListen(string GUID, ushort channel)
        {
            listenLock.Wait();

            HSteamListenSocket socket = HSteamListenSocket.Invalid;

            for (ushort i = 0; i < ports.Count; i++)
            {
                if (ports[i] != i)
                {
                    ports.Insert(i, i);

                    socket = SteamNetworkingSockets.CreateListenSocketP2P(i, 0, []);
                    revPorts[socket] = i;

                    break;
                }
            }

            if (socket == HSteamListenSocket.Invalid)
            {
                ushort end = (ushort)ports.Count;
                ports.Add(end);

                socket = SteamNetworkingSockets.CreateListenSocketP2P(end, 0, []);
                revPorts[socket] = end;
            }

            NetworkChannel chan = new(socket, GUID, channel);
            sockets[socket] = chan;
            listenLock.Release();

            return chan;
        }

        internal void NotifyClosed(HSteamListenSocket socket)
        {
            listenLock.Wait();

            if (revPorts.TryGetValue(socket, out ushort port))
            {
                ports.Remove(port);
                revPorts.Remove(socket);
            }

            sockets.Remove(socket);

            listenLock.Release();
        }

        internal void NotifyClosed(HSteamNetConnection conn)
        {
            connections.Remove(conn);
        }
    }
}
