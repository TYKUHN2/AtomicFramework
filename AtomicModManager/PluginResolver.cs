using System.Reflection;

namespace AtomicFramework
{
    internal class PluginResolver: MetadataAssemblyResolver
    {
        private readonly string bep_path;
        private readonly string managed_path;

        internal PluginResolver(string bep_path, string managed_path)
        {
            this.bep_path = bep_path;
            this.managed_path = managed_path;
        }

        public override Assembly? Resolve(MetadataLoadContext ctx, AssemblyName target)
        {
            Console.WriteLine($"Resolving {target.Name}");

            IEnumerable<string> plugins = GetAssemblies();
            foreach (string plugin in plugins)
            {
                try
                {
                    AssemblyName potential = AssemblyName.GetAssemblyName(plugin);

                    if (CompareNames(target, potential))
                        return ctx.LoadFromAssemblyPath(plugin);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                }
            }

            return null;
        }

        private IEnumerable<string> GetAssemblies()
        {
            IEnumerable<string> plugins = Directory.EnumerateFiles(Path.Combine(bep_path, "plugins"), "*.dll", SearchOption.AllDirectories);
            foreach (string plugin in plugins)
                yield return plugin;

            plugins = Directory.EnumerateFiles(Path.Combine(bep_path, "core"), "*.dll", SearchOption.AllDirectories);
            foreach (string plugin in plugins)
                yield return plugin;

            plugins = Directory.EnumerateFiles(managed_path, "*.dll", SearchOption.AllDirectories);
            foreach (string plugin in plugins)
                yield return plugin;
        }

        private static bool CompareNames(AssemblyName target, AssemblyName sample)
        {
            if (target.Version == null ||
                (target.Version.Major <= 0 && target.Version.Minor <= 0 &&
                target.Version.Revision <= 0 && target.Version.Build <= 0))
            {
                return target.Name == sample.Name;
            }
            else
            {
                return target.Name == sample.Name &&
                    (target.Version.Major == sample.Version!.Major && target.Version.Minor <= sample.Version.Minor);
            }
        }
    }
}
