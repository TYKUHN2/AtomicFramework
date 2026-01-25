using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Unity.Mono;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace AtomicFramework
{
    internal class EarlyHook
    {
        internal void HookBepInEx()
        {
            Harmony h = new("xyz.tyknet.NuclearOption.Early");

            h.Patch(typeof(BaseChainloader<BaseUnityPlugin>).GetMethod("ToPluginInfo"),
                null,
                ToHarmony(MutateList)
                );

            h.Patch(typeof(BaseUnityPlugin).GetMethod("LoadPlugin"),
                null,
                ToHarmony(PostAdd)
                );
        }

        private static bool ShouldLoad(PluginInfo plugin)
        {
            return !Patcher.NATIVE_DISABLED.Contains(plugin.Metadata.GUID);
        }

        private static void PostAdd(PluginInfo plugin)
        {
            if (Patcher.ATOMIC_DISABLED.Contains(plugin.Metadata.GUID))
                (plugin.Instance as Behaviour)!.enabled = false;
        }

        private static Dictionary<string, PluginInfo> MutateList(Dictionary<string, PluginInfo> __result)
        {
            return __result.Where(p => ShouldLoad(p.Value)).ToDictionary(p => p.Key, p => p.Value);
        }

        private static HarmonyMethod ToHarmony(Delegate method)
        {
            return new HarmonyMethod(method.GetMethodInfo());
        }
    }
}
