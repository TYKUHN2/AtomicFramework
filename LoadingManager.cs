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
    public static class LoadingManager
    {
        public class Cancelable
        {
            private bool _canceled;

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

        public static event Action? GameLoaded;
        public static event Action? NetworkReady;

        public static event Action? MissionLoaded;
        public static event Action? MissionUnloaded;

        public static event Action<ulong>? PlayerJoined;
        public static event Action<ulong>? PlayerLeft;

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
            harmony.Patch(
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
                        .Where(plugin => plugin.Instance is not Mod mod || mod.options.multiplayerOptions == Mod.MultiplayerOptions.REQUIRES_ALL)
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

        private static void ServerDisconnectCallback(INetworkPlayer player)
        {
            ulong id = (player.Address as SteamEndPoint)?.Connection?.SteamID.m_SteamID ?? 0;
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
