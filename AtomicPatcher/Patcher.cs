#if BEP6
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Preloader.Core.Patching;
using HarmonyLib;
#endif

using Mono.Cecil;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace AtomicFramework
{

#if BEP5
public class Patcher
    {
        public static IEnumerable<string> TargetDLLs
        {
            get
            {
                return ["UnityEngine.CoreModule.dll"];
            }
        }

        public static void Patch(AssemblyDefinition assembly)
        {
            assembly.MainModule.AssemblyReferences.Add(new("AtomicFramework", new(MyPluginInfo.PLUGIN_VERSION)));
        }
    }
#endif

#if BEP6
    [PatcherPluginInfo(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Patcher : BasePatcher
    {
        private static readonly Harmony harmony = new("xyz.tyknet.NuclearOption.Preload");

        public override void Initialize()
        {
            base.Initialize();

            Log.LogInfo("Patcher initalized");

            // We need to use Harmony because BepInEx.Core is already loaded
            // Also need to use some random parent types to prevent loading Unity too early.
            MethodInfo[] methods = typeof(BaseChainloader<>)
                .MakeGenericType(typeof(object))
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo target = methods.First(m => m.Name.StartsWith("HasBepinPlugin")); // They change the name

            if (target.ContainsGenericParameters)
            {
                target = target.MakeGenericMethod(typeof(object));
            }

            harmony.Patch(
                target,
                postfix: new HarmonyMethod(((Delegate)Postfix).GetMethodInfo())
            );
        }

        static bool Postfix(bool result, AssemblyDefinition ass)
        {
            if (result)
                return true;

            Console.WriteLine("Patch needed here!");

            Assembly framework = Assembly.ReflectionOnlyLoadFrom(Path.Combine(Paths.PluginPath, "AtomicFramework.dll"));
            string frameworkName = framework.FullName;

            return ass.MainModule.AssemblyReferences.Any(a => a.Name == frameworkName)
                && ass.MainModule.GetTypeReferences().Any(a => a.FullName == "AtomicFramework.Mod");
        }
    }
#endif
}
