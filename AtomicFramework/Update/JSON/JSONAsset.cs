using System;

namespace AtomicFramework
{
    [Serializable]
    internal class JSONAsset(int id, string name, string browser_download_url)
    {
        internal int id = id;
        internal string name = name;
        internal string browser_download_url = browser_download_url;
    }
}
