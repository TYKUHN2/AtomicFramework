using BepInEx;
using UnityEngine;
using UnityEngine.UI;

namespace AtomicFramework.UI
{
    internal class ModListEntry: MonoBehaviour
    {
        internal PluginInfo plugin;

        private void Awake()
        {
            gameObject.AddComponent<ContentSizeFitter>();
            
            gameObject.AddComponent<HorizontalLayoutGroup>();

            GameObject labels = new("Labels", [typeof(VerticalLayoutGroup)]);
            labels.transform.SetParent(transform, false);

            Text label = UIHelper.CreateLabel(labels, plugin.Metadata.Name);
            label.gameObject.AddComponent<ContentSizeFitter>();

            Text version = UIHelper.CreateLabel(labels, plugin.Metadata.Version.ToString());
            version.gameObject.AddComponent<ContentSizeFitter>();

            version.gameObject.name = "Version";

            if (plugin.Instance is Mod mod)
            {
                if (mod.options.runtimeOptions == Mod.Options.Runtime.TOGGLEABLE || mod.options.runtimeOptions == Mod.Options.Runtime.RELOADABLE)
                {
                    Toggle toggle = UIHelper.HostedComponent<Toggle>(gameObject);
                    toggle.gameObject.name = "Toggle";
                    toggle.isOn = mod.enabled;
                    toggle.onValueChanged.AddListener(OnToggle);
                }

                if (mod.options.repository != string.Empty)
                    CheckUpdate();
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
