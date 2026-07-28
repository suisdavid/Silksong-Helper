using System;
using GlobalEnums;
using HarmonyLib;
using UnityEngine;

namespace SilksongHelper;

/// <summary>
/// 临时诊断补丁：定位"装备疾风纹章后无法攻击"问题。验证完成后可整类删除。
/// </summary>
internal static class DebugDiagPatches
{
    [HarmonyPatch(typeof(HeroController), "UpdateConfig")]
    internal static class UpdateConfigDiag
    {
        internal static void Postfix(HeroController __instance)
        {
            try
            {
                var group = __instance.CurrentConfigGroup;
                object? cfg = group != null ? AccessTools.Field(group.GetType(), "Config")?.GetValue(group) : null;
                Plugin.Log.LogInfo($"[DIAG] UpdateConfig: crest={PlayerDataInstance()} group={(group == null ? "NULL" : "ok")} config={(cfg == null ? "NULL" : ((UnityEngine.Object)cfg).name)}");
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[DIAG] UpdateConfig: {e.Message}"); }
        }

        private static string PlayerDataInstance()
        {
            try
            {
                var t = AccessTools.TypeByName("PlayerData");
                var p = t?.GetProperty("instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                var inst = p?.GetValue(null);
                return AccessTools.Field(t!, "CurrentCrestID")?.GetValue(inst) as string ?? "?";
            }
            catch { return "?"; }
        }
    }

    [HarmonyPatch(typeof(HeroController), "Attack", typeof(AttackDirection))]
    internal static class AttackDiag
    {
        internal static void Prefix(HeroController __instance, AttackDirection attackDir)
        {
            try
            {
                var group = __instance.CurrentConfigGroup;
                object? cfg = group != null ? AccessTools.Field(group.GetType(), "Config")?.GetValue(group) : null;
                var cooldown = AccessTools.Field(typeof(HeroController), "attack_cooldown")?.GetValue(__instance);
                Plugin.Log.LogInfo($"[DIAG] Attack({attackDir}): group={(group == null ? "NULL" : "ok")} config={(cfg == null ? "NULL" : ((UnityEngine.Object)cfg).name)} cooldown={cooldown}");
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[DIAG] Attack: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(NailSlash), "StartSlash")]
    internal static class StartSlashDiag
    {
        internal static void Postfix(NailSlash __instance)
        {
            try { Plugin.Log.LogInfo($"[DIAG] NailSlash.StartSlash on {__instance.gameObject.name} active={__instance.gameObject.activeInHierarchy}"); }
            catch (Exception e) { Plugin.Log.LogWarning($"[DIAG] StartSlash: {e.Message}"); }
        }
    }
}
