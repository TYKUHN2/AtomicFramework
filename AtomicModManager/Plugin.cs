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

        internal Plugin(Assembly plugin)
        {
            Console.WriteLine($"Processing {plugin.FullName}");
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
                try {

                    if (!t.IsClass)
                        return false;

                    if (t.IsAbstract)
                        return false;

                    return IsPlugin(t);
                }
                catch (FileNotFoundException)
                {
                    Console.Error.WriteLine($"Exception testing for plugin with {t.FullName}");

                    return false;
                }
            })];

            if (entries.Length == 1)
            {
                Type cur = entries[0];
                List<CustomAttributeData> attrs = [];
                CustomAttributeData? pluginAttr = null;

                while (!cur.FullName!.StartsWith("BepInEx"))
                {
                    Console.Out.WriteLine($"Searching attributes of {cur.FullName}");
                    attrs.AddRange(cur.CustomAttributes);

                    foreach (CustomAttributeData attr in cur.CustomAttributes)
                    {
                        try
                        {
                            if (attr.AttributeType.Name == "BepInPlugin")
                                pluginAttr = attr;
                        }
                        catch (FileNotFoundException)
                        {
                            Console.Error.WriteLine($"Error extracting attribute from {cur.FullName}");
                        }
                    }

                    cur = cur.BaseType!;
                }

                if (pluginAttr == null)
                {
                    display_name = plugin.GetName().Name!;
                    guid = "Error";
                    version = new(0, 0);

                    dependencies = [];

                    Console.Out.WriteLine($"Class {entries[0].FullName} is a plugin or plugin derivative but we cannot extract the BepInPlugin attribute.");

                    return;
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
                throw new ArgumentException($"Plugin {plugin.GetName().Name} missing BaseUnityPlugin");
        }

        private static bool IsPlugin(Type test)
        {
            Type? cur = test.BaseType;
            while (cur != null)
            {
                if (cur.FullName!.StartsWith("System") || cur.FullName.StartsWith("Unity") ||
                    cur.FullName.StartsWith("Mono"))
                    return false;

                if (cur.FullName == "BepInEx.Unity.Mono.BaseUnityPlugin" || cur.FullName == "BepInEx.BaseUnityPlugin")
                    return true;

                cur = cur.BaseType;
            }

            return false;
        }
    }
}
