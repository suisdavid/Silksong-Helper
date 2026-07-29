using System;
using HarmonyLib;
using UnityEngine;

namespace SilksongHelper;

/// <summary>疾风纹章特效挂接：上劈/下劈/下刺/冲刺/缚丝 触发对应特效（仅装备疾风纹章时）。</summary>
internal static class GaleFxPatches
{
    private static bool Active => DesignedCrests.AppliedId == "Gale";

    private static HeroController? HeroOf(NailSlash s)
        => AccessTools.Field(typeof(NailSlash), "hc")?.GetValue(s) as HeroController;

    /// <summary>上劈 / 下劈（NailSlash 路径）。</summary>
    [HarmonyPatch(typeof(NailSlash), nameof(NailSlash.StartSlash))]
    internal static class NailSlashFx
    {
        internal static void Postfix(NailSlash __instance)
        {
            try
            {
                if (!Active) return;
                var hc = HeroOf(__instance);
                if (hc == null) return;
                var up = AccessTools.Field(typeof(HeroController), "upSlash")?.GetValue(hc);
                var altUp = AccessTools.Field(typeof(HeroController), "altUpSlash")?.GetValue(hc);
                var down = AccessTools.Field(typeof(HeroController), "downSlash")?.GetValue(hc);
                var altDown = AccessTools.Field(typeof(HeroController), "altDownSlash")?.GetValue(hc);
                if (ReferenceEquals(__instance, up) || ReferenceEquals(__instance, altUp))
                    GaleFx.PlayUpSlash(hc);
                else if (ReferenceEquals(__instance, down) || ReferenceEquals(__instance, altDown))
                    GaleFx.PlayDownSlash(hc);
            }
            catch (Exception e) { Plugin.Log.LogWarning($"galefx slash: {e.Message}"); }
        }
    }

    /// <summary>下刺（Downspike 路径，纹章 downSlashType=DownSpike 时）。</summary>
    [HarmonyPatch(typeof(Downspike), nameof(Downspike.StartSlash))]
    internal static class DownspikeFx
    {
        internal static void Postfix(Downspike __instance)
        {
            try
            {
                if (!Active) return;
                var hc = AccessTools.Field(typeof(Downspike), "hc")?.GetValue(__instance) as HeroController;
                if (hc != null) GaleFx.PlayDownSlash(hc);
            }
            catch (Exception e) { Plugin.Log.LogWarning($"galefx downspike: {e.Message}"); }
        }
    }

    /// <summary>冲刺攻击（DashStab 激活路径；仅在实际冲刺中触发，避免换纹章误触发）。</summary>
    [HarmonyPatch(typeof(NailSlashTravel), "OnEnable")]
    internal static class DashStabFx
    {
        private static float _last;

        internal static void Postfix(NailSlashTravel __instance)
        {
            try
            {
                if (!Active) return;
                var hc = AccessTools.Field(typeof(NailSlashTravel), "hc")?.GetValue(__instance) as HeroController;
                if (hc == null) return;
                var cs = AccessTools.Field(typeof(HeroController), "cState")?.GetValue(hc);
                bool dashing = cs != null && (bool)(AccessTools.Field(cs.GetType(), "dashing")?.GetValue(cs) ?? false);
                if (!dashing) return;
                if (Time.time - _last < 0.3f) return; // 防抖
                _last = Time.time;
                GaleFx.PlayDashStab(hc);
            }
            catch (Exception e) { Plugin.Log.LogWarning($"galefx dash: {e.Message}"); }
        }
    }

    /// <summary>缚丝开始沿检测 → 丝愈光环。</summary>
    [HarmonyPatch(typeof(HeroController), "Update")]
    internal static class BindWatcher
    {
        private static bool _wasBinding;

        internal static void Postfix(HeroController __instance)
        {
            try
            {
                if (!Active) { _wasBinding = false; return; }
                var cs = AccessTools.Field(typeof(HeroController), "cState")?.GetValue(__instance);
                bool binding = cs != null && (bool)(AccessTools.Field(cs.GetType(), "isBinding")?.GetValue(cs) ?? false);
                if (binding && !_wasBinding)
                    GaleFx.PlayBind(__instance);
                _wasBinding = binding;
            }
            catch { }
        }
    }
}
