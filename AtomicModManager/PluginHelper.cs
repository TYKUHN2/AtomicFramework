using System.Reflection;
using Mono.Cecil;

namespace AtomicFramework
{
    internal static class PluginHelper
    {
        internal static Assembly GetBepInEx(MetadataLoadContext mlc, string bep_path)
        internal static AssemblyDefinition? GetBepInEx()
        {
            if (Program.BEP_PATH == null)
                return null;

            string core_path = Path.Combine(Program.BEP_PATH, "core");
            string path;

            if (File.Exists(Path.Combine(core_path, "BepInEx.Unity.Mono.dll")))
                path = Path.Combine(core_path, "BepInEx.Unity.Mono.dll");
            else if (File.Exists(Path.Combine(core_path, "BepInEx.dll")))
                path = Path.Combine(core_path, "BepInEx.dll");
            else
                return null;

            return AssemblyDefinition.ReadAssembly(path);
        }

        internal static Plugin[]? GetPlugins()
        {
            if (Program.BEP_PATH == null)
                return null;

            IEnumerable<AssemblyDefinition> plugins = Directory.EnumerateFiles(Path.Combine(Program.BEP_PATH, "plugins"), "*.dll", SearchOption.AllDirectories)
                .Where(Plugin.IsPlugin)
                .Select(Plugin.Load)
                .OfType<AssemblyDefinition>();

            AssemblyDefinition? bepInEx = GetBepInEx();
            if (bepInEx ==  null)
                return null;

            Version bepVersion = bepInEx.Name.Version ?? (bepInEx.Name.Name == "BepInEx.Unity.Mono" ? new(6, 0) : new(5, 0));
            Plugin[] output = [new(bepVersion, "BepInEx", "BepInEx", bepVersion), .. plugins.Select(p => {
                try {
                    return new Plugin(p);
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }).OfType<Plugin>()];

            ResolveDependencies(output);

            return output;
        }

        internal static void ResolveDependencies(Plugin[] plugins)
        {
            Plugin bepInEx = plugins.First(p => p.guid == "BepInEx");
            Plugin atomic = plugins.First(p => p.guid == "AtomicFramework");

            plugins = [.. plugins.Where(p => p != bepInEx && p != atomic)];

            bepInEx.hasBep = true;
            bepInEx.hasAtomic = true;
            bepInEx.foundDependencies = true;

            atomic.hasBep = bepInEx.version.Major == atomic.bepVersion.Major;
            atomic.hasAtomic = true;
            atomic.foundDependencies = true;

            foreach (Plugin plugin in plugins)
            {
                plugin.hasBep = bepInEx.version.Major == plugin.bepVersion.Major;
                plugin.hasAtomic = plugin.atomicVersion == null || plugin.atomicVersion.Major == atomic.version.Major;
                plugin.foundDependencies = plugin.dependencies.All(d =>
                {
                    if (!d.hard)
                        return true;

                    return plugins.Any(p => {
                        bool match = p.guid == d.guid &&
                        (d.version == null || d.version.IsSatisfied(p.version.ToString(), true, true));

                        if (match)
                            d.loaded = p;

                        return match;
                    });
                });
            }
        }
    }
}
