using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace SilksongHelper;

/// <summary>
/// 内置自设计纹章：以原版纹章为蓝本。
///
/// 关键机制说明：HeroController.UpdateConfig() 按 HeroConfig 的「引用相等」
/// 匹配配置组（攻击预制体/动画都在配置组里），克隆新配置会匹配失败导致
/// 完全没有攻击动作。因此自设计纹章的 heroConfig 直接共享蓝本纹章的配置
/// 资产（攻击动作/动画与蓝本一致），自定义数值（Mults/Overrides）在装备时
/// 通过 ApplyRuntime 写入、卸下时通过 RestoreRuntime 还原（记录原值）。
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
        /// <summary>斩击预制体移植：(来源纹章id, 要复制的配置组字段)——真正换用别的纹章的攻击动画。</summary>
        public (string srcCrestId, string[] fields)[] GroupSwaps = Array.Empty<(string, string[])>();
        /// <summary>斩击特效的缩放倍率（同时放大攻击判定范围），1 表示不变。</summary>
        public float SlashScale = 1f;
        /// <summary>斩击特效的染色（null 表示不染）。</summary>
        public Color? SlashTint;
    }

    private static readonly string[] _normalSlashFields =
    {
        "<NormalSlash>k__BackingField", "<NormalSlashDamager>k__BackingField", "NormalSlashObject",
        "<AlternateSlash>k__BackingField", "<AlternateSlashDamager>k__BackingField", "AlternateSlashObject",
    };
    private static readonly string[] _upSlashFields =
    {
        "<UpSlash>k__BackingField", "<UpSlashDamager>k__BackingField", "UpSlashObject",
        "<AltUpSlash>k__BackingField", "<AltUpSlashDamager>k__BackingField", "AltUpSlashObject",
    };
    private static readonly string[] _downSlashFields =
    {
        "<DownSlash>k__BackingField", "<DownSlashDamager>k__BackingField", "DownSlashObject",
        "<Downspike>k__BackingField",
        "<AltDownSlash>k__BackingField", "<AltDownSlashDamager>k__BackingField", "AltDownSlashObject",
        "<AltDownspike>k__BackingField",
    };

    private static readonly Def[] _defs =
    {
        new Def
        {
            Id = "Gale",
            Name = "疾风纹章",
            Description = "自设纹章：收割者的横扫普攻与上劈、野兽的强力下劈、漫游者的迅捷身法，全方位提速。",
            BaseCrestId = "Wanderer",
            Mults = new (string, float)[]
            {
                // 身法提速（作用于共享的漫游者数值配置）
                ("attackCooldownTime", 0.75f),
                ("quickAttackCooldownTime", 0.75f),
                ("attackRecoveryTime", 0.8f),
                ("chargeSlashLungeSpeed", 1.4f),
                ("dashStabSpeed", 1.3f),
                ("dashStabTime", 0.85f),
                ("downspikeSpeed", 1.2f),
                ("downspikeRecoveryTime", 0.8f),
            },
            Overrides = new (string, object)[]
            {
                ("canTurnWhileSlashing", true),
                ("canNailCharge", true),
                ("canBind", true),
            },
            SlashScale = 1.2f,
            SlashTint = new Color(0.55f, 0.95f, 1f), // 疾风青蓝
        },
    };

    static DesignedCrests()
    {
        // 真正移植其他纹章的攻击动画：收割者的普攻/上劈 + 野兽的下劈
        _defs[0].GroupSwaps = new (string, string[])[]
        {
            ("Reaper", _normalSlashFields.Concat(_upSlashFields).ToArray()),
            ("Warrior", _downSlashFields),
        };
    }

    private static readonly Dictionary<string, ToolCrest> _crests = new();
    private static readonly Dictionary<string, object> _configs = new();
    private static readonly HashSet<string> _builtDefs = new();
    private static readonly List<(object target, string field, object? value)> _originals = new();
    private static readonly List<(Transform tr, Vector3 scale)> _scaledObjects = new();
    private static readonly List<(Renderer rd, Color color)> _tintedRenderers = new();
    private static readonly List<GameObject> _activatedRoots = new();
    private static string? _appliedId;

    public static string? AppliedId => _appliedId;

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
                // 注意：heroConfig 保持指向蓝本的共享配置资产，确保
                // UpdateConfig 能引用匹配到蓝本的配置组（攻击动作/动画）。

                var cfgField = AccessTools.Field(typeof(ToolCrest), "heroConfig");
                if (cfgField?.GetValue(baseCrest) is UnityEngine.Object baseCfg && baseCfg != null)
                    _configs[def.Id] = baseCfg;

                _crests[def.Id] = crest;
                _builtDefs.Add(def.Id);
                Plugin.Log.LogInfo($"designed crest '{def.Name}' built (base: {def.BaseCrestId}).");
            }
            catch (Exception e) { Plugin.Log.LogWarning($"build designed crest '{def.Name}': {e}"); }
        }
    }

    /// <summary>装备自设计纹章时调用：把自定义数值写入当前激活配置（记录原值）。</summary>
    public static void ApplyRuntime(string id, object hero)
    {
        if (_appliedId == id) return;
        RestoreRuntime();
        var def = _defs.FirstOrDefault(d => d.Id == id);
        if (def == null) return;

        var active = AccessTools.Property(hero.GetType(), "CurrentConfigGroup")?.GetValue(hero);
        var cfg = active != null ? GetMember(active, "Config") : null;
        if (cfg is not UnityEngine.Object cfgObj || cfgObj == null || cfg == null)
        {
            Plugin.Log.LogWarning($"designed crest '{def.Name}': no active config to modify.");
            return;
        }

        int n = 0;
        var t = cfg.GetType();
        foreach (var (fn, mult) in def.Mults)
        {
            var fi = AccessTools.Field(t, fn);
            if (fi == null || fi.FieldType != typeof(float)) continue;
            try
            {
                RecordOriginal(cfg, fn, fi);
                fi.SetValue(cfg, (float)fi.GetValue(cfg) * mult);
                n++;
            }
            catch (Exception e) { Plugin.Log.LogWarning($"mult {fn}: {e.Message}"); }
        }
        foreach (var (fn, value) in def.Overrides)
        {
            var fi = AccessTools.Field(t, fn);
            if (fi == null) continue;
            try
            {
                RecordOriginal(cfg, fn, fi);
                fi.SetValue(cfg, Convert.ChangeType(value, fi.FieldType));
                n++;
            }
            catch (Exception e) { Plugin.Log.LogWarning($"override {fn}: {e.Message}"); }
        }

        _appliedId = id;
        ApplyGroupSwaps(hero, active!, def);
        ApplySlashVisuals(active!, def);
        Plugin.Log.LogInfo($"designed crest '{def.Name}' applied ({n} fields).");
    }

    /// <summary>把其他纹章的斩击预制体（NailSlash/Damager/Object 引用）移植到当前配置组——真正换用不同的攻击动画。</summary>
    private static void ApplyGroupSwaps(object hero, object activeGroup, Def def)
    {
        if (def.GroupSwaps.Length == 0) return;
        var groups = new List<object>();
        foreach (var fname in new[] { "configs", "specialConfigs" })
            if (AccessTools.Field(hero.GetType(), fname)?.GetValue(hero) is Array arr)
                foreach (var g in arr) groups.Add(g);

        foreach (var (srcCrestId, fields) in def.GroupSwaps)
        {
            try
            {
                var mi = AccessTools.Method(typeof(ToolItemManager), nameof(ToolItemManager.GetCrestByName));
                if (mi?.Invoke(null, new object?[] { srcCrestId }) is not ToolCrest srcCrest) continue;
                var srcCfg = GetMember(srcCrest, "HeroConfig");
                var srcGroup = groups.FirstOrDefault(g => ReferenceEquals(GetMember(g, "Config"), srcCfg));
                if (srcGroup == null)
                {
                    Plugin.Log.LogWarning($"designed crest: source group '{srcCrestId}' not found.");
                    continue;
                }
                int n = 0;
                foreach (var fn in fields)
                {
                    var fi = AccessTools.Field(activeGroup.GetType(), fn);
                    var fiSrc = AccessTools.Field(srcGroup.GetType(), fn);
                    if (fi == null || fiSrc == null) continue;
                    RecordOriginal(activeGroup, fn, fi);
                    fi.SetValue(activeGroup, fiSrc.GetValue(srcGroup));
                    n++;
                }
                // 激活来源纹章的根对象（斩击预制体挂在它下面）
                if (AccessTools.Field(srcGroup.GetType(), "ActiveRoot")?.GetValue(srcGroup) is GameObject root && root != null)
                {
                    if (!_activatedRoots.Contains(root))
                    {
                        root.SetActive(true);
                        _activatedRoots.Add(root);
                    }
                }
                Plugin.Log.LogInfo($"designed crest: borrowed {n} attack objects from '{srcCrestId}'.");
            }
            catch (Exception e) { Plugin.Log.LogWarning($"group swap '{srcCrestId}': {e.Message}"); }
        }
    }

    private static readonly string[] _slashObjectFields =
    {
        "NormalSlashObject", "AlternateSlashObject", "UpSlashObject", "AltUpSlashObject",
    };

    /// <summary>改造斩击特效：缩放（影响判定范围）+ 染色，使自设计纹章视觉不同于蓝本。</summary>
    private static void ApplySlashVisuals(object configGroup, Def def)
    {
        foreach (var fn in _slashObjectFields)
        {
            try
            {
                if (AccessTools.Field(configGroup.GetType(), fn)?.GetValue(configGroup) is not GameObject go || go == null)
                    continue;
                if (def.SlashScale != 1f)
                {
                    _scaledObjects.Add((go.transform, go.transform.localScale));
                    go.transform.localScale *= def.SlashScale;
                }
                if (def.SlashTint is Color tint)
                {
                    var rd = go.GetComponentInChildren<MeshRenderer>();
                    if (rd != null)
                    {
                        _tintedRenderers.Add((rd, rd.material.color));
                        rd.material.color = tint;
                    }
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning($"slash visuals {fn}: {e.Message}"); }
        }
    }

    /// <summary>卸下自设计纹章（或插件卸载）时调用：还原所有被修改的配置字段、移植引用与特效。</summary>
    public static void RestoreRuntime(object? hero = null)
    {
        foreach (var (target, field, value) in _originals)
        {
            try
            {
                var fi = AccessTools.Field(target.GetType(), field);
                fi?.SetValue(target, value);
            }
            catch (Exception e) { Plugin.Log.LogWarning($"restore {field}: {e.Message}"); }
        }
        _originals.Clear();
        foreach (var (tr, scale) in _scaledObjects)
        {
            try { if (tr != null) tr.localScale = scale; } catch { }
        }
        _scaledObjects.Clear();
        foreach (var (rd, color) in _tintedRenderers)
        {
            try { if (rd != null) rd.material.color = color; } catch { }
        }
        _tintedRenderers.Clear();
        // 禁用借来的根对象，但保留当前激活配置组的根对象（避免误关导致无法攻击）
        GameObject? keepActive = null;
        if (hero != null)
        {
            try
            {
                var active = AccessTools.Property(hero.GetType(), "CurrentConfigGroup")?.GetValue(hero);
                if (active != null)
                    keepActive = AccessTools.Field(active.GetType(), "ActiveRoot")?.GetValue(active) as GameObject;
            }
            catch { }
        }
        foreach (var go in _activatedRoots)
        {
            try { if (go != null && go != keepActive) go.SetActive(false); } catch { }
        }
        _activatedRoots.Clear();
        _appliedId = null;
    }

    private static void RecordOriginal(object target, string field, System.Reflection.FieldInfo fi)
    {
        if (!_originals.Exists(o => ReferenceEquals(o.target, target) && o.field == field))
            _originals.Add((target, field, fi.GetValue(target)));
    }

    private static object? GetMember(object obj, string name)
    {
        var t = obj.GetType();
        var p = AccessTools.Property(t, name);
        if (p != null && p.CanRead) return p.GetValue(obj, null);
        var f = AccessTools.Field(t, name);
        return f?.GetValue(obj);
    }
}
