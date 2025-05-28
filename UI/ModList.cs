using BepInEx;
using System.Linq;
using UnityEngine;

namespace AtomicFramework.UI
{
    internal class ModList: MonoBehaviour
    {
        private GameObject entryHost = new("Entries");
        private ModListEntry[] entries;

        ModList()
        {
            entryHost.transform.parent = transform;
            entryHost.SetActive(false);

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            PluginInfo[] plugins = [.. Plugin.Instance.PluginsLoaded().OrderBy(plugin => plugin.Metadata.Name)];

            entries = [.. plugins.Select(plugin =>
            {
                GameObject host = new(plugin.Metadata.GUID);
                host.transform.parent = entryHost.transform;

                ModListEntry entry = host.AddComponent<ModListEntry>();

                entry.plugin = plugin;

                return entry;
            })];

            entryHost.SetActive(true);
        }
    }
}
