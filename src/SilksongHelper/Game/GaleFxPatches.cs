using System;
using HarmonyLib;
using UnityEngine;

namespace SilksongHelper;

/// <summary>疾风纹章特效挂接：缚丝光环（招式设计已移至 GaleMoves/CyclonePatches）。</summary>
internal static class GaleFxPatches
{
    private static bool Active => DesignedCrests.AppliedId == "Gale";

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
                bool binding = GaleCombat.CStateBool(__instance, "isBinding");
                if (binding && !_wasBinding)
                    GaleFx.PlayBind(__instance);
                _wasBinding = binding;
            }
            catch { }
        }
    }
}
