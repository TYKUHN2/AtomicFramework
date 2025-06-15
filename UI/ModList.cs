using BepInEx;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AtomicFramework.UI
{
    internal class ModList: MonoBehaviour
    {
        private GameObject entryHost = new("Entries");
        private ModListEntry[] entries;

        ModList()
        {
            gameObject.SetActive(false);

            Transform parent = gameObject.transform.parent;
            RectTransform trans = gameObject.AddComponent<RectTransform>();
            trans.SetParent(parent);

            trans.anchoredPosition = Vector2.zero;
            trans.anchorMax = Vector2.one;
            trans.anchorMin = Vector2.zero;

            Image background = UIHelper.HostedComponent<Image>(gameObject, "Background");
            background.sprite = Resources.FindObjectsOfTypeAll<Image>()
                .First(image => image.sprite?.name == "LoadingScreenTarantula1")
                .sprite;

            AspectRatioFitter fitter = background.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectRatio = 2;
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

            Image mask = UIHelper.HostedComponent<Image>(gameObject, "Mask");
            mask.color = new(0.245f, 0.245f, 0.245f, 0.8f);

            AspectRatioFitter fitter2 = mask.gameObject.AddComponent<AspectRatioFitter>();
            fitter2.aspectRatio = 2;
            fitter2.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

            GameObject superLayout = new("TopLayout", [typeof(VerticalLayoutGroup)]);
            superLayout.transform.SetParent(transform, false);
            
            RectTransform superTrans = superLayout.GetComponent<RectTransform>();
            superTrans.anchorMin = Vector2.zero;
            superTrans.anchorMax = Vector2.one;

            entryHost.transform.SetParent(superLayout.transform, false);
            VerticalLayoutGroup entryGroup = entryHost.AddComponent<VerticalLayoutGroup>();
            entryGroup.childAlignment = TextAnchor.UpperLeft;

            GameObject buttonLayout = new("ButtonLayout");
            buttonLayout.transform.SetParent(superLayout.transform, false);

            HorizontalLayoutGroup buttonGroup = buttonLayout.AddComponent<HorizontalLayoutGroup>();
            buttonGroup.childAlignment = TextAnchor.LowerLeft;

            RectTransform buttonTrans = (RectTransform)buttonLayout.transform;
            buttonTrans.anchorMin = Vector2.zero;

            GameObject closeHost = new("CloseButton");
            closeHost.transform.SetParent(buttonLayout.transform, false);

            closeHost.AddComponent<ContentSizeFitter>();

            Button closeButton = closeHost.AddComponent<Button>();
            closeButton.onClick.AddListener(OnClose);

            Image closeBackground = UIHelper.HostedComponent<Image>(closeHost, "Background");
            closeBackground.color = new Color(0.245f, 0.245f, 0.245f, 0.8f);

            UIHelper.CreateLabel(closeHost, "Close");

            //Canvas canvas = gameObject.AddComponent<Canvas>();
            //canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            PluginInfo[] plugins = [.. Plugin.Instance.PluginsLoaded().OrderBy(plugin => plugin.Metadata.Name)];

            entries = [.. plugins.Select(plugin =>
            {
                GameObject host = new(plugin.Metadata.GUID);
                host.transform.SetParent(entryHost.transform, false);

                ModListEntry entry = host.AddComponent<ModListEntry>();

                entry.plugin = plugin;

                return entry;
            })];

            gameObject.SetActive(true);
        }

        private void OnClose()
        {
            Destroy(gameObject);
        }
    }
}
