#if BEP6
using AtomicFramework.Communication;
using BepInEx;
using BepInEx.Preloader.Core.Patching;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
#endif

[assembly: InternalsVisibleTo("BepInEx.Unity.Mono")]

namespace AtomicFramework
{

#if BEP5
public partial class Patcher
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
            Impl();
        }
    }
#endif

#if BEP6
    [PatcherPluginInfo(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public partial class Patcher : BasePatcher
    {
        public override void Initialize()
        {
            base.Initialize();
            Impl();
        }

        [TargetAssembly("BepInEx.Unity.Mono.dll")]
        private void PatchChainloader(AssemblyDefinition assembly)
        {
            try
            {
                Log.LogDebug("Fetching HookBepInEx");
                AssemblyDefinition plugin = AssemblyDefinition.ReadAssembly(Path.Combine(Paths.PluginPath, "AtomicFramework.dll"));
                MethodReference hookRef = plugin.MainModule.Types.First(t => t.Name == "EarlyHook")
                    .Methods.First(m => m.Name == "HookBepInEx");

                MethodReference ourImport = assembly.MainModule.ImportReference(hookRef);

                Log.LogDebug("Fetching UnityChainloader");
                TypeDefinition chainloader = assembly.MainModule.Types
                    .Where(t => t.IsClass && t.FullName == "BepInEx.Unity.Mono.Bootstrap.UnityChainloader").First();

                Log.LogDebug("Patching Init");
                MethodDefinition init = chainloader.Methods.Where(m => m.Name == "Initialize").First();
                Instruction first = init.Body.Instructions[0];
                ILProcessor proc = init.Body.GetILProcessor();

                proc.InsertBefore(first, proc.Create(OpCodes.Call, ourImport));
            }
            catch (Exception e)
            {
                Log.LogError($"Uncaught exception in Patcher.PatchChainloader: {e.ToString()}");

                while (true)
                    Process.GetCurrentProcess().WaitForExit();
            }
        }
    }
#endif

    public partial class Patcher
    {
        public static string[] NATIVE_DISABLED
        { get; private set; }

        public static string[] ATOMIC_DISABLED
        { get; private set; }


        static Patcher()
        {
            NATIVE_DISABLED = [];
            ATOMIC_DISABLED = [];
        }

        private void Impl()
        {
            try
            {
                Log.LogInfo("AtomicFramework Patcher Initialized");

                string filename;
                switch (Environment.OSVersion.Platform)
                {
                    case PlatformID.Win32NT:
                        filename = "AtomicModManager.exe";
                        break;
                    default:
                        Log.LogWarning("Skipping mod manager, unsupported OS");
                        return;
                }

                Process newProc = new();
                newProc.StartInfo.FileName = Path.Combine(Paths.PatcherPluginPath, "AtomicFramework", filename);
                newProc.StartInfo.Environment["BEP_PATH"] = Paths.BepInExRootPath;
                newProc.StartInfo.Environment["MANAGE_PATH"] = Paths.ManagedPath;
                newProc.StartInfo.UseShellExecute = false;
                newProc.StartInfo.RedirectStandardInput = true;
                newProc.StartInfo.RedirectStandardOutput = true;
                newProc.Start();

                ModManager manager = new(newProc.StandardOutput, newProc.StandardInput);

                NATIVE_DISABLED = manager.ReadPlugins();
                ATOMIC_DISABLED = manager.ReadPlugins();

                if (NATIVE_DISABLED.Length > 0)
                    Log.LogInfo($"Blocking {String.Join(", ", NATIVE_DISABLED)}");

                if (ATOMIC_DISABLED.Length > 0)
                    Log.LogInfo($"Disabling {String.Join(", ", ATOMIC_DISABLED)}");

                if (NATIVE_DISABLED.Contains("AtomicFramework"))
                    Log.LogWarning("WARNING: AtomicFramework must be loaded to function properly. It will not be awoken.");
            }
            catch (Exception e)
            {
                Log.LogError($"Uncaught exception in Patcher.Impl: {e.ToString()}");

                while (true)
                    Process.GetCurrentProcess().WaitForExit();
            }
        }
    }
}
