using BepInEx;
using System;
using System.Threading;
using BepInEx.Logging;
using UnityEngine;
using System.Linq;
using AtomicFramework.UI;

#if BEP5
using BepInEx.Bootstrap;
#elif BEP6
using BepInEx.Unity.Mono;
using BepInEx.Unity.Mono.Bootstrap;
#endif

namespace AtomicFramework
{
    /// <summary>
    /// BepInEx plugin of AtomicFramework.
    /// </summary>
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInProcess("NuclearOption.exe")]
    internal class Plugin : BaseUnityPlugin
    {
        private static Plugin? _Instance;
        internal static Plugin Instance
        {
            get
            {
                if (_Instance == null)
                    throw new InvalidOperationException("Plugin not initialized");

                return _Instance;
            }
        }

        internal new static ManualLogSource Logger
        {
            get { return Instance._Logger; }
        }

        private ManualLogSource _Logger => base.Logger;

        private Plugin()
        {
            if (Interlocked.CompareExchange(ref _Instance, this, null) != null) // I like being thread safe okay?
                throw new InvalidOperationException($"Reinitialization of Plugin {MyPluginInfo.PLUGIN_GUID}");

            LoadingManager.GameLoaded += LateLoad;
        }

        ~Plugin()
        {
            Logger.LogWarning("AtomicFramework unloaded. Dependent mods will likely break.");
            _Instance = null;
        }

        private void Awake()
        {
            gameObject.hideFlags = HideFlags.HideAndDontSave; // Saves some dumb tech support questions.
        }

        private void LateLoad()
        {
            Logger.LogInfo($"LateLoading {MyPluginInfo.PLUGIN_GUID}");

            if (!SteamManager.Initialized)
            {
                Logger.LogWarning("Steam is not initalized, networking will be unavailable.");
                return;
            }

            PluginInfo[] plugins = PluginsLoaded();
            string[] legacy = [.. plugins.Where(plugin => plugin.Instance is not Mod).Select(plugin => plugin.Metadata.GUID)];
            string[] modern = [.. plugins.Where(plugin => plugin.Instance is Mod).Select(plugin => plugin.Metadata.GUID)];

            Logger.LogDebug($"Loaded with the following legacy mods {string.Join(", ", legacy)}");
            Logger.LogDebug($"Loaded with the following modern mods {string.Join(", ", modern)}");

            ModButton.Init();

            gameObject.AddComponent<NetworkingManager>();
        }

        internal PluginInfo[] PluginsEnabled()
        {
            return [.. PluginsLoaded().Where(info => ((MonoBehaviour)info.Instance).enabled)];
        }

        internal PluginInfo[] PluginsLoaded()
        {
#if BEP5
            PluginInfo[] plugins = [.. Chainloader.PluginInfos.Values.Where(info => info.Instance != this)];
#elif BEP6
            PluginInfo[] plugins = [.. UnityChainloader.Instance.Plugins.Values.Where(info => info.Instance != (object)this)];
#else
#error Undefined bepinex version
#endif

            return plugins;
        }
    }
}
