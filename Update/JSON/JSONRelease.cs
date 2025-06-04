using System;

namespace AtomicFramework
{
    [Serializable]
    internal class JSONRelease
    {
        internal int id;
        internal string tag_name;
        internal JSONAsset[] assets;
    }
}
