using AtomicFramework.Communication;
using BepInEx;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx.Logging;

#if BEP6
using BepInEx.Preloader.Core.Patching;
#else
using System.Collections.Generic;
using BepInEx.Logging;
#endif

[assembly: InternalsVisibleTo("BepInEx.Unity.Mono")]

namespace AtomicFramework
{

#if BEP5
    public partial class Patcher
    {
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource(MyPluginInfo.PLUGIN_NAME);

        public static IEnumerable<string> TargetDLLs
        {
            get
            {
                return ["BepInEx.dll"];
            }
        }

        public static void Initialize()
        {
            Patcher impl = new();
            impl.Impl();
        }

        public static void Patch(AssemblyDefinition assembly)
        {
            try
            {
                Log.LogDebug("Fetching HookBepInEx");
                AssemblyDefinition plugin = AssemblyDefinition.ReadAssembly(Path.Combine(Paths.PluginPath, "AtomicFramework/AtomicFramework.dll"));
                MethodReference hookRef = plugin.MainModule.Types.First(t => t.Name == "EarlyHook")
                    .Methods.First(m => m.Name == "HookBepInEx");

                MethodReference ourImport = assembly.MainModule.ImportReference(hookRef);

                Log.LogDebug("Fetching Chainloader");
                TypeDefinition chainloader = assembly.MainModule.Types
                    .Where(t => t.IsClass && t.FullName == "BepInEx.Bootstrap.Chainloader").First();

                Log.LogDebug("Patching Chainloader");
                MethodDefinition init = chainloader.Methods.Where(m => m.Name == "Initialize").First();
                Instruction first = init.Body.Instructions[0];
                ILProcessor proc = init.Body.GetILProcessor();

                proc.InsertBefore(first, proc.Create(OpCodes.Call, ourImport));
            }
            catch (Exception e)
            {
                Log.LogError($"Uncaught exception in Patcher.PatchChainloader: {e}");

                if (CONSOLE_ENABLED)
                    Process.GetCurrentProcess().WaitForExit();
            }
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
                AssemblyDefinition plugin = AssemblyDefinition.ReadAssembly(Path.Combine(Paths.PluginPath, "AtomicFramework/AtomicFramework.dll"));
                MethodReference hookRef = plugin.MainModule.Types.First(t => t.Name == "EarlyHook")
                    .Methods.First(m => m.Name == "HookBepInEx");

                MethodReference ourImport = assembly.MainModule.ImportReference(hookRef);

                Log.LogDebug("Fetching UnityChainloader");
                TypeDefinition chainloader = assembly.MainModule.Types
                    .Where(t => t.IsClass && t.FullName == "BepInEx.Unity.Mono.Bootstrap.UnityChainloader").First();

                Log.LogDebug("Patching UnityChainloader");
                MethodDefinition init = chainloader.Methods.Where(m => m.Name == "Initialize").First();
                Instruction first = init.Body.Instructions[0];
                ILProcessor proc = init.Body.GetILProcessor();

                proc.InsertBefore(first, proc.Create(OpCodes.Call, ourImport));
            }
            catch (Exception e)
            {
                Log.LogError($"Uncaught exception in Patcher.PatchChainloader: {e}");

                if (CONSOLE_ENABLED)
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

        public static bool CONSOLE_ENABLED
        {
            get
            {
#if BEP5
                Type consoleManager = typeof(BepInPlugin).Assembly.GetType("BepInEx.ConsoleManager");
                return (bool)consoleManager.GetProperty("ConsoleActive").GetValue(null);
#else
                return ConsoleManager.ConsoleActive;
#endif
            }
        }


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
                string? arguments = null;

                switch (Environment.OSVersion.Platform)
                {
                    case PlatformID.Win32NT:
                        filename = "AtomicModManager.exe";
                        break;
                    case PlatformID.Unix:
                        string? wine = GetWine();
                        if (wine != null)
                        {
                            filename = wine;
                            arguments = "AtomicModManager.exe";
                        }
                        else
                        {
                            Log.LogWarning("Couldn't find Wine, skipping mod manager.");
                            return;
                        }

                        Log.LogWarning("Attempted to run Wine, this feature is untested.");

                        break;
                    default:
                        Log.LogWarning("Skipping mod manager, unsupported OS");
                        return;
                }

                string fullpath = Path.GetFullPath(filename);
                Process? existing = Process.GetProcesses().FirstOrDefault(p => p.MainModule?.FileName == fullpath);
                if (existing != null)
                {
                    using ModManager manager = new(existing.StandardOutput, existing.StandardInput);
                    manager.Prompt();

                    DisablePluginsPacket plugins = (DisablePluginsPacket)manager.ReadPackets()!;
                    NATIVE_DISABLED = plugins.native;
                    ATOMIC_DISABLED = plugins.atomic;

                    try
                    {
                        existing.Kill();
                    }
                    catch { }
                }
                else
                {
                    ProcessStartInfo si = new(Path.Combine(Paths.PatcherPluginPath, "AtomicFramework", filename))
                    {
                        Environment =
                        {
                            { "BEP_PATH", Paths.BepInExRootPath },
                            { "MANAGE_PATH", Paths.ManagedPath }
                        },

                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true
                    };

                    if (arguments != null)
                        si.Arguments = arguments;

                    using Process newProc = Process.Start(si);
                    using (ModManager manager = new(newProc.StandardOutput, newProc.StandardInput))
                    {
                        using ManualLogSource subLog = BepInEx.Logging.Logger.CreateLogSource("AtomicModManager");
                        bool looping = true;
                        while (looping)
                        {
                            Packet? read = manager.ReadPackets();

                            switch (read)
                            {
                                case LogPacket log:
                                    subLog.Log((LogLevel)log.level, log.message);
                                    break;
                                case DisablePluginsPacket plugins:
                                    NATIVE_DISABLED = plugins.native;
                                    ATOMIC_DISABLED = plugins.atomic;
                                    looping = false;
                                    break;
                                case null when newProc.HasExited:
                                    looping = false;
                                    break;
                                default:
                                    Log.LogError("Unknown packet type from ModManager");
                                    break;
                            }
                        }
                    }

                    try
                    {
                        newProc.Kill();
                    }
                    catch { }
                }

                if (NATIVE_DISABLED.Length > 0)
                    Log.LogDebug($"Blocking {String.Join(", ", NATIVE_DISABLED)}");

                if (ATOMIC_DISABLED.Length > 0)
                    Log.LogDebug($"Disabling {String.Join(", ", ATOMIC_DISABLED)}");

                if (NATIVE_DISABLED.Contains("AtomicFramework"))
                    Log.LogWarning("AtomicFramework must be loaded to function properly. It will not be awoken.");
            }
            catch (Exception e)
            {
                Log.LogError($"Uncaught exception in Patcher.Impl: {e}");

                if (CONSOLE_ENABLED)
                    Process.GetCurrentProcess().WaitForExit();
            }
        }

        private static string? GetWine()
        {
            string[]? targets = Environment.GetEnvironmentVariable("PATH")?.Split(';');
            if (targets == null)
                return null;

            foreach (string test in targets)
            {
                string path = test.Trim();
                if (!String.IsNullOrEmpty(path) && File.Exists(Path.Combine(path, "wine")))
                    return Path.GetFullPath(path);
            }

            return null;
        }
    }
}
