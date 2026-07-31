using System.Collections.Generic;
using UnityEngine;

namespace SilksongHelper;

/// <summary>
/// 疾风纹章自创招式（行为全新，不属于任何纹章）：
/// 上劈「青霄柱」：小浮空 + 头顶丝刃柱多段攻击
/// 下劈「坠星震荡」：高速坠击，落地/命中时向两侧放出地面冲击波
/// 冲刺「残影连突」：穿透整条冲刺路径上的所有敌人，末端爆发击退
/// </summary>

/// <summary>上劈：青霄柱</summary>
public sealed class SkyPillar : MonoBehaviour
{
    private const float Duration = 0.45f, Tick = 0.1f;
    private static int _alive;

    private HeroController _hero = null!;
    private readonly HashSet<int> _hit = new();
    private float _t, _nextTick;

    public static void Start(HeroController hero)
    {
        if (_alive >= 2) return;
        _alive++;
        var c = hero.gameObject.AddComponent<SkyPillar>();
        c._hero = hero;
        // 小幅浮空，配合空中连段
        var body = hero.Body;
        if (body != null && body.linearVelocity.y < 5f)
            body.linearVelocity = new Vector2(body.linearVelocity.x, 5f);
        GaleFx.PlayUpSlash(hero);
    }

    private void Update()
    {
        _t += Time.deltaTime;
        if (_hero == null || _t >= Duration) { _alive--; Destroy(this); return; }
        if (_t >= _nextTick)
        {
            _nextTick += Tick;
            Vector2 center = (Vector2)_hero.transform.position + new Vector2(0, 1.5f);
            foreach (var hm in GaleCombat.EnemiesInBox(center, new Vector2(1.1f, 2.2f)))
            {
                if (!_hit.Add(hm.GetInstanceID())) continue;
                GaleCombat.ApplyHit(hm, _hero.gameObject, GaleCombat.NailDamage(_hero, 0.5f), 90f, magnitude: 0.6f);
            }
            GaleFx.PlayPillarMote((Vector3)center);
        }
    }
}

/// <summary>下劈：坠星震荡</summary>
public sealed class MeteorDive : MonoBehaviour
{
    private const float DiveSpeed = -24f, Timeout = 1.2f;
    private const int WaveTicks = 4;
    private static bool _active;

    private HeroController _hero = null!;
    private readonly HashSet<int> _hit = new();
    private float _t, _nextWave;
    private bool _impacted;
    private Vector2 _impactPos;
    private int _wave;

    public static void Start(HeroController hero)
    {
        if (_active) return;
        _active = true;
        var c = hero.gameObject.AddComponent<MeteorDive>();
        c._hero = hero;
        var body = hero.Body;
        if (body != null)
            body.linearVelocity = new Vector2(body.linearVelocity.x * 0.3f, DiveSpeed);
        GaleFx.PlayDownSlash(hero);
    }

    private void Update()
    {
        _t += Time.deltaTime;
        if (_hero == null || _t > Timeout || (_impacted && _wave > WaveTicks)) { End(); return; }
        if (!_impacted)
        {
            Vector2 below = (Vector2)_hero.transform.position + Vector2.down * 0.4f;
            bool ground = GaleCombat.CStateBool(_hero, "onGround");
            var enemies = GaleCombat.EnemiesInCircle(below, 1.1f);
            if (ground || enemies.Count > 0)
            {
                _impacted = true;
                _impactPos = below;
                if (enemies.Count > 0 && _hero.Body != null)
                    _hero.Body.linearVelocity = new Vector2(_hero.Body.linearVelocity.x, 14f); // 命中弹起
                Impact();
            }
        }
        else if (_t >= _nextWave)
        {
            _nextWave += 0.06f;
            _wave++;
            // 冲击波向两侧推进（贴身短波，不横扫全场）
            float d = _wave * 0.45f;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 front = _impactPos + new Vector2(side * d, 0.1f);
                foreach (var hm in GaleCombat.EnemiesInCircle(front, 0.6f))
                {
                    if (!_hit.Add(hm.GetInstanceID())) continue;
                    GaleCombat.ApplyHit(hm, _hero.gameObject, GaleCombat.NailDamage(_hero, 0.6f),
                        GaleCombat.AngleTo(_impactPos, hm.transform.position), circle: true);
                }
                GaleFx.PlayWaveFront((Vector3)front);
            }
        }
    }

    private void Impact()
    {
        GaleFx.PlayDiveImpact((Vector3)_impactPos);
        foreach (var hm in GaleCombat.EnemiesInCircle(_impactPos, 1.15f))
        {
            if (!_hit.Add(hm.GetInstanceID())) continue;
            GaleCombat.ApplyHit(hm, _hero.gameObject, GaleCombat.NailDamage(_hero, 1f),
                GaleCombat.AngleTo(_impactPos, hm.transform.position), circle: true, magnitude: 1.2f);
        }
    }

    private void End()
    {
        _active = false;
        Destroy(this);
    }
}

