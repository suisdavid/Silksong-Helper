using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SilksongHelper;

/// <summary>自创招式共用的战斗助手：反射构造 HitInstance 打伤害、读取英雄状态。</summary>
public static class GaleCombat
{
    /// <summary>对目标造成一次伤害。dir 为击退方向角度（度），circle=true 时为径向击退。</summary>
    public static void ApplyHit(HealthManager hm, GameObject source, int dmg, float dirDeg, bool circle = false, float magnitude = 1f)
    {
        try
        {
            var hitType = AccessTools.TypeByName("HitInstance");
            if (hitType == null) return;
            var hit = Activator.CreateInstance(hitType);
            SetF(hit, "AttackType", Enum.Parse(AccessTools.TypeByName("AttackTypes")!, "Nail"));
            SetF(hit, "DamageDealt", dmg);
            SetF(hit, "Multiplier", 1f);
            SetF(hit, "MagnitudeMultiplier", magnitude);
            SetF(hit, "Source", source);
            SetF(hit, "IsFirstHit", true);
            SetF(hit, "Direction", dirDeg);
            if (circle) SetF(hit, "CircleDirection", true);
            AccessTools.Method(hm.GetType(), "Hit", new[] { hitType })?.Invoke(hm, new[] { hit });
        }
        catch (Exception e) { Plugin.Log.LogWarning($"gale hit: {e.Message}"); }
    }

    private static void SetF(object obj, string name, object value)
    {
        var fi = AccessTools.Field(obj.GetType(), name);
        if (fi == null) return;
        try
        {
            var v = fi.FieldType.IsEnum ? Enum.ToObject(fi.FieldType, value) : Convert.ChangeType(value, fi.FieldType);
            fi.SetValue(obj, v);
        }
        catch { }
    }

    public static int NailDamage(HeroController hero, float mult = 1f)
    {
        try
        {
            var pd = AccessTools.Property(typeof(HeroController), "playerData")?.GetValue(hero)
                     ?? AccessTools.Field(typeof(HeroController), "playerData")?.GetValue(hero);
            if (pd != null)
            {
                var v = AccessTools.Property(pd.GetType(), "nailDamage")?.GetValue(pd);
                if (v is int n && n > 0) return Mathf.Max(1, Mathf.RoundToInt(n * mult));
            }
        }
        catch { }
        return Mathf.Max(1, Mathf.RoundToInt(5 * mult));
    }

    public static bool CStateBool(HeroController hero, string name)
    {
        try
        {
            var cs = AccessTools.Field(typeof(HeroController), "cState")?.GetValue(hero);
            return cs != null && (bool)(AccessTools.Field(cs.GetType(), name)?.GetValue(cs) ?? false);
        }
        catch { return false; }
    }

    /// <summary>范围内的敌人（去重）。</summary>
    public static List<HealthManager> EnemiesInCircle(Vector2 center, float radius)
    {
        var list = new List<HealthManager>();
        var seen = new HashSet<int>();
        foreach (var col in Physics2D.OverlapCircleAll(center, radius))
        {
            HealthManager? hm = null;
            try { hm = col.GetComponentInParent<HealthManager>(); } catch { }
            if (hm == null) continue;
            if (seen.Add(hm.GetInstanceID())) list.Add(hm);
        }
        return list;
    }

    public static List<HealthManager> EnemiesInBox(Vector2 center, Vector2 size)
    {
        var list = new List<HealthManager>();
        var seen = new HashSet<int>();
        foreach (var col in Physics2D.OverlapBoxAll(center, size, 0f))
        {
            HealthManager? hm = null;
            try { hm = col.GetComponentInParent<HealthManager>(); } catch { }
            if (hm == null) continue;
            if (seen.Add(hm.GetInstanceID())) list.Add(hm);
        }
        return list;
    }

    public static float AngleTo(Vector2 from, Vector2 to)
        => Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
}
