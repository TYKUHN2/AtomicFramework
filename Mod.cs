using BepInEx;

namespace AtomicFramework
{

    [BepInDependency("AtomicFramework", BepInDependency.DependencyFlags.HardDependency)]
    public abstract class Mod: BaseUnityPlugin
    {
        public enum MultiplayerOptions
        {
            CLIENT_ONLY,
            SERVER_ONLY,
            REQUIRES_HOST,
            REQUIRES_ALL
        }

        protected internal NetworkAPI? Networking;

        internal readonly MultiplayerOptions multiplayerOptions;
        internal readonly bool toggleable;

        protected Mod(MultiplayerOptions multiplayer = MultiplayerOptions.REQUIRES_HOST, bool toggleable = false)
        {
            if (NetworkingManager.instance != null)
                Networking = new(Info.Metadata.GUID);

            multiplayerOptions = multiplayer;
            this.toggleable = toggleable;
        }
    }
}
