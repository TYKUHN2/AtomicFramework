using UnityEngine;
using UnityEngine.UI;

namespace AtomicFramework.UI
{
    internal static class UIHelper
    {
        internal static readonly Font LIBERATION_SANS;

        static UIHelper()
        {
            Font[] fonts = Resources.FindObjectsOfTypeAll<Font>();
            foreach (Font font in fonts)
            {
                if (font.name == "LiberationSans")
                    LIBERATION_SANS = font;
            }
        }

        internal static Text CreateLabel(GameObject host, string text, Font? font = null)
        {
            GameObject label = new("Label");
            label.transform.SetParent(host.transform, false);

            Text comp = label.AddComponent<Text>();

            comp.font = font ?? LIBERATION_SANS;
            comp.text = text;

            return comp;
        }

        internal static T HostedComponent<T> (GameObject host, string name = "GameObject") where T : Component
        {
            GameObject obj = new(name);
            obj.transform.SetParent(host.transform, false);

            return obj.AddComponent<T>();
        }
    }
}
