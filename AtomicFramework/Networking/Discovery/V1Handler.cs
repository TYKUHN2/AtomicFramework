using BepInEx;
using Mirage.SocketLayer;
using Mirage.SteamworksSocket;
using NuclearOption.Networking;
using Steamworks;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AtomicFramework
{
    internal class V1Handler: IDiscoveryHandler
    {
        private enum Commands: byte
        {
            DISCOVER,
            REQUIRE,
            PORT
        }

        internal override void Receive(NetworkMessage message)
        {
            MemoryStream stream = new(message.data, false);
            BinaryReader reader = new(stream);

            try
            {
                switch ((Commands)reader.ReadByte())
                {
                    case Commands.DISCOVER:
                        if (message.data.Length == 1) // Request
                        {
                            MemoryStream output = new();
                            BinaryWriter writer = new(output);

                            writer.Write((byte)Commands.DISCOVER);

                            string[] mods = Plugin.Instance.PluginsEnabled().Select(plugin => plugin.Metadata.GUID).ToArray();

                            writer.Write((uint)mods.Length);
                            foreach (string mod in mods)
                            {
                                writer.Write(mod);
                            }

                            PushMessage(output.ToArray());
                        }
                        else
                        {
                            string[] mods = new string[reader.ReadUInt32()];
                            for (int i = 0; i < mods.Length; i++)
                            {
                                mods[i] = reader.ReadString();
                            }

                            Mods = mods;
                            NotifyMods();
                        }
                        break;
                    case Commands.REQUIRE:
                        if (message.data.Length == 1) // Request
                        {
                            MemoryStream output = new();
                            BinaryWriter writer = new(output);

                            writer.Write((byte)Commands.REQUIRE);

                            PluginInfo[] plugins = [.. Plugin.Instance.PluginsEnabled()];

                            List<string> required = [];

                            foreach (PluginInfo plugin in plugins)
                            {
                                if (plugin.Instance is Mod mod)
                                {
                                    Mod.Options.Multiplayer options = mod.options.multiplayerOptions;

                                    if (NetworkManagerNuclearOption.i.Server.Active) // We are host
                                    {
                                        if (options == Mod.Options.Multiplayer.REQUIRES_ALL)
                                            required.Add(plugin.Metadata.GUID);
                                    }
                                    else // We are client
                                    {
                                        IEndPoint peer = NetworkManagerNuclearOption.i.Client.Player.Address;

                                        // If we are loading, we can toggle mods and can ignore their requirements
                                        if (NetworkManagerNuclearOption.i.Client.Player.SceneIsReady && mod.options.runtimeOptions != Mod.Options.Runtime.NONE)
                                            continue;

                                        if (options == Mod.Options.Multiplayer.REQUIRES_ALL ||
                                            (options == Mod.Options.Multiplayer.REQUIRES_HOST &&
                                            peer is SteamEndPoint steamPeer &&
                                            steamPeer.Connection.SteamID.m_SteamID == message.player))
                                            required.Add(plugin.Metadata.GUID);
                                    }
                                }
                                else
                                    required.Add(plugin.Metadata.GUID);
                            }

                            writer.Write(required.Count);
                            required.ForEach(writer.Write);

                            PushMessage(output.ToArray());
                        }
                        else
                        {
                            string[] mods = new string[reader.ReadUInt32()];
                            for (int i = 0; i < mods.Length; i++)
                            {
                                mods[i] = reader.ReadString();
                            }

                            PushRequired(mods);
                        }
                        break;
                    case Commands.PORT:
                        string GUID = reader.ReadString();
                        ushort channel = reader.ReadUInt16();

                        if (stream.Position == stream.Length) // request
                        {
                            MemoryStream output = new();
                            BinaryWriter writer = new(output);

                            writer.Write((byte)Commands.PORT);
                            writer.Write(GUID);
                            writer.Write(channel);

                            PluginInfo[] plugins = Plugin.Instance.PluginsEnabled();
                            PluginInfo? plugin = plugins.First(plugin => plugin.Metadata.GUID == GUID);
                            if (plugin == null || plugin.Instance is not Mod)
                                writer.Write((ushort)0);
                            else
                            {
                                if (((Mod)plugin.Instance).Networking!.channels.TryGetValue(channel, out var netChan))
                                {
                                    HSteamListenSocket socket = netChan.socket;
                                    writer.Write(NetworkingManager.instance!.revPorts.GetValueOrDefault(socket, (ushort)0));
                                }
                                else
                                    writer.Write((ushort)0);
                            }

                            PushMessage(output.ToArray());
                        }
                        else
                            PushDiscovery(GUID, channel, reader.ReadUInt16());
                        break;
                }
            }
            catch (EndOfStreamException)
            {
                Plugin.Logger.LogWarning($"Discovery packet invalid from {message.player:X}");
            }
        }
        internal override void GetPort(string GUID, int channel)
        {
            MemoryStream str = new();
            BinaryWriter writer = new(str);

            writer.Write((byte)Commands.PORT);
            writer.Write(GUID);
            writer.Write(channel);

            PushMessage(str.ToArray());
        }

        internal override void GetRequired()
        {
            PushMessage([(byte)Commands.REQUIRE]);
        }

        internal override void Ready()
        {
            PushMessage([(byte)Commands.DISCOVER]);
        }
    }
}
