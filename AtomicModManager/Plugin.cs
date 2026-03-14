using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;
using MonoMod.Cil;
using System.Text;

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
		
        internal readonly string? filename;

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
			
			filename = null;
        }

        internal Plugin(AssemblyDefinition plugin)
        {
            Program.Log.LogDebug($"Processing {plugin.FullName}");
            AssemblyNameReference[] refs = [.. plugin.MainModule.AssemblyReferences.Where(n => n.Name?.Contains("BepInEx") ?? false)];

            filename = plugin.MainModule.FileName;

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

            refs = [.. plugin.MainModule.AssemblyReferences.Where(n => n.Name?.Contains("AtomicFramework") ?? false)];

            if (refs.Length != 0)
            {
                if (refs[0].Version != null)
                    atomicVersion = refs[0].Version!;
                else
                    atomicVersion = new(0, 0);
            }

            TypeDefinition[] entries = [.. plugin.MainModule.Types.Where(IsPlugin)];

            if (entries.Length == 1)
            {
                TypeDefinition cur = entries[0];
                List<CustomAttribute> attrs = [];
                CustomAttribute? pluginAttr = null;

                while (true)
                {
                    Program.Log.LogDebug($"Searching attributes of {cur.FullName}");
                    attrs.AddRange(cur.CustomAttributes);

                    foreach (CustomAttribute attr in cur.CustomAttributes)
                    {
                        try
                        {
                            if (attr.AttributeType.Name == "BepInPlugin")
                                pluginAttr = attr;
                        }
                        catch (FileNotFoundException)
                        {
                            Program.Log.LogError($"Error extracting attribute from {cur.FullName}");
                        }
                    }

                    if (!cur.BaseType.Namespace.StartsWith("BepInEx"))
                        cur = cur.BaseType.Resolve();
                    else
                        break;
                }

                if (pluginAttr == null)
                {
                    display_name = plugin.Name.Name;
                    guid = "Error";
                    version = new(0, 0);

                    dependencies = [];

                    Program.Log.LogDebug($"Class {entries[0].FullName} is a plugin or plugin derivative but we cannot extract the BepInPlugin attribute.");

                    return;
                }
                else
                {
                    display_name = pluginAttr.ConstructorArguments[1].Value as string ?? "Error";
                    guid = pluginAttr.ConstructorArguments[0].Value as string ?? "Error";
                    version = new(pluginAttr.ConstructorArguments[2].Value as string ?? "0.0");
                }

                Program.Log.LogDebug($"Processing {guid}");

                CustomAttribute[] deps = [.. attrs.Where(a => a.AttributeType.Name == "BepInDependency")];
                dependencies = [.. deps.Select(d =>
                {
                    Collection<CustomAttributeArgument> args;

                    // We'll crash trying to load the enum
                    if (d.Constructor.Parameters[1].ParameterType.IsValueType)
                    {
                        byte[] blob = d.GetBlob();

                        args = [
                            new(
                                d.Constructor.Module.GetTypeReferences().First(@ref => @ref.FullName == "System.String"),
                                Encoding.UTF8.GetString(blob, 3, blob[2])
                                ),
                            new(
                                d.Constructor.Module.GetTypeReferences().First(@ref => @ref.FullName == "BepInEx.BepInDependency/DependencyFlags"),
                                (int)(blob[blob[2] + 3]))
                        ];
                    }
                    else
                        args = d.ConstructorArguments;

                    string id = (args[0].Value as string)!;
                    SemanticVersioning.Range? range = null;
                    bool hard = true;

                    if (args[1].Type.Name == "DependencyFlags")
                        hard = ((int)args[1].Value!) == 1;
                    else
                        range = new((args[1].Value as string)!, true);

                    Program.Log.LogDebug($"{guid} depends on {id}");

                    return new Dependency
                    {
                        guid = id,
                        version = range,
                        hard = hard
                    };
                }).Where(d => d.guid != "AtomicFramework")];
            }
            else
                throw new ArgumentException($"Plugin {plugin.Name.Name} missing BaseUnityPlugin");
        }

        internal static bool IsPlugin(string name)
        {
            AssemblyDefinition? ass = Load(name);
            if (ass == null)
                return false;

            if (!ass.MainModule.AssemblyReferences.Any(r => r.Name.StartsWith("BepInEx")))
                return false;

            TypeDefinition[] candidates = [.. ass.MainModule.Types.Where(t => !t.IsAbstract && !t.IsInterface)];

            return candidates.Any(IsPlugin);
        }

        private static bool IsPlugin(TypeDefinition test)
        {
            if (test.IsAbstract || test.IsInterface || test.BaseType == null)
                return false;

            List<TypeDefinition> tree = [test];
            try
            {
                while (true)
                {
                    TypeReference cur = tree[^1].BaseType;

                    if (cur.Namespace.StartsWith("System") || cur.Namespace.StartsWith("Unity") ||
                        cur.Namespace.StartsWith("Mono"))
                        return false;

                    if (cur.Namespace.StartsWith("BepInEx") && cur.Name == "BaseUnityPlugin")
                        break;

                    tree.Add(cur.Resolve());
                }
            }
            catch
            {
                return false;
            }

            if (tree.SelectMany(t => t.CustomAttributes).Any(a => a.AttributeType.Name == "BepInPlugin"))
                return true;

            return false;
        }
		
		internal static AssemblyDefinition? Load(string path)
        {
            if (!Path.IsPathRooted(path))
            {
                if (Program.BEP_PATH == null || !path.EndsWith(".dll"))
                    return null;

                path = Path.Combine(Program.BEP_PATH, "plugins", path);
            }

            return AssemblyDefinition.ReadAssembly(path,
                new ReaderParameters(ReadingMode.Deferred) { AssemblyResolver = new RecursivePluginResolver() });
        }
        private class RecursivePluginResolver : IAssemblyResolver
        {
            public void Dispose()
            { }

            public AssemblyDefinition Resolve(AssemblyNameReference name)
            {
                return Resolve(name, new ReaderParameters(ReadingMode.Deferred) { AssemblyResolver = this });
            }

            public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
            {
                if (Program.BEP_PATH == null)
                    throw new AssemblyResolutionException(name);

                string basePath = Path.Combine(Program.BEP_PATH, "plugins");

                string[] found = Directory.GetFiles(basePath, name.Name + ".dll", SearchOption.AllDirectories);

                if (found.Length == 0)
                    throw new AssemblyResolutionException(name);

                return AssemblyDefinition.ReadAssembly(
                    File.Open(found[0], FileMode.Open, FileAccess.Read, FileShare.Read),
                    parameters
                );
            }
        }
    }
}
