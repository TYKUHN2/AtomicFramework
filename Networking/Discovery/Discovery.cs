using NuclearOption.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace AtomicFramework
{
    /// <summary>
    /// Provides access to discovering the mods of other players.
    /// </summary>
    public class Discovery
    {
        private static readonly byte[] Handshake = [0x01, 0x00, 0x00, 0x00]; // Our maximum discovery version (little endian): 1.0

        private readonly NetworkChannel channel = NetworkingManager.instance!.OpenListen("AtomicFramework", 0);

        private int pending = 0;

        private readonly Dictionary<ulong, IDiscoveryHandler?> knownPlayers = [];

        /// <summary>
        /// Array of all players currently connected via the Discovery mechanism.
        /// </summary>
        public ulong[] Players
        {
            get
            {
                return [..knownPlayers.Keys];
            }
        }

        /// <summary>
        /// Fired when all pending connections to other players are connected or failed.
        /// </summary>
        /// <remarks>
        /// Fires repeatedly throughout mission.
        /// </remarks>
        public event Action? Ready;

        /// <summary>
        /// Fires when the given player has send its mod list.
        /// </summary>
        public event Action<ulong>? ModsAvailable;

        internal Discovery()
        {
            channel.OnConnection = ConnectionFilter;
            channel.OnConnected += Discover;
            channel.OnConnectionFailed += Fail;
            channel.OnDisconnect += Disconnect;
            channel.OnMessage += Handler;

            LoadingManager.MissionLoaded += MissionLoaded;
            LoadingManager.MissionUnloaded += MissionUnloaded;
        }

        /// <summary>
        /// Gets the list of known mods of a player.
        /// </summary>
        /// <remarks>
        /// If the mods are not known, returns and empty array.
        /// Otherwise, always contains at least AtomicFramework.
        /// </remarks>
        /// <param name="player">Player to get mods of.</param>
        /// <returns>List of known mods.</returns>
        public string[] GetMods(ulong player)
        {
            return knownPlayers.GetValueOrDefault(player)?.Mods ?? [];
        }

        internal void GetRequired(ulong player, Action<string[]> callback)
        {
            if (knownPlayers.TryGetValue(player, out IDiscoveryHandler? handler) && handler != null)
            {
                void Subscriber(string[] mods)
                {
                    handler.OnRequired -= Subscriber;
                    callback(mods);
                }

                handler.OnRequired += Subscriber;
                handler.GetRequired();
            }
            else
                callback([]);
        }

        internal void GetPort(ulong player, string GUID, ushort channel, Action<ushort> callback)
        {
            if (knownPlayers.TryGetValue(player, out IDiscoveryHandler? handler) && handler != null)
            {
                if (handler.Mods.Length > 0 && !handler.Mods.Contains(GUID))
                {
                    callback.Invoke(0);
                    return;
                }

                void Subscriber(string iGUID, ushort iChannel, ushort port)
                {
                    if (iGUID == GUID && iChannel == channel)
                    {
                        handler.OnDiscovery -= Subscriber;
                        callback.Invoke(port);
                    }
                }

                handler.OnDiscovery += Subscriber;

                handler.GetPort(GUID, channel);
            }
            else
                callback.Invoke(0);
        }

        private bool ConnectionFilter(ulong player)
        {
            return NetworkManagerNuclearOption.i.Server.Active || UnitRegistry.playerLookup.Values.Any(play => play.SteamID == player);
        }

        private void Discover(ulong player)
        {
            knownPlayers[player] = null;
            channel.Send(player, Handshake);

            int res = Interlocked.Decrement(ref pending);
            if (res == 0)
                Ready?.Invoke();
            else if (res < 0)
                res = 0;
        }

        private void Fail(ulong player, bool refused)
        {
            int res = Interlocked.Decrement(ref pending);
            if (res == 0)
                Ready?.Invoke();
            else if (res < 0)
                res = 0;
        }

        private void Disconnect(ulong player)
        {
            knownPlayers.Remove(player);
        }

        private void Handler(NetworkMessage message)
        {
            IDiscoveryHandler? handler = knownPlayers[message.player];
            if (handler == null)
            {
                if (message.data.Length == Handshake.Length && memcmp(message.data, Handshake, Handshake.Length) == 0)
                {
                    V1Handler nHandler = new();
                    nHandler.OnOutbound += data => channel.Send(message.player, data);
                    nHandler.OnMods += () => ModsAvailable?.Invoke(message.player);
                    nHandler.Ready();

                    knownPlayers[message.player] = nHandler;
                }
                else
                    channel.Disconnect(message.player);
            }
            else
                handler.Receive(message);
        }

        private void MissionLoaded()
        {
            // Multiplayer and not host
            if (GameManager.gameState == GameManager.GameState.Multiplayer && !NetworkManagerNuclearOption.i.Server.Active)
            {
                Player[] players = UnitRegistry.playerLookup.Values.Where(player => player != GameManager.LocalPlayer).ToArray();
                pending = players.Length;

                foreach (Player player in players)
                    channel.Connect(player.SteamID);
            }
        }

        private void MissionUnloaded()
        {
            foreach (ulong player in knownPlayers.Keys.ToArray())
            {
                channel.Disconnect(player);
                knownPlayers.Remove(player);
            }
        }

        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int memcmp(byte[] b1, byte[] b2, long count);
    }
}
