using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SilksongHelper;

/// <summary>
/// 自创招式「旋风丝刃」：不属于任何纹章的全新攻击动作。
/// 装备疾风纹章后进行水平攻击时，大黄蜂的织针化作两片丝刃环绕全身
/// 高速旋转两周，对四周所有敌人造成伤害（动画为程序化绘制，伤害逻辑自实现）。
/// </summary>
public sealed class CycloneSlash : MonoBehaviour
{
    private const float Life = 0.5f;      // 持续时长
    private const float Fps = 32f;        // 帧率（16帧转2周）
    private const float Tick = 0.12f;     // 伤害判定间隔
    private const float Radius = 1.3f;    // 伤害半径（与动画刃圈范围一致，不外溢）
    private const float HitCooldown = 0.24f; // 同一敌人受击间隔
    private const int MaxAlive = 3;

    private static Sprite[]? _frames;
    private static readonly Dictionary<int, float> _hitCooldowns = new();
    private static int _alive;

    private HeroController _hero = null!;
    private SpriteRenderer _rd = null!;
    private float _t;
    private float _nextTick;

    public static void Spawn(HeroController hero)
    {
        if (_alive >= MaxAlive) return;
        EnsureFrames();
        var go = new GameObject("SilkCyclone");
        var c = go.AddComponent<CycloneSlash>();
        c._hero = hero;
        c._rd = go.AddComponent<SpriteRenderer>();
        c._rd.sortingOrder = 100;
        go.transform.position = Center(hero);
        go.transform.localScale = Vector3.one * 1.2f; // 动画刃圈 ≈ 1.2 米，与判定半径对齐
        GaleFx.PlayCycloneExtras(hero); // 精细补充层：冲击环+火花+逆向光尘
        _alive++;
    }

    private static Vector3 Center(HeroController h) => h.transform.position + new Vector3(0f, 0.9f, 0f);

    private static void EnsureFrames()
    {
        if (_frames != null) return;
        var texs = ProceduralTextures.BuildCyclone(16, 128);
        _frames = new Sprite[texs.Length];
        for (int i = 0; i < texs.Length; i++)
            _frames[i] = Sprite.Create(texs[i], new Rect(0, 0, texs[i].width, texs[i].height),
                new Vector2(0.5f, 0.5f), 64f);
    }

    private void Update()
    {
        if (_hero == null) { DestroySelf(); return; }
        _t += Time.deltaTime;
        transform.position = Center(_hero);
        _rd.sprite = _frames![Mathf.FloorToInt(_t * Fps) % _frames.Length];
        var col = _rd.color;
        col.a = _t > Life - 0.15f ? Mathf.Max(0f, (Life - _t) / 0.15f) : 1f;
        _rd.color = col;
        if (_t >= _nextTick)
        {
            _nextTick += Tick;
            DamageTick();
        }
        if (_t >= Life) DestroySelf();
    }

    private void DestroySelf()
    {
        _alive--;
        Destroy(gameObject);
    }

    private void DamageTick()
    {
        Vector2 center = transform.position;
        int dmg = Mathf.Max(1, GetNailDamage() * 3 / 5);
        var seen = new HashSet<int>();
        foreach (var col in Physics2D.OverlapCircleAll(center, Radius))
        {
            HealthManager? hm = null;
            try { hm = col.GetComponentInParent<HealthManager>(); } catch { }
            if (hm == null) continue;
            int id = hm.GetInstanceID();
            if (!seen.Add(id)) continue;
            if (_hitCooldowns.TryGetValue(id, out var last) && Time.time - last < HitCooldown) continue;
            _hitCooldowns[id] = Time.time;
            ApplyHit(hm, center, dmg);
        }
    }

