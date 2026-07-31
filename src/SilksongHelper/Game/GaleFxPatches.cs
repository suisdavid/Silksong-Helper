using System;
using HarmonyLib;
using UnityEngine;

namespace SilksongHelper;

/// <summary>自设计纹章特效挂接：缚丝光环、亵渎者冲刺闪现。</summary>
internal static class GaleFxPatches
{
    /// <summary>缚丝开始沿检测 → 丝愈光环；冲刺开始沿检测 → 亵渎者虚影闪现。</summary>
    [HarmonyPatch(typeof(HeroController), "Update")]
    internal static class BindWatcher
    {
        private static bool _wasBinding, _wasDashing;

        internal static void Postfix(HeroController __instance)
        {
            try
            {
                var id = DesignedCrests.AppliedId;
                if (id == null) { _wasBinding = _wasDashing = false; return; }

                bool binding = GaleCombat.CStateBool(__instance, "isBinding");
                if (binding && !_wasBinding)
                {
                    if (id == "Gale") GaleFx.PlayBind(__instance);
                    else if (id == "Blasphemer") GaleFx.PlayBind(__instance, BlasphemerTheme.Flame);
                }
                _wasBinding = binding;

                bool dashing = GaleCombat.CStateBool(__instance, "dashing");
                if (dashing && !_wasDashing && id == "Blasphemer")
                    PhantomBlink.Do(__instance);
                _wasDashing = dashing;
            }
            catch { }
        }
    }
}
