using BepInEx;
using HarmonyLib;
using Mirage;
using Mirage.SteamworksSocket;
using NuclearOption.Networking;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace AtomicFramework
{
    /// <summary>
    /// Provides access to common gameplay events.
    /// </summary>
    public static class LoadingManager
    {
        /// <summary>
        /// Allows delegates to cancel an event.
        /// </summary>
        /// <remarks>
        /// Due to how C# works, all delegates are always called regardless of when the event is canceled.
        /// </remarks>
        public class Cancelable
        {
            private bool _canceled;

            /// <summary>
            /// If the event has been canceled.
            /// </summary>
            /// <remarks>
            /// Cannot be set to false.
            /// </remarks>
            public bool Canceled
            {
                get
                {
                    return _canceled;
                }
                set
                {
                    _canceled = _canceled || value;
                }
            }
        }

        private static readonly List<INetworkPlayer> pending = [];

        private static readonly Harmony harmony = new("xyz.tyknet.NuclearOption");

        /// <summary>
        /// When the game has finished loading.
        /// </summary>
        public static event Action? GameLoaded;

        /// <summary>
        /// When the game's Network Manager (<code>NetworkManagerNuclearOption</code>) has been
        /// initialized.
        /// </summary>
        public static event Action? NetworkReady;

        /// <summary>
        /// When a mission has been fully loaded.
        /// </summary>
        /// <remarks>
        /// Mirage at this point reports that the scene has been loaded, and most or all network
        /// objects created.
        /// That being said, there is no guarentee the game follows what Mirage says.
        /// </remarks>
        public static event Action? MissionLoaded;

        /// <summary>
        /// When a mission is being unloaded.
        /// </summary>
        public static event Action? MissionUnloaded;

        /// <summary>
        /// When we are a host and a given player joined.
        /// </summary>
        public static event Action<ulong>? PlayerJoined;

        /// <summary>
        /// When we are a host and a given player left.
        /// </summary>
        public static event Action<ulong>? PlayerLeft;

        /// <summary>
        /// When a player is attempting to join, before any framework networking begun.
        /// </summary>
        /// <remarks>
        /// Avoid using the networking API as functionality is not guaranteed at this point.
        /// </remarks>
        /// <seealso cref="PlayerAuthenticating"/>
        public static event Action<ulong, Cancelable>? PrePlayerAuthenticating;

        /// <summary>
        /// When a given player is attempting to join, allowing mods filter out users.
        /// </summary>
        /// <remarks>
        /// Does not trigger the kick system, so the player can immediately rejoin.
        /// </remarks>
        public static event Action<ulong, Cancelable>? PlayerAuthenticating;

        static LoadingManager()
        {
            Type thisType = typeof(LoadingManager);

            Type netManager = typeof(NetworkManagerNuclearOption);
            harmony.Patch(
                netManager.GetMethod("Awake"),
                null,
                HookMethod(NetworkManagerPostfix)
            );
            harmony.Patch(
                netManager.GetMethod("OnServerAuthenticated", BindingFlags.NonPublic | BindingFlags.Instance),
                HookMethod(ClientAuthenticatingCallback)
            );

            harmony.Patch( // Maybe todo: replace with a direct listener on NetworkServer
                netManager.GetMethod("OnServerDisconnect", BindingFlags.NonPublic | BindingFlags.Instance),
                postfix: HookMethod(ServerDisconnectCallback)
            );

            Type mainMenu = typeof(MainMenu);
            harmony.Patch(
                mainMenu.GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance),
                postfix: HookMethod(MainMenuPostfix)
            );

            Type server = typeof(Server);
            harmony.Patch(
                server.GetConstructors()[0],
                postfix: HookMethod(KillCallback)
                );

            Type client = typeof(Client);
            harmony.Patch(
                client.GetMethod("ConnectAsync", BindingFlags.NonPublic | BindingFlags.Instance),
                postfix: HookMethod(KillCallback)
                );
        }

        private static HarmonyMethod HookMethod(Delegate hook)
        {
            return new HarmonyMethod(hook.GetMethodInfo());
        }

        private static void MainMenuPostfix()
        {
            Plugin.Logger.LogDebug("Reached GameLoaded");
            GameLoaded?.Invoke();

            MethodBase original = harmony.GetPatchedMethods().Where(a => a.DeclaringType == typeof(MainMenu)).First();
            harmony.Unpatch(original, HookMethod(MainMenuPostfix).method);
        }

        private static void NetworkManagerPostfix()
        {
            NetworkManagerNuclearOption.i.Client.Connected.AddListener(ClientConnectCallback);
            NetworkManagerNuclearOption.i.Client.Disconnected.AddListener(ClientDisconectCallback);

            Plugin.Logger.LogDebug("Reached NetworkReady");
            NetworkReady?.Invoke();
        }

        private static void MissionLoadCallback()
        {
            Plugin.Logger.LogDebug("Reached MissionLoaded");
            MissionLoaded?.Invoke();
        }

        private static void OnIdentity(NetworkIdentity identity)
        {
            identity.OnStartLocalPlayer.AddListener(MissionLoadCallback);
        }

        private static void ClientConnectCallback(INetworkPlayer player)
        {
            if (MissionManager.CurrentMission != null)
                player.OnIdentityChanged += OnIdentity;
        }

        private static void ClientDisconectCallback(ClientStoppedReason reason)
        {
            Plugin.Logger.LogDebug("Reached MissionUnloaded");
            MissionUnloaded?.Invoke();
        }

        private static bool ClientAuthenticatingCallback(INetworkPlayer player)
        {
            if (player.IsHost)
                return true;

            if (pending.Contains(player))
            {
                pending.Remove(player);
                return true;
            }

            Plugin.Logger.LogDebug("ClientAutheticatingCallback");
            if (player.Address is not SteamEndPoint endpoint)
            {
                Plugin.Logger.LogWarning("Non-Steam player detected. Cannot validate.");
                return true;
            }

            Cancelable cancelable = new();
            PrePlayerAuthenticating?.Invoke(endpoint.Connection.SteamID.m_SteamID, cancelable);
            if (cancelable.Canceled)
            {
                player.Disconnect();
                NetworkingManager.instance!.Kill(endpoint.Connection.SteamID.m_SteamID);
                return false;
            }

            NetworkingManager.instance!.discovery.ConnectTo(endpoint.Connection.SteamID.m_SteamID);

            int checkpoint = 0;

            void Subscriber(ulong iplayer)
            {
                Plugin.Logger.LogDebug("Subscriber");
                if (iplayer == endpoint.Connection.SteamID.m_SteamID)
                {
                    NetworkingManager.instance!.discovery.ModsAvailable -= Subscriber;

                    PluginInfo[] enabled = [.. Plugin.Instance.PluginsEnabled()
                        .Where(plugin => plugin.Instance is Mod mod
                        && mod.options.multiplayerOptions == Mod.Options.Multiplayer.REQUIRES_ALL)];

                    string[] mods = NetworkingManager.instance!.discovery.GetMods(iplayer);

                    Plugin.Logger.LogDebug($"Got [{string.Join(", ", mods)}]   need   [{string.Join(", ", enabled.Select(a => a.Metadata.GUID))}]");

                    foreach (PluginInfo plugin in enabled)
                    {
                        if (mods.Contains(plugin.Metadata.GUID)) // Available, so we are good
                            continue;

                        if (plugin.Instance is Mod mod)
                        {
                            if (mod.options.runtimeOptions != Mod.Options.Runtime.NONE) // Unavailable but can disable
                            {
                                mod.enabled = false;
                                continue;
                            }
                        }
                        else
                            continue; // A lot of legacy plugins are much more careful about either making sure they are safe,
                                      // or informing users. We should change this back later once Mod becomes more popular
                                      // or a more user-centric mechanism is available.

                        // Cannot disable and is unavailable. Cannot join.
                        player.Disconnect();
                        NetworkingManager.instance.Kill(endpoint.Connection.SteamID.m_SteamID);

                        Plugin.Logger.LogDebug("Discovery.Subscriber.Kill");

                        return;
                    }

                    Plugin.Logger.LogDebug("Discovery.Subscriber.Passed");

                    if (Interlocked.Exchange(ref checkpoint, int.MaxValue) == int.MaxValue)
                    {
                        ContinueAuthentication(player);
                    }
                }
            }

            NetworkingManager.instance!.discovery.ConnectionResolved += resolve_for =>
            {
                if (resolve_for != endpoint.Connection.SteamID.m_SteamID)
                    return;

                NetworkingManager.instance!.discovery.ModsAvailable += Subscriber;

                NetworkingManager.instance!.discovery.GetRequired(endpoint.Connection.SteamID.m_SteamID, required =>
                {
                    Plugin.Logger.LogDebug("GetRequired");
                    PluginInfo[] loaded = [.. Plugin.Instance.PluginsLoaded()];
                    PluginInfo[] loadedAndRequired = [.. loaded.Where(plugin => required.Contains(plugin.Metadata.GUID))];

                    if (loadedAndRequired.Length == required.Length) // All required are available
                    {
                        foreach (PluginInfo info in loadedAndRequired)
                        {
                            if (((MonoBehaviour)info.Instance).enabled)
                                continue;

                            if (info.Instance is Mod mod)
                            {
                                if (mod.options.runtimeOptions != Mod.Options.Runtime.NONE)
                                {
                                    mod.enabled = true;
                                    continue;
                                }
                            }

                            // Mod disabled, required, cannot enable.
                            player.Disconnect();
                            NetworkingManager.instance.Kill(endpoint.Connection.SteamID.m_SteamID);

                            Plugin.Logger.LogDebug("Plugin.Required.Disabled.Kill");

                            return;
                        }

                        Plugin.Logger.LogDebug("Plugin.Required.Passed");

                        if (Interlocked.Exchange(ref checkpoint, int.MaxValue) == int.MaxValue)
                        {
                            ContinueAuthentication(player);
                        }
                    }
                    else
                    {
                        Plugin.Logger.LogDebug("Discovery.Required.Kill");

                        player.Disconnect();
                        NetworkingManager.instance.Kill(endpoint.Connection.SteamID.m_SteamID);
                    }
                });
            };

            return false;
        }

        private static void ServerDisconnectCallback(INetworkPlayer conn)
        {
            ulong id = (conn.Address as SteamEndPoint)?.Connection?.SteamID.m_SteamID ?? 0;
            PlayerLeft?.Invoke(id);
        }

        private static void ContinueAuthentication(INetworkPlayer player)
        {
            Plugin.Logger.LogDebug("ContinueAuthetication");
            ulong id = (player.Address as SteamEndPoint)?.Connection?.SteamID.m_SteamID ?? 0;

            Cancelable cancelable = new();
            PlayerAuthenticating?.Invoke(id, cancelable);

            if (!cancelable.Canceled)
            {
                PlayerJoined?.Invoke(id);

                MethodInfo continuation = typeof(NetworkManagerNuclearOption).GetMethod("OnServerAuthenticated", BindingFlags.NonPublic | BindingFlags.Instance);

                pending.Add(player);

                continuation.Invoke(NetworkManagerNuclearOption.i, [player]);
            }
            else
            {
                player.Disconnect();
                NetworkingManager.instance!.Kill(id);
            }
        }

        private static void KillCallback(Callback<SteamNetConnectionStatusChangedCallback_t> ___c_onConnectionChange)
        {
            ___c_onConnectionChange.Unregister();
        }
    }
}
