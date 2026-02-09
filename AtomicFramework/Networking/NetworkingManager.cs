using Steamworks;
using System.Linq;
using System.Runtime.InteropServices;
using System;
using UnityEngine;
using System.Collections.Generic;
using System.Threading;
using NuclearOption.Networking;
using Mirage.SteamworksSocket;
using HarmonyLib;
using Mirage.SocketLayer;
using System.IO;
using Mirage;

namespace AtomicFramework
{
    /// <summary>
    /// Provides networking capabilities.
    /// </summary>
    public class NetworkingManager: MonoBehaviour
    {
        public static NetworkingManager? instance
        {
            get; private set;
        }

        private readonly SemaphoreSlim listenLock = new(1);

        private readonly List<ushort> ports = [0];
        private readonly Dictionary<HSteamListenSocket, NetworkChannel> sockets = [];
        internal readonly Dictionary<HSteamNetConnection, NetworkChannel> connections = [];

        private readonly Callback<SteamNetConnectionStatusChangedCallback_t> statusChanged;

        private readonly HSteamNetPollGroup poll = SteamNetworkingSockets.CreatePollGroup();

        /// <summary>
        /// Access to the <see cref="Discovery">Discovery</see> instance.
        /// </summary>
        public readonly Discovery discovery;

        internal static event Action<NetworkingManager>? OnNetworkingAvail;

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
                GameManager.GetLocalPlayer(out BasePlayer localPlayer);

                foreach (Player player in UnitRegistry.playerLookup.Values.Where(player => player != localPlayer).ToArray())
                {
                    if (!discovery.Players.Contains(player.SteamID))
                        Plugin.Logger.LogDebug($"Player {player.PlayerName} ({player.SteamID:X}) not using framework.");
                }
            };

