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
            Vector2 center = (Vector2)_hero.transform.position + new Vector2(0, 2.4f);
            foreach (var hm in GaleCombat.EnemiesInBox(center, new Vector2(1.5f, 3.6f)))
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
    private const int WaveTicks = 6;
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
            // 冲击波向两侧推进
            float d = _wave * 0.55f;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 front = _impactPos + new Vector2(side * d, 0.1f);
                foreach (var hm in GaleCombat.EnemiesInCircle(front, 0.85f))
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
        foreach (var hm in GaleCombat.EnemiesInCircle(_impactPos, 1.7f))
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

/// <summary>冲刺攻击：残影连突</summary>
public sealed class PhantomLunge : MonoBehaviour
{
    private const float Duration = 0.32f, Tick = 0.06f;
    private static bool _active;

    private HeroController _hero = null!;
    private readonly HashSet<int> _hit = new();
    private float _t, _nextTick;

    public static void Start(HeroController hero)
    {
        if (_active) return;
        _active = true;
        var c = hero.gameObject.AddComponent<PhantomLunge>();
        c._hero = hero;
        GaleFx.PlayDashStab(hero);
    }

    private void Update()
    {
        _t += Time.deltaTime;
        if (_hero == null) { _active = false; Destroy(this); return; }
        if (_t < Duration)
        {
            if (_t >= _nextTick)
            {
                _nextTick += Tick;
                Vector2 center = GaleFx.Center(_hero);
                // 穿透：路径上所有敌人都会被击中
                foreach (var hm in GaleCombat.EnemiesInCircle(center, 1.3f))
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
            // 末端爆发
            Vector2 center = GaleFx.Center(_hero);
            foreach (var hm in GaleCombat.EnemiesInCircle(center, 1.9f))
            {
                GaleCombat.ApplyHit(hm, _hero.gameObject, GaleCombat.NailDamage(_hero, 0.8f),
                    GaleCombat.AngleTo(center, hm.transform.position), circle: true, magnitude: 1.3f);
            }
            GaleFx.PlayLungeBurst((Vector3)center);
            _active = false;
            Destroy(this);
        }
    }
}
