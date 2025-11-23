using System;

namespace AtomicFramework.Update
{
    [Serializable]
    internal class JSONRelease(int id, string tag_name, JSONAsset[]? assets)
    {
        internal int id = id;
        internal string tag_name = tag_name;
        internal JSONAsset[] assets = assets ?? [];
    }
}