            OnNetworkingAvail?.Invoke(this);
        }

        /// <summary>
        /// Utility funtion to convert player SteamID to Player object.
        /// </summary>
        /// <param name="steamid">SteamID to convert.</param>
        /// <returns>Game Player object.</returns>
        public static Player? GetPlayer(ulong steamid)
        {
            return UnitRegistry.playerLookup.Values.FirstOrDefault(player => player.SteamID == steamid);
        }

        public static bool IsServer()
        {
            return GameManager.IsHeadless || NetworkManagerNuclearOption.i.Server.Active;
        }

        public static bool IsDedicatedServer()
        {
            return GameManager.IsHeadless;
        }

        public static bool IsPeer(ulong player)
        {
            return GetPlayer(player) != null;
        }

        public static bool IsHost(ulong player)
        {
            return (NetworkManagerNuclearOption.i.Client.Player.ConnectionHandle as SteamConnection)?.SteamID.m_SteamID == player;
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
                    SteamNetworkingMessage_t message = SteamNetworkingMessage_t.FromIntPtr(ptrs[i]);

                    byte[] data = new byte[message.m_cbSize];
                    Marshal.Copy(message.m_pData, data, 0, data.Length);

                    SteamNetworkingMessage_t.Release(ptrs[i]);

                    if (connections.TryGetValue(message.m_conn, out NetworkChannel channel))
                        channel.ReceiveMessage(new(data, message.m_identityPeer.GetSteamID64()));
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
            if (change.m_info.m_hListenSocket != HSteamListenSocket.Invalid && !sockets.ContainsKey(change.m_info.m_hListenSocket))
            {
                Redirect(change);
                return;
            }

            if (change.m_info.m_hListenSocket == HSteamListenSocket.Invalid && !connections.ContainsKey(change.m_hConn))
            {
                Redirect(change);
                return;
            }

            switch (change.m_info.m_eState)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                    OnConnection(change.m_info.m_hListenSocket, change.m_hConn, change.m_info.m_identityRemote);
                    break;
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
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
            if (listen == HSteamListenSocket.Invalid)
                return;

            NetworkChannel channel = sockets[listen];

            bool cond1 = GameManager.gameState == GameState.Multiplayer &&
            (IsServer() || IsPeer(remote.GetSteamID64()));

            bool cond2 = IsHost(remote.GetSteamID64());

            if ((cond1 || cond2) && channel.ReceiveConnection(remote.GetSteamID64(), conn))
            {
                SteamNetworkingSockets.AcceptConnection(conn);
                connections[conn] = channel;

                SteamNetworkingSockets.SetConnectionPollGroup(conn, poll);

                SteamNetworkingSockets.SetConnectionName(conn, $"{channel.GUID}({channel.channel}):{remote.GetSteamID64()}");
            }
            else
                SteamNetworkingSockets.CloseConnection(conn, 1001, $"Connection refused ({cond1} {cond2})", false);
        }

        private void OnConnected(HSteamNetConnection conn, SteamNetworkingIdentity remote)
        {
            NetworkChannel channel = connections[conn];
            SteamNetworkingSockets.SetConnectionPollGroup(conn, poll);

            channel.NotifyConnected(remote.GetSteamID64(), conn);
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
            Plugin.Logger.LogDebug($"Allocating {GUID}:{channel}");

            HSteamListenSocket socket = HSteamListenSocket.Invalid;

            listenLock.Wait();

            for (ushort i = 0; i < ports.Count; i++)
            {
                if (ports[i] != i)
                {
                    ports.Insert(i, i);

                    socket = SteamNetworkingSockets.CreateListenSocketP2P(i, 0, []);

                    break;
                }
            }

            if (socket == HSteamListenSocket.Invalid)
            {
                ushort end = (ushort)ports.Count;
                ports.Add(end);

                socket = SteamNetworkingSockets.CreateListenSocketP2P(end, 0, []);
            }

            if (socket == HSteamListenSocket.Invalid)
                throw new IOException("Failed to generate socket");

            NetworkChannel chan = new(socket, GUID, channel);
            sockets[socket] = chan;
            listenLock.Release();

            Plugin.Logger.LogDebug($"Channel for socket {socket.m_HSteamListenSocket} open");

            return chan;
        }

        internal void NotifyClosed(HSteamListenSocket socket)
        {
            ushort port = GetListenPort(socket);
            if (port == 0)
                return;
            
            listenLock.Wait();

            ports.Remove(port);
            sockets.Remove(socket);

            listenLock.Release();
        }

        internal ushort GetListenPort(HSteamListenSocket socket)
        {
            int port;
            ulong len = 4;
            ESteamNetworkingGetConfigValueResult res;

            unsafe
            {
                res = SteamNetworkingUtils.GetConfigValue(
                    ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_LocalVirtualPort,
                    ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_ListenSocket,
                    new IntPtr(&socket),
                    out _,
                    new IntPtr(&port),
                    ref len
                );
            }

            if (res < 0)
            {
                Plugin.Logger.LogError("Failed to get port of listen socket");
                return 0;
            }

            return (ushort)port;
        }

        internal void NotifyClosed(HSteamNetConnection conn)
        {
            connections.Remove(conn);
        }

        private void Redirect(SteamNetConnectionStatusChangedCallback_t change)
        {
            if (NetworkManagerNuclearOption.i.Server.Active)
            {
                Peer peer = (Peer)AccessTools.Field(typeof(NetworkServer), "_peer").GetValue(NetworkManagerNuclearOption.i.Server);
                SteamSocket socket = (SteamSocket)AccessTools.Field(typeof(Peer), "_socket").GetValue(peer);
                Server server = (Server)AccessTools.Field(typeof(SteamSocket), "common").GetValue(socket);
                AccessTools.Method(typeof(Server), "OnConnectionStatusChanged").Invoke(server, [change]);
            }
            else if (NetworkManagerNuclearOption.i.Client.Active)
            {
                Peer peer = (Peer)AccessTools.Field(typeof(NetworkClient), "_peer").GetValue(NetworkManagerNuclearOption.i.Client);
                SteamSocket socket = (SteamSocket)AccessTools.Field(typeof(Peer), "_socket").GetValue(peer);
                Client client = (Client)AccessTools.Field(typeof(SteamSocket), "common").GetValue(socket);
                AccessTools.Method(typeof(Client), "OnConnectionStatusChanged").Invoke(client, [change]);
            }
        }
    }
}