/// <summary>冲刺攻击：残影连突（带螺旋钻头突刺动画，自驱动位移）</summary>
public sealed class PhantomLunge : MonoBehaviour
{
    private const float Duration = 0.32f, Tick = 0.06f, DrillFps = 24f, LungeSpeed = 24f;
    private static bool _active;
    private static Sprite[]? _drillFrames;

    private HeroController _hero = null!;
    private readonly HashSet<int> _hit = new();
    private float _t, _nextTick;
    private GameObject? _drill;
    private SpriteRenderer? _drillRd;

    public static void Start(HeroController hero)
    {
        if (_active) return;
        _active = true;
        var c = hero.gameObject.AddComponent<PhantomLunge>();
        c._hero = hero;
        c.SpawnDrill();
        GaleFx.PlayDashStab(hero);
    }

    private void SpawnDrill()
    {
        if (_drillFrames == null)
        {
            var texs = ProceduralTextures.BuildDrill(8, 128, 64);
            _drillFrames = new Sprite[texs.Length];
            for (int i = 0; i < texs.Length; i++)
                _drillFrames[i] = Sprite.Create(texs[i], new Rect(0, 0, texs[i].width, texs[i].height),
                    new Vector2(0.35f, 0.5f), 64f); // 枢轴偏后，针尖朝前伸出
        }
        _drill = new GameObject("GaleDrill");
        _drillRd = _drill.AddComponent<SpriteRenderer>();
        _drillRd.sortingOrder = 102;
        _drill.transform.localScale = Vector3.one * 1.6f;
    }

    private void UpdateDrill()
    {
        if (_drill == null || _drillRd == null || _hero == null) return;
        float facing = Mathf.Sign(_hero.transform.localScale.x);
        _drill.transform.position = GaleFx.Center(_hero) + new Vector3(facing * 0.5f, 0f, 0f);
        _drill.transform.rotation = Quaternion.Euler(0, 0, facing > 0 ? 0f : 180f);
        _drillRd.sprite = _drillFrames![Mathf.FloorToInt(_t * DrillFps) % _drillFrames.Length];
        var col = _drillRd.color;
        col.a = _t > Duration - 0.1f ? Mathf.Max(0f, (Duration - _t) / 0.1f) : 1f;
        _drillRd.color = col;
    }

    private void Update()
    {
        _t += Time.deltaTime;
        if (_hero == null) { End(); return; }
        // 位移交给原版突进（NailSlashTravel），钻头动画与额外判定叠加其上
        UpdateDrill();
        if (_t < Duration)
        {
            if (_t >= _nextTick)
            {
                _nextTick += Tick;
                Vector2 center = GaleFx.Center(_hero);
                // 穿透：窄路径上的敌人被击中（宽度与突刺动画一致）
                foreach (var hm in GaleCombat.EnemiesInCircle(center, 0.9f))
                {
                    if (!_hit.Add(hm.GetInstanceID())) continue;
                    GaleCombat.ApplyHit(hm, _hero.gameObject, GaleCombat.NailDamage(_hero, 0.7f),
                        Mathf.Sign(_hero.transform.localScale.x) > 0 ? 0f : 180f, magnitude: 0.5f);
                }
                GaleFx.PlayAfterimage((Vector3)center);
            }
        }
        else
        {
            // 末端爆发（小范围）
            Vector2 center = GaleFx.Center(_hero);
            foreach (var hm in GaleCombat.EnemiesInCircle(center, 1.25f))
            {
                GaleCombat.ApplyHit(hm, _hero.gameObject, GaleCombat.NailDamage(_hero, 0.8f),
                    GaleCombat.AngleTo(center, hm.transform.position), circle: true, magnitude: 1.3f);
            }
            GaleFx.PlayLungeBurst((Vector3)center);
            End();
        }
    }

    private void End()
    {
        if (_drill != null) Destroy(_drill);
        _active = false;
        Destroy(this);
    }
}
