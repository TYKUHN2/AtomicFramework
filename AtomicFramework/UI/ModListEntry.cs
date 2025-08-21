using BepInEx;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AtomicFramework.UI
{
    internal class ModListEntry: MonoBehaviour
    {
#pragma warning disable CS8618
        internal PluginInfo plugin;
#pragma warning restore CS8618

        private void Awake()
        {
            TextMeshProUGUI label = transform.Find("Labels/Name").GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI version = transform.Find("Labels/Version").GetComponent<TextMeshProUGUI>();

            label.text = plugin.Metadata.Name;
            version.text = plugin.Metadata.Version.ToString();

            transform.Find("Buttons/Update").gameObject.SetActive(false);
            GameObject toggle = transform.Find("Buttons/Toggle").gameObject;

            PluginInfo[] plugins = [Plugin.Instance.Info, .. Plugin.Instance.PluginsLoaded().OrderBy(plugin => plugin.Metadata.Name)];

            if (plugin.Instance is Mod mod)
            {
                if (mod.options.runtimeOptions == Mod.Options.Runtime.TOGGLEABLE || mod.options.runtimeOptions == Mod.Options.Runtime.RELOADABLE)
                {
                    toggle.SetActive(true);

                    Toggle comp = toggle.GetComponent<Toggle>();
                    comp.isOn = mod.enabled;
                    comp.onValueChanged.AddListener(OnToggle);
                }
                else
                    toggle.SetActive(false);

                if (mod.options.repository != string.Empty)
                    CheckUpdate();
            }
            else
                toggle.SetActive(false);
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
