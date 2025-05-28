using BepInEx;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AtomicFramework.UI
{
    internal class ModListEntry: MonoBehaviour
    {
        private static readonly Font font = Resources.FindObjectsOfTypeAll<Font>().First(font => font.name == "LiberationSans");

        internal PluginInfo plugin;

        private void Awake()
        {
            gameObject.AddComponent<CanvasGroup>();

            GameObject labelHost = new("Label");
            labelHost.transform.parent = transform;

            Text label = labelHost.AddComponent<Text>();
            label.font = font;

            GameObject versionHost = new("Version");
            versionHost.transform.parent = transform;

            Text version = versionHost.AddComponent<Text>();
            version.font = font;

            label.text = plugin.Metadata.Name;
            version.text = plugin.Metadata.Version.ToString();

            if (plugin.Instance is Mod mod)
            {
                if (mod.options.runtimeOptions == Mod.Options.Runtime.TOGGLEABLE || mod.options.runtimeOptions == Mod.Options.Runtime.RELOADABLE)
                {
                    GameObject toggleHost = new("Toggle");
                    toggleHost.transform.parent = transform;

                    Toggle enable = toggleHost.AddComponent<Toggle>();
                    enable.isOn = mod.enabled;

                    enable.onValueChanged.AddListener(OnToggle);
                }

                if (mod.options.repository != string.Empty)
                {
                    CheckUpdate();
                }
            }
        }

        private void OnToggle(bool enabled)
        {
            ((Mod)plugin.Instance).enabled = enabled;
        }

        private void CheckUpdate()
        {
            
        }
    }
}
