using System.Reflection;

namespace AtomicFramework
{
    internal class PluginResolver(string bep_path, string managed_path): MetadataAssemblyResolver
    {
        private readonly string bep_path = bep_path;
        private readonly string managed_path = managed_path;

        public override Assembly? Resolve(MetadataLoadContext context, AssemblyName assemblyName)
        {
            Console.WriteLine($"Resolving {assemblyName.Name}");

            string[] plugins = Directory.GetFiles(Path.Combine(bep_path, "plugins"), assemblyName.Name + ".dll", SearchOption.AllDirectories);
            if (plugins.Length > 0)
                return context.LoadFromAssemblyPath(plugins[0]);

            plugins = Directory.GetFiles(Path.Combine(bep_path, "core"), assemblyName.Name + ".dll");
            if (plugins.Length > 0)
                return context.LoadFromAssemblyPath(plugins[0]);

            plugins = Directory.GetFiles(managed_path, assemblyName.Name + ".dll");
            if (plugins.Length > 0)
                return context.LoadFromAssemblyPath(plugins[0]);

            return null;
        }
    }
}
