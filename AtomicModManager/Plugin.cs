using System.Reflection;

namespace AtomicFramework
{
    internal class Plugin
    {
        internal struct Dependency
        {
            internal required string guid;
            internal SemanticVersioning.Range? version;
            internal required bool hard;
            internal Plugin? loaded;
        }

        internal readonly Version bepVersion;
        internal readonly Version? atomicVersion;
        internal readonly Dependency[] dependencies;

        internal readonly string display_name;
        internal readonly string guid;
        internal readonly Version version;

        internal bool hasBep = false;
        internal bool hasAtomic = false;
        internal bool foundDependencies = false;

        internal Plugin(Version bepVersion, string display_name, string guid, Version version)
        {
            dependencies = [];

            this.bepVersion = bepVersion;
            this.display_name = display_name;
            this.guid = guid;
            this.version = version;
        }

        internal Plugin(Type basePlugin, Assembly plugin)
        {
            AssemblyName[] refs = [.. plugin.GetReferencedAssemblies().Where(n => n.Name?.Contains("BepInEx") ?? false)];

            if (refs.Length == 0)
                throw new ArgumentException($"Assembly {plugin.FullName} not a plugin");

            if (refs[0].Version != null)
                bepVersion = refs[0].Version!;
            else
            {
                if (refs.Any(n => n.Name == "BepInEx.Core"))
                    bepVersion = new(6, 0);
                else
                    bepVersion = new(5, 0);
            }

            refs = [.. plugin.GetReferencedAssemblies().Where(n => n.Name?.Contains("AtomicFramework") ?? false)];

            if (refs.Length != 0)
            {
                if (refs[0].Version != null)
                    atomicVersion = refs[0].Version!;
                else
                    atomicVersion = new(0, 0);
            }

            Type[] entries = [.. plugin.GetTypes().Where(t =>
            {
                if (!t.IsClass)
                    return false;

                if (t.IsAbstract)
                    return false;

                return t.IsSubclassOf(basePlugin);
            })];

            if (entries.Length == 1)
            {
                Type cur = entries[0];
                List<CustomAttributeData> attrs = [];

                while (!cur.FullName!.StartsWith("BepInEx") && !cur.FullName.EndsWith("BaseUnityPlugin"))
                {
                    attrs.AddRange(cur.CustomAttributes);
                    cur = cur.BaseType!;
                }

                CustomAttributeData? pluginAttr = attrs.FirstOrDefault(a => a.AttributeType.Name == "BepInPlugin");
                if (pluginAttr == null)
                {
                    display_name = "Error";
                    guid = "Error";
                    version = new(0, 0);
                }
                else
                {
                    display_name = pluginAttr.ConstructorArguments[1].Value as string ?? "Error";
                    guid = pluginAttr.ConstructorArguments[0].Value as string ?? "Error";
                    version = new(pluginAttr.ConstructorArguments[2].Value as string ?? "0.0");
                }

                Console.WriteLine($"Processing {guid}");

                CustomAttributeData[] deps = [.. attrs.Where(a => a.AttributeType.Name == "BepInDependency")];
                dependencies = [.. deps.Select(d =>
                {
                    var args = d.ConstructorArguments;

                    string id = (args[0].Value as string)!;
                    SemanticVersioning.Range? range = null;
                    bool hard = true;

                    if (args[1].ArgumentType.IsEnum)
                        hard = ((int)args[1].Value!) == 1;
                    else
                        range = new((args[1].Value as string)!, true);

                    Console.WriteLine($"{guid} depends on {id}");

                    return new Dependency
                    {
                        guid = id,
                        version = range,
                        hard = hard
                    };
                }).Where(d => d.guid != "AtomicFramework")];
            }
            else
            {
                Console.Error.WriteLine($"With {basePlugin.FullName}");
                foreach (Type type in plugin.GetTypes())
                {
                    if (type.IsClass && !type.IsAbstract)
                        Console.Error.WriteLine($"Tried {type.FullName}");
                }
                    

                throw new ArgumentException($"Plugin {plugin.GetName().Name} missing BaseUnityPlugin");
            }
        }
    }
}
