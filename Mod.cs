using BepInEx;

namespace AtomicFramework
{

    [BepInDependency("AtomicFramework", BepInDependency.DependencyFlags.HardDependency)]
    public abstract class Mod: BaseUnityPlugin
    {
        public struct Options()
        {
            public enum Runtime
            {
                NONE,
                TOGGLEABLE,
                RELOADABLE
            }

            public enum Multiplayer
            {
                CLIENT_ONLY,
                SERVER_ONLY,
                REQUIRES_HOST,
                REQUIRES_ALL
            }

            public string repository = string.Empty;
            public Multiplayer multiplayerOptions = Multiplayer.REQUIRES_HOST;
            public Runtime runtimeOptions = Runtime.NONE;
        }

        protected internal NetworkAPI? Networking;

        internal readonly Options options;

        protected Mod(Options options)
        {
            if (NetworkingManager.instance != null)
                Networking = new(Info.Metadata.GUID);

            this.options = options;
        }
    }
}
