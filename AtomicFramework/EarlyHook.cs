using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

#if BEP6
using BepInEx.Unity.Mono;
using BepInEx.Unity.Mono.Bootstrap;
#endif

namespace AtomicFramework
{
    internal static partial class EarlyHook
    {
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("AtomicFramework.EarlyHook");

        private static bool ShouldLoad(PluginInfo plugin)
        {
            return !(Patcher.NATIVE_DISABLED.Contains(plugin.Metadata.GUID) || Patcher.ATOMIC_DISABLED.Contains(plugin.Metadata.GUID));
        }

        private static HarmonyMethod ToHarmony(Delegate method)
        {
            return new HarmonyMethod(method.GetMethodInfo());
        }
    }

#if BEP6
    internal static partial class EarlyHook
    {
        internal static void HookBepInEx()
        {
            Harmony h = new("xyz.tyknet.NuclearOption.Early");

            try
            {
                Log.LogDebug("HOOKING DISCOVERPLUGINS");
                h.Patch(typeof(BaseChainloader<BaseUnityPlugin>)
                    .GetMethod("DiscoverPlugins", BindingFlags.Instance | BindingFlags.NonPublic),
                    null,
                    ToHarmony(MutateList)
                    );

                Log.LogDebug("HOOKING LOADPLUGIN");
                h.Patch(typeof(UnityChainloader).GetMethod("LoadPlugin"),
                    null,
                    ToHarmony(PostAdd)
                    );

                Log.LogDebug("EarlyHook successful");
            }
            catch (HarmonyException e)
            {
                Log.LogError(e.InnerException);

                Process.GetCurrentProcess().WaitForExit();
            }
        }
        private static void PostAdd(PluginInfo pluginInfo, BaseUnityPlugin __result)
        {
            if (Patcher.ATOMIC_DISABLED.Contains(pluginInfo.Metadata.GUID))
                __result.enabled = false;
        }

        private static IList<PluginInfo> MutateList(IList<PluginInfo> __result)
        {
            return [.. __result.Where(ShouldLoad)];
        }
    }
#else
    internal static partial class EarlyHook
    {
        internal static void HookBepInEx()
        {
            Harmony h = new("xyz.tyknet.NuclearOption.Early");

            try
            {
                Log.LogDebug("HOOKING FINDPLUGINTYPES");
                h.Patch(typeof(TypeLoader)
                    .GetMethod("FindPluginTypes")
                    .MakeGenericMethod(typeof(PluginInfo)),
                    null,
                    ToHarmony(MutateList)
                    );

                /*
                Log.LogDebug("HOOKING LOADPLUGIN");
                h.Patch(typeof(UnityChainloader).GetMethod("LoadPlugin"),
                    null,
                    ToHarmony(PostAdd)
                    );
                */

                Log.LogDebug("EarlyHook successful");
            }
            catch (HarmonyException e)
            {
                Log.LogError(e.InnerException);

                Process.GetCurrentProcess().WaitForExit();
            }
        }

        private static Dictionary<string, List<PluginInfo>> MutateList(Dictionary<string, List<PluginInfo>> __result)
        {
            return __result.Select(kv => new KeyValuePair<string, List<PluginInfo>>(kv.Key, [.. kv.Value.Where(ShouldLoad)]))
                .Where(kv => kv.Value.Count() > 0)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }
    }
#endif
}
