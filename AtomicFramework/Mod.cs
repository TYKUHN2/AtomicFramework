using BepInEx;

#if BEP6
using BepInEx.Unity.Mono;
#endif


namespace AtomicFramework
{

    /**
     * <summary>Parent class for all AtomicFramework compatible mods.</summary>
     */
    [BepInDependency("AtomicFramework", BepInDependency.DependencyFlags.HardDependency)]
    public abstract class Mod: BaseUnityPlugin
    {

        /**
         * <summary>Options defining mod's support and settings.</summary>
         */
        public struct Options()
        {
            /**
             * <summary>Options defining mod's support for runtime state changes.</summary>
             */
            public enum Runtime
            {
                NONE,
                TOGGLEABLE,
                RELOADABLE
            }

            /**
             * <summary>Options defining mod's requirements for multiplayer support.</summary>
             */
            public enum Multiplayer
            {
                CLIENT_ONLY,
                SERVER_ONLY,
                REQUIRES_HOST,
                REQUIRES_ALL
            }

            /// <summary>Defines a mod's github repository.</summary>
            /// <example>TYKUHN2/AtomicFramework</example>
            public string repository = string.Empty;
            public Multiplayer multiplayerOptions = Multiplayer.REQUIRES_HOST;
            public Runtime runtimeOptions = Runtime.NONE;
        }

        /**
         * <summary>Access to safe networking functionality.</summary>
         */
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
