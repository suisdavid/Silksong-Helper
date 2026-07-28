using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace SilksongHelper;

internal static class CrestInventoryPatches
{
    [HarmonyPatch(typeof(ToolItemManager), nameof(ToolItemManager.GetAllCrests))]
    internal static class GetAllCrestsPatch
    {
        internal static void Postfix(List<ToolCrest> __result)
        {
            try
            {
                if (__result == null) return;
                CustomCrestRegistry.EnsureBuilt();
                foreach (var synth in CustomCrestRegistry.All)
                    if (!__result.Contains(synth))
                        __result.Add(synth);
                DesignedCrests.EnsureBuilt();
                foreach (var d in DesignedCrests.All)
                    if (!__result.Contains(d))
                        __result.Add(d);
            }
            catch (Exception e) { Plugin.Log.LogWarning($"GetAllCrests postfix: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(ToolItemManager), nameof(ToolItemManager.GetCrestByName))]
    internal static class GetCrestByNamePatch
    {
        internal static void Postfix(ref ToolCrest? __result, object[] __args)
        {
            try
            {
                if (__result != null) return;
                if (__args == null || __args.Length == 0) return;
                var name = __args[0] as string;
                DesignedCrests.EnsureBuilt();
                __result ??= DesignedCrests.Get(name);
                __result ??= CustomCrestRegistry.Get(name);
            }
            catch (Exception e) { Plugin.Log.LogWarning($"GetCrestByName postfix: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(InventoryToolCrest), nameof(InventoryToolCrest.DisplayName), MethodType.Getter)]
    internal static class DisplayNamePatch
    {
        internal static void Postfix(ref string __result, InventoryToolCrest __instance)
        {
            try
            {
                if (__instance == null) return;
                var designed = DesignedCrests.DisplayNameFor(__instance.CrestData?.name);
                if (designed != null) { __result = designed; return; }
                var custom = CustomCrestRegistry.CustomNameFor(__instance.CrestData);
                if (custom != null) __result = custom;
            }
            catch (Exception e) { Plugin.Log.LogWarning($"DisplayName postfix: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(InventoryToolCrest), nameof(InventoryToolCrest.IsUnlocked), MethodType.Getter)]
    internal static class IsUnlockedPatch
    {
        internal static void Postfix(ref bool __result, InventoryToolCrest __instance)
        {
            try
            {
                var crestData = __instance?.CrestData;
                if (crestData == null) return;
                if (DesignedCrests.IsDesigned(crestData.name)) { __result = true; return; }
                if (CustomCrestRegistry.IsSentinel(crestData.name)) __result = true;
            }
            catch (Exception e) { Plugin.Log.LogWarning($"IsUnlocked postfix: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(HeroController), "ResetAllCrestState", typeof(bool))]
    internal static class ResetCrestStatePatch
    {
        internal static void Postfix(HeroController __instance)
        {
            try
            {
                var id = CurrentCrestId();
                // 先统一还原旧状态（还原时会保留当前激活配置组的根对象），再应用新纹章
                Plugin.Applier.RestoreOverrides(__instance);
                DesignedCrests.RestoreRuntime();

                var customId = CustomCrestRegistry.IdFromSentinel(id);
                if (customId != null)
                {
                    var charm = Plugin.SaveData.Charms.FirstOrDefault(c => c.Id == customId);
                    if (charm != null) Plugin.Applier.ApplyOverrides(charm, __instance);
                }
                else if (DesignedCrests.IsDesigned(id))
                {
                    DesignedCrests.ApplyRuntime(id!, __instance);
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning($"ResetAllCrestState postfix: {e.Message}"); }
        }

        private static string? CurrentCrestId()
        {
            var t = AccessTools.TypeByName("PlayerData");
            if (t == null) return null;
            object? inst = null;
            foreach (var n in new[] { "Instance", "instance", "current", "Current" })
            {
                try
                {
                    var p = AccessTools.Property(t, n);
                    if (p != null && p.GetGetMethod(nonPublic: true) != null)
                    {
                        var v = p.GetValue(null, null);
                        if (v is UnityEngine.Object u && u == null) continue;
                        if (v != null) { inst = v; break; }
                    }
                }
                catch { }
                try
                {
                    var f = AccessTools.Field(t, n);
                    if (f != null && f.IsStatic)
                    {
                        var v = f.GetValue(null);
                        if (v is UnityEngine.Object u2 && u2 == null) continue;
                        if (v != null) { inst = v; break; }
                    }
                }
                catch { }
            }
            if (inst == null)
            {
                try { inst = UnityEngine.Object.FindObjectOfType(t); } catch { }
            }
            if (inst == null) return null;
            try { return AccessTools.Field(t, "CurrentCrestID")?.GetValue(inst) as string; }
            catch { return null; }
        }
    }
}
