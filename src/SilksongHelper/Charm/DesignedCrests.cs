using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace SilksongHelper;

/// <summary>
/// 内置自设计纹章：以原版纹章为蓝本克隆，再对 HeroControllerConfig
/// 施加自定义的数值修改（倍率/覆盖），形成全新的攻击动作手感。
/// 目前实现「疾风纹章」：极限攻速 + 多段冲刺 + 急速下刺。
/// </summary>
public static class DesignedCrests
{
    private sealed class Def
    {
        public string Id = "";
        public string Name = "";
        public string Description = "";
        public string BaseCrestId = "";
        public (string field, float mult)[] Mults = Array.Empty<(string, float)>();
        public (string field, object value)[] Overrides = Array.Empty<(string, object)>();
    }

    private static readonly Def[] _defs =
    {
        new Def
        {
            Id = "Gale",
            Name = "疾风纹章",
            Description = "自设纹章：以漫游者为蓝本，极限攻速、二段冲刺、急速下刺。",
            BaseCrestId = "Wanderer",
            Mults = new (string, float)[]
            {
                // 普通攻击：全面提速
                ("attackCooldownTime", 0.6f),
                ("quickAttackCooldownTime", 0.6f),
                ("attackDuration", 0.8f),
                ("attackRecoveryTime", 0.7f),
                // 蓄力攻击：突进更快
                ("chargeSlashLungeSpeed", 1.4f),
                // 冲刺攻击：更快更短
                ("dashStabSpeed", 1.3f),
                ("dashStabTime", 0.85f),
                // 下劈跳：急速下刺
                ("downspikeSpeed", 1.25f),
                ("downspikeAnticTime", 0.7f),
                ("downspikeRecoveryTime", 0.7f),
            },
            Overrides = new (string, object)[]
            {
                ("canTurnWhileSlashing", true),
                ("canNailCharge", true),
                ("canBind", true),
                ("dashStabSteps", 2),
                ("downspikeThrusts", true),
                ("downspikeBurstEffect", true),
            },
        },
    };

    private static readonly Dictionary<string, ToolCrest> _crests = new();
    private static readonly Dictionary<string, object> _configs = new();
    private static readonly HashSet<string> _builtDefs = new();

    public static bool IsDesigned(string? name) => name != null && _defs.Any(d => d.Id == name);

    public static string? DisplayNameFor(string? name)
        => _defs.FirstOrDefault(d => d.Id == name)?.Name;

    public static IEnumerable<ToolCrest> All => _crests.Values;

    public static ToolCrest? Get(string? name)
        => name != null && _crests.TryGetValue(name, out var c) ? c : null;

    public static object? ConfigFor(string? name)
        => name != null && _configs.TryGetValue(name, out var c) ? c : null;

    public static IEnumerable<CrestInfo> AllInfos()
    {
        foreach (var def in _defs)
        {
            if (!_crests.TryGetValue(def.Id, out var crest)) continue;
            _configs.TryGetValue(def.Id, out var cfg);
            int slots = 0;
            try { slots = (AccessTools.Property(typeof(ToolCrest), "Slots")?.GetValue(crest) as Array)?.Length ?? 0; }
            catch { }
            float hue = 0f;
            foreach (var ch in def.Id) hue = (hue + ch * 0.013f) % 1f;
            yield return new CrestInfo
            {
                Id = def.Id,
                Name = def.Name,
                Description = def.Description,
                SlotCount = slots,
                HeroConfig = cfg,
                Crest = crest,
                Preview = new SpriteAnimation(ProceduralTextures.Build(CharmPart.Slot, hue)),
            };
        }
    }

    public static void EnsureBuilt()
    {
        foreach (var def in _defs)
        {
            if (_builtDefs.Contains(def.Id)) continue;
            try
            {
                var mi = AccessTools.Method(typeof(ToolItemManager), nameof(ToolItemManager.GetCrestByName));
                if (mi?.Invoke(null, new object?[] { def.BaseCrestId }) is not ToolCrest baseCrest)
                    continue; // 游戏数据未加载，下次再试

                var crest = UnityEngine.Object.Instantiate(baseCrest);
                crest.name = def.Id;

                var cfgField = AccessTools.Field(typeof(ToolCrest), "heroConfig");
                if (cfgField?.GetValue(baseCrest) is UnityEngine.Object baseCfg && baseCfg != null)
                {
                    var cfgClone = UnityEngine.Object.Instantiate(baseCfg);
                    cfgClone.name = def.Id + "Config";
                    ApplyModifiers(cfgClone, def);
                    cfgField.SetValue(crest, cfgClone);
                    _configs[def.Id] = cfgClone;
                }

                _crests[def.Id] = crest;
                _builtDefs.Add(def.Id);
                Plugin.Log.LogInfo($"designed crest '{def.Name}' built (base: {def.BaseCrestId}).");
            }
            catch (Exception e) { Plugin.Log.LogWarning($"build designed crest '{def.Name}': {e}"); }
        }
    }

    private static void ApplyModifiers(UnityEngine.Object cfg, Def def)
    {
        var t = cfg.GetType();
        foreach (var (fn, mult) in def.Mults)
        {
            var fi = AccessTools.Field(t, fn);
            if (fi == null || fi.FieldType != typeof(float)) continue;
            try { fi.SetValue(cfg, (float)fi.GetValue(cfg) * mult); }
            catch (Exception e) { Plugin.Log.LogWarning($"mult {fn}: {e.Message}"); }
        }
        foreach (var (fn, value) in def.Overrides)
        {
            var fi = AccessTools.Field(t, fn);
            if (fi == null) continue;
            try { fi.SetValue(cfg, Convert.ChangeType(value, fi.FieldType)); }
            catch (Exception e) { Plugin.Log.LogWarning($"override {fn}: {e.Message}"); }
        }
    }
}