    private void ApplyHit(HealthManager hm, Vector2 center, int dmg)
    {
        try
        {
            var hitType = AccessTools.TypeByName("HitInstance");
            if (hitType == null) return;
            var hit = Activator.CreateInstance(hitType);
            SetF(hit, "AttackType", Enum.Parse(AccessTools.TypeByName("AttackTypes")!, "Nail"));
            SetF(hit, "DamageDealt", dmg);
            SetF(hit, "Multiplier", 1f);
            SetF(hit, "MagnitudeMultiplier", 1f);
            SetF(hit, "Source", _hero.gameObject);
            SetF(hit, "IsFirstHit", true);
            // 径向击退：从旋风中心向外
            var ep = (Vector2)hm.transform.position;
            float dir = Mathf.Atan2(ep.y - center.y, ep.x - center.x) * Mathf.Rad2Deg;
            SetF(hit, "Direction", dir);
            SetF(hit, "CircleDirection", true);
            AccessTools.Method(hm.GetType(), "Hit", new[] { hitType })?.Invoke(hm, new[] { hit });
        }
        catch (Exception e) { Plugin.Log.LogWarning($"cyclone hit: {e.Message}"); }
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

    private int GetNailDamage()
    {
        try
        {
            var pd = AccessTools.Property(typeof(HeroController), "playerData")?.GetValue(_hero)
                     ?? AccessTools.Field(typeof(HeroController), "playerData")?.GetValue(_hero);
            if (pd != null)
            {
                var v = AccessTools.Property(pd.GetType(), "nailDamage")?.GetValue(pd);
                if (v is int n && n > 0) return n;
            }
        }
        catch { }
        return 5;
    }
}

/// <summary>装备自设计纹章时，用各自的自创招式替换各方向斩击。</summary>
internal static class CyclonePatches
{
    [HarmonyPatch(typeof(NailSlash), nameof(NailSlash.StartSlash))]
    internal static class StartSlashPrefix
    {
        internal static bool Prefix(NailSlash __instance)
        {
            try
            {
                var id = DesignedCrests.AppliedId;
                if (id == null) return true;
                var hc = AccessTools.Field(typeof(NailSlash), "hc")?.GetValue(__instance) as HeroController;
                if (hc == null) return true;
                object? F(string n) => AccessTools.Field(typeof(HeroController), n)?.GetValue(hc);
                bool Is(params string[] names)
                {
                    foreach (var n in names)
                        if (ReferenceEquals(__instance, F(n))) return true;
                    return false;
                }

                if (id == "Gale")
                {
                    if (Is("normalSlash", "alternateSlash")) { CycloneSlash.Spawn(hc); return false; }
                    if (Is("upSlash", "altUpSlash")) { SkyPillar.Start(hc); return false; }
                    if (Is("downSlash", "altDownSlash")) { MeteorDive.Start(hc); return false; }
                }
                else if (id == "Blasphemer")
                {
                    if (Is("normalSlash", "alternateSlash")) { SwordSwing.Start(hc, SwordSwing.Dir.Forward); return false; }
                    if (Is("upSlash", "altUpSlash")) { SwordSwing.Start(hc, SwordSwing.Dir.Up); return false; }
                    if (Is("downSlash", "altDownSlash")) { SwordSwing.Start(hc, SwordSwing.Dir.Down); return false; }
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning($"move prefix: {e.Message}"); }
            return true;
        }
    }

    /// <summary>下刺路径（downSlashType=DownSpike）。</summary>
    [HarmonyPatch(typeof(Downspike), nameof(Downspike.StartSlash))]
    internal static class DownspikePrefix
    {
        internal static bool Prefix(Downspike __instance)
        {
            try
            {
                var id = DesignedCrests.AppliedId;
                if (id == null) return true;
                var hc = AccessTools.Field(typeof(Downspike), "hc")?.GetValue(__instance) as HeroController;
                if (hc == null) return true;
                if (id == "Gale") { MeteorDive.Start(hc); return false; }
                if (id == "Blasphemer") { SwordSwing.Start(hc, SwordSwing.Dir.Down); return false; }
            }
            catch (Exception e) { Plugin.Log.LogWarning($"dive prefix: {e.Message}"); }
            return true;
        }
    }

    /// <summary>冲刺攻击路径（DashStab 激活）。仅实际冲刺中触发。</summary>
    [HarmonyPatch(typeof(NailSlashTravel), "OnEnable")]
    internal static class DashStabPrefix
    {
        private static float _last;

        internal static bool Prefix(NailSlashTravel __instance)
        {
            try
            {
                var id = DesignedCrests.AppliedId;
                if (id == null) return true;
                var hc = AccessTools.Field(typeof(NailSlashTravel), "hc")?.GetValue(__instance) as HeroController;
                if (hc == null) return true;
                if (!GaleCombat.CStateBool(hc, "dashing")) return true;
                if (Time.time - _last < 0.3f) return true; // 防抖
                _last = Time.time;
                if (id == "Gale") { PhantomLunge.Start(hc); return false; }
                if (id == "Blasphemer") { BloodRush.Start(hc); return false; }
            }
            catch (Exception e) { Plugin.Log.LogWarning($"rush prefix: {e.Message}"); }
            return true;
        }
    }
}
