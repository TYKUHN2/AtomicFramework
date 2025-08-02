using BepInEx;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AtomicFramework.UI
{
    internal class ModList: MonoBehaviour
    {
        private static readonly GameObject MenuPrefab;
        private static readonly GameObject EntryPrefab;

        private readonly ModListEntry[] entries;

        ModList()
        {
            transform.Find("Background")
                .GetComponent<Image>()
                .sprite = Resources.FindObjectsOfTypeAll<Image>()
                .First(image => image.sprite?.name == "LoadingScreenTarantula1")
                .sprite;

            Transform list = transform.Find("Scroll View/Viewport/List");

            transform.Find("ButtonList/Button").GetComponent<Button>().onClick.AddListener(OnClose);

            PluginInfo[] plugins = [Plugin.Instance.Info, .. Plugin.Instance.PluginsLoaded().OrderBy(plugin => plugin.Metadata.Name)];

            entries = [.. plugins.Select(plugin =>
            {
                GameObject host = Instantiate(EntryPrefab, list);
                host.SetActive(false);

                ModListEntry entry = host.AddComponent<ModListEntry>();

                entry.plugin = plugin;

                host.SetActive(true);

                return entry;
            })];
        }

        static ModList()
        {
            AssetBundle prefabs = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Plugin.Instance.Info.Location), "modlistprefab"));

            MenuPrefab = prefabs.LoadAsset<GameObject>("Menu");
            EntryPrefab = prefabs.LoadAsset<GameObject>("Entry");

            prefabs.Unload(false);
        }

        private void OnClose()
        {
            Destroy(gameObject);
        }

        internal static void Attach(Transform parent)
        {
            GameObject menu = Instantiate(MenuPrefab, parent);
            menu.AddComponent<ModList>();
        }
    }
}
