using SemanticVersioning;

namespace AtomicFramework.Update
{
    internal record class Release(int id, Version version, string name, int asset)
    {
        internal readonly int id = id;
        internal readonly Version version = version;
        internal readonly string name = name;
        internal readonly int asset = asset;
    }
}
