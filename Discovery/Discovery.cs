using NuclearOption.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace AtomicFramework
{
    public class Discovery
    {
        private static readonly byte[] Handshake = [0x01, 0x00, 0x00, 0x00]; // Our maximum discovery version (little endian): 1.0

        private readonly NetworkChannel channel = NetworkingManager.instance!.OpenListen("AtomicFramework", 0);

        private int pending = 0;

        private readonly Dictionary<ulong, IDiscoveryHandler?> knownPlayers = [];

        public ulong[] Players
        {
            get
            {
                return [..knownPlayers.Keys];
            }
        }

        public event Action? Ready;
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

            if (Interlocked.Decrement(ref pending) == 0)
                Ready?.Invoke();
        }

        private void Fail(ulong player, bool refused)
        {
            if (Interlocked.Decrement(ref pending) == 0)
                Ready?.Invoke();
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
