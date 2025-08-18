using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AtomicFramework.UI
{
    internal class ModButton
    {
        internal static void Init()
        {
            SceneManager.activeSceneChanged += InjectMenu;

            InjectMenu(default, default);
        }

        private static void InjectMenu(Scene old, Scene next)
        {
            if (GameManager.gameState != GameManager.GameState.Menu)
                return;

            GameObject buttonList = GameObject.Find("MainCanvas/Prejoin menu/LeftPanel/Container/MenuButtonsPanel");

            GameObject button = GameObject.Instantiate(buttonList.transform.GetChild(0).gameObject, buttonList.transform);
            button.name = "ModButton";

            Button comp = button.GetComponent<Button>();

            GameObject textObj = button.transform.GetChild(0).gameObject;
            textObj.name = "ModButtonLabel";

            TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
            text.text = "MODS";

            for (int i = 0; i < comp.onClick.GetPersistentEventCount(); i++)
                comp.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);

            comp.onClick.AddListener(OpenModMenu);
        }

        private static void OpenModMenu()
        {

            ModList.Attach(GameObject.Find("MainCanvas/OverlayMenuLayer").transform);
        }
    }
}
