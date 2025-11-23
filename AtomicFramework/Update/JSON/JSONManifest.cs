using System;

namespace AtomicFramework.Update
{
    [Serializable]
    internal class JSONManifest(JSONMod[]? mods)
    {
        internal JSONMod[] mods = mods ?? [];
    }
}
