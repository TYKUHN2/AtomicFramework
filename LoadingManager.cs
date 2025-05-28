using HarmonyLib;
using Mirage;
using Mirage.SteamworksSocket;
using NuclearOption.Networking;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;

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
                null,
                HookMethod(ServerDisconnectCallback)
            );

            Type mainMenu = typeof(MainMenu);
            harmony.Patch(
                mainMenu.GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance),
                null,
                HookMethod(MainMenuPostfix)
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
            if (player.Address is not SteamEndPoint endpoint)
            {
                Plugin.Logger.LogWarning("Non-Steam player detected. Cannot validate.");
                return true;
            }

            int checkpoint = 0;

            void Subscriber(ulong iplayer)
            {
                if (iplayer == endpoint.Connection.SteamID.m_SteamID)
                {
                    NetworkingManager.instance!.discovery.ModsAvailable -= Subscriber;

                    string[] enabled = Plugin.Instance.PluginsEnabled()
                        .Where(plugin => plugin.Instance is not Mod mod || mod.options.multiplayerOptions == Mod.Options.Multiplayer.REQUIRES_ALL)
                        .Select(plugin => plugin.Metadata.GUID)
                        .ToArray();

                    if (NetworkingManager.instance!.discovery.GetMods(iplayer).All(mod => enabled.Contains(mod)))
                    {
                        if (Interlocked.Exchange(ref checkpoint, int.MaxValue) == int.MaxValue)
                        {
                            ContinueAuthentication(player);
                        }
                    }
                    else
                    {
                        player.Disconnect();
                        NetworkingManager.instance.Kill(endpoint.Connection.SteamID.m_SteamID);
                    }
                }
            }

            NetworkingManager.instance!.discovery.ModsAvailable += Subscriber;

            NetworkingManager.instance!.discovery.GetRequired(endpoint.Connection.SteamID.m_SteamID, required =>
            {
                string[] enabled = Plugin.Instance.PluginsEnabled().Select(plugin => plugin.Metadata.GUID).ToArray();

                if (required.All(mod => enabled.Contains(mod)))
                {
                    if (Interlocked.Exchange(ref checkpoint, int.MaxValue) == int.MaxValue)
                    {
                        ContinueAuthentication(player);
                    }
                }
                else
                {
                    player.Disconnect();
                    NetworkingManager.instance.Kill(endpoint.Connection.SteamID.m_SteamID);
                }
            });

            return false;
        }

        private static void ServerDisconnectCallback(INetworkPlayer conn)
        {
            ulong id = (conn.Address as SteamEndPoint)?.Connection?.SteamID.m_SteamID ?? 0;
            PlayerLeft?.Invoke(id);
        }

        private static void ContinueAuthentication(INetworkPlayer player)
        {
            ulong id = (player.Address as SteamEndPoint)?.Connection?.SteamID.m_SteamID ?? 0;

            Cancelable cancelable = new();
            PlayerAuthenticating?.Invoke(id, cancelable);

            if (!cancelable.Canceled)
            {
                PlayerJoined?.Invoke(id);

                MethodInfo continuation = typeof(NetworkManagerNuclearOption).GetMethod("OnServerAuthenticated", BindingFlags.NonPublic | BindingFlags.Instance);
                continuation.Invoke(NetworkManagerNuclearOption.i, [player]);
            }
            else
            {
                player.Disconnect();
                NetworkingManager.instance!.Kill(id);
            }
        }
    }
}
