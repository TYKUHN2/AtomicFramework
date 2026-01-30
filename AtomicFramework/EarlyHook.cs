using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using BepInEx.Unity.Mono.Bootstrap;
using HarmonyLib;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace AtomicFramework
{
    internal static class EarlyHook
    {
        internal static void HookBepInEx()
        {
            ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("AtomicFramework.EarlyHook");

            Harmony h = new("xyz.tyknet.NuclearOption.Early");

            try
            {
                Log.LogDebug("EARLYHOOK: HOOKING TOPLUGININFO");
                h.Patch(typeof(BaseChainloader<BaseUnityPlugin>)
                    .GetMethod("ToPluginInfo"),
                    null,
                    ToHarmony(MutateList)
                    );

                Log.LogDebug("EARLYHOOK: HOOKING LOADPLUGIN");
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

        private static bool ShouldLoad(PluginInfo plugin)
        {
            return !Patcher.NATIVE_DISABLED.Contains(plugin.Metadata.GUID);
        }

        private static void PostAdd(PluginInfo pluginInfo)
        {
            if (Patcher.ATOMIC_DISABLED.Contains(pluginInfo.Metadata.GUID))
                (pluginInfo.Instance as Behaviour)!.enabled = false;
        }

        private static PluginInfo? MutateList(PluginInfo __result)
        {
            return __result != null && ShouldLoad(__result) ? __result : null;
        }

        private static HarmonyMethod ToHarmony(Delegate method)
        {
            return new HarmonyMethod(method.GetMethodInfo());
        }
    }
}
