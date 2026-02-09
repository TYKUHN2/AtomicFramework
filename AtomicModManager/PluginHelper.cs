using System.Linq.Expressions;
using System.Reflection;

namespace AtomicFramework
{
    internal static class PluginHelper
    {
        internal static string GetBepInEx(string bep_path)
        {
            string core_path = Path.Combine(bep_path, "core");

            if (File.Exists(Path.Combine(core_path, "BepInEx.Unity.Mono.dll")))
                return Path.Combine(core_path, "BepInEx.Unity.Mono.dll");
            else
                return Path.Combine(core_path, "BepInEx.dll");
        }

        internal static Plugin[]? GetPlugins()
        {
            string? bep_path = Environment.GetEnvironmentVariable("BEP_PATH");
            if (bep_path != null && !Path.IsPathFullyQualified(bep_path))
                bep_path = null;

            string? managed_path = Environment.GetEnvironmentVariable("MANAGE_PATH");
            if (managed_path != null && !Path.IsPathFullyQualified(managed_path))
                managed_path = null;

            if (bep_path == null || managed_path == null)
            {
                if (bep_path != null)
                    managed_path = Path.GetFullPath("../NuclearOption_Data/Managed", bep_path);
                else if (managed_path != null)
                    bep_path = Path.GetFullPath("../../BepInEx", managed_path);
                else
                {
                    managed_path = Environment.GetEnvironmentVariable("NUCLEAR_OPTION_REFERENCES");

                    // If we don't set bep_path, we won't continue processing, so we don't need to reclear managed_path
                    if (managed_path != null && Path.IsPathFullyQualified(managed_path))
                        bep_path = Path.GetFullPath("../../BepInEx", managed_path);
                }
            }

            if (bep_path == null)
                return null;

            PluginResolver resolver = new(bep_path, managed_path!);
            MetadataLoadContext mlc = new(resolver);

            IEnumerable<Assembly> potentials = Directory.EnumerateFiles(Path.Combine(bep_path, "plugins"), "*.dll", SearchOption.AllDirectories)
                .Select(p =>
                {
                    try { return mlc.LoadFromAssemblyPath(p); } catch { return null; }
                })
                .OfType<Assembly>();

            IEnumerable<Assembly> plugins = potentials.Where(a => a.GetReferencedAssemblies().Any(n => n.Name?.Contains("BepInEx") ?? false));

            Assembly bepInEx = mlc.LoadFromAssemblyPath(GetBepInEx(bep_path));
            Type? basePlugin = bepInEx.GetType(bepInEx.GetName().Name == "BepInEx.Unity.Mono" ? "BepInEx.Unity.Mono.BaseUnityPlugin" : "BepInEx.BaseUnityPlugin");
            if (basePlugin == null)
            {
                Console.Error.WriteLine($"Failed to find BaseUnityPlugin from {bepInEx.GetName().Name}");
                Console.Error.WriteLine(string.Join(", ", bepInEx.GetExportedTypes().Where(t => t.IsClass)));
                throw new ArgumentNullException("BaseUnityPlugin Null");
            }

            Version bepVersion = bepInEx.GetName().Version ?? (bepInEx.GetName().Name == "BepInEx.Unity.Mono" ? new(6, 0) : new(5, 0));
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
