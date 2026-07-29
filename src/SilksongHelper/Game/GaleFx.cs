using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SilksongHelper;

/// <summary>
/// 疾风纹章精细特效库：普攻/上劈/下劈/冲刺/缚丝 五套独立特效。
/// 全部为程序化贴图 + 轻量粒子（SpriteRenderer），按招式主题色与运动方式区分。
/// </summary>
public static class GaleFx
{
    public static readonly Color Cyan = new(0.55f, 0.95f, 1f);       // 疾风青蓝（普攻/冲刺）
    public static readonly Color Sky = new(0.70f, 1.00f, 1f);        // 青霄（上劈）
    public static readonly Color Azure = new(0.40f, 0.80f, 1f);      // 深蓝（下劈）
    public static readonly Color Jade = new(0.60f, 1.00f, 0.85f);    // 丝愈青绿（缚丝）

    private static Sprite? _dot, _ring, _streak;
    private static Sprite Dot => _dot ??= MakeSprite(FxTextures.SoftDot(64));
    private static Sprite Ring => _ring ??= MakeSprite(FxTextures.Ring(128));
    private static Sprite Streak => _streak ??= MakeSprite(FxTextures.Streak(128, 32));

    private static Sprite MakeSprite(Texture2D t)
        => Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 64f);

    public static Vector3 Center(HeroController h) => h.transform.position + new Vector3(0f, 0.9f, 0f);

    private static readonly System.Random _rng = new();
    private static float Rand(float a, float b) => Mathf.Lerp(a, b, (float)_rng.NextDouble());

    private static FxParticle Spawn(Sprite spr, Vector3 pos, Color color,
        float scale0, float scale1, Vector3 vel, float drag, float angVel, float rotZ, float life)
    {
        var go = new GameObject("GaleFx");
        var c = go.AddComponent<FxParticle>();
        var rd = go.AddComponent<SpriteRenderer>();
        rd.sprite = spr;
        rd.color = color;
        rd.sortingOrder = 101;
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0, 0, rotZ);
        go.transform.localScale = Vector3.one * scale0;
        c.Init(rd, color, scale0, scale1, vel, drag, angVel, life);
        return c;
    }

    /* ================= 普攻：旋风丝刃·补充层 ================= */
    public static void PlayCycloneExtras(HeroController hero)
    {
        Vector3 c = Center(hero);
        // 冲击环
        Spawn(Ring, c, Cyan, 0.5f, 2.8f, Vector3.zero, 0f, 0f, 0f, 0.35f);
        // 外圈火花
        for (int i = 0; i < 10; i++)
        {
            float ang = i * 36f + Rand(-10f, 10f);
            var dir = new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad), 0);
            Spawn(Dot, c + dir * 0.6f, i % 3 == 0 ? Color.white : Cyan,
                Rand(0.18f, 0.3f), 0.05f, dir * Rand(3.5f, 6f), 3f, 0f, 0f, Rand(0.3f, 0.45f));
        }
        // 内圈逆向光尘环
        WispRing.Spawn(hero, Dot, Cyan, 6, 0.85f, -760f, 0.5f);
    }

    /* ================= 上劈：青霄刺 ================= */
    public static void PlayUpSlash(HeroController hero)
    {
        Vector3 c = Center(hero);
        Spawn(Ring, c + Vector3.up * 0.3f, Sky, 0.4f, 1.8f, Vector3.zero, 0f, 0f, 0f, 0.3f);
        for (int i = -1; i <= 1; i++)
        {
            Spawn(Streak, c + new Vector3(i * 0.3f, 0.9f, 0), i == 0 ? Color.white : Sky,
                0.9f, 1.4f, new Vector3(i * 0.8f, Rand(4f, 5.5f), 0), 2.5f, 0f, 90f + i * 12f, 0.3f);
        }
        for (int i = 0; i < 6; i++)
            Spawn(Dot, c + new Vector3(Rand(-0.5f, 0.5f), Rand(0.2f, 0.8f), 0), Sky,
                Rand(0.15f, 0.25f), 0.04f, new Vector3(Rand(-0.6f, 0.6f), Rand(1.8f, 3f), 0), 1.5f, 0f, 0f, Rand(0.4f, 0.55f));
    }

    /* ================= 下劈：坠星刺 ================= */
    public static void PlayDownSlash(HeroController hero)
    {
        Vector3 c = Center(hero);
        for (int i = -1; i <= 1; i++)
        {
            Spawn(Streak, c + new Vector3(i * 0.3f, -0.7f, 0), i == 0 ? Color.white : Azure,
                0.9f, 1.5f, new Vector3(i * 0.7f, Rand(-7f, -5.5f), 0), 2f, 0f, -90f + i * 12f, 0.28f);
        }
        for (int i = 0; i < 8; i++)
            Spawn(Dot, c + new Vector3(Rand(-0.5f, 0.5f), 0, 0), Azure,
                Rand(0.15f, 0.28f), 0.04f, new Vector3(Rand(-1.2f, 1.2f), Rand(-4.5f, -2.5f), 0), 1f, 0f, 0f, Rand(0.3f, 0.45f));
    }

    /* ================= 冲刺攻击：疾影突 ================= */
    public static void PlayDashStab(HeroController hero)
    {
        float facing = Mathf.Sign(hero.transform.localScale.x);
        Vector3 c = Center(hero);
        for (int i = 0; i < 4; i++)
        {
            float back = -facing;
            Spawn(Streak, c + new Vector3(back * (0.3f + i * 0.35f), Rand(-0.25f, 0.25f), 0),
                i == 0 ? Color.white : Cyan,
                Rand(0.7f, 1f) * 1.4f, 0.4f, new Vector3(back * Rand(1.5f, 2.5f), 0, 0), 3f, 0f,
                facing > 0 ? 0f : 180f, 0.22f + i * 0.03f);
        }
        Spawn(Ring, c, Cyan, 0.3f, 1.4f, Vector3.zero, 0f, 0f, 0f, 0.25f);
    }

    /* ================= 缚丝：丝愈之环 ================= */
    public static void PlayBind(HeroController hero) => BindAura.Begin(hero, Dot, Ring, Jade);

    /* ================= 自创招式补充特效 ================= */
    /// <summary>青霄柱：柱内光尘上涌</summary>
    public static void PlayPillarMote(Vector3 center)
    {
        Spawn(Dot, center + new Vector3(Rand(-0.6f, 0.6f), Rand(-1.4f, 0f), 0), Sky,
            Rand(0.15f, 0.25f), 0.04f, new Vector3(0, Rand(2.5f, 4f), 0), 1f, 0f, 0f, 0.35f);
    }

    /// <summary>坠星震荡：冲击波波前</summary>
    public static void PlayWaveFront(Vector3 pos)
    {
        Spawn(Streak, pos + Vector3.up * 0.15f, Azure, 0.7f, 1.1f, Vector3.up * 1.2f, 2f, 0f, 90f, 0.22f);
        Spawn(Dot, pos, Azure, 0.25f, 0.05f, Vector3.up * 2f, 1.5f, 0f, 0f, 0.3f);
    }

    /// <summary>坠星震荡：落地冲击</summary>
    public static void PlayDiveImpact(Vector3 pos)
    {
        Spawn(Ring, pos, Azure, 0.6f, 3.4f, Vector3.zero, 0f, 0f, 0f, 0.4f);
        foreach (float side in new[] { -1f, 1f })
        {
            Spawn(Streak, pos + new Vector3(side * 0.9f, 0.15f, 0), Color.white,
                1.3f, 0.5f, new Vector3(side * 6f, 0, 0), 4f, 0f, side > 0 ? 0f : 180f, 0.3f);
        }
        for (int i = 0; i < 10; i++)
        {
            float ang = Rand(20f, 160f) * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0);
            Spawn(Dot, pos, i % 2 == 0 ? Color.white : Azure, Rand(0.2f, 0.32f), 0.05f,
                dir * Rand(3f, 6f), 2.5f, 0f, 0f, Rand(0.3f, 0.5f));
        }
    }

    /// <summary>残影连突：路径残影</summary>
    public static void PlayAfterimage(Vector3 pos)
    {
        Spawn(Dot, pos, new Color(0.55f, 0.95f, 1f, 0.7f), 0.7f, 0.2f, Vector3.zero, 0f, 0f, 0f, 0.3f);
    }

    /// <summary>残影连突：末端爆发</summary>
    public static void PlayLungeBurst(Vector3 pos)
    {
        Spawn(Ring, pos, Cyan, 0.5f, 3.2f, Vector3.zero, 0f, 0f, 0f, 0.35f);
        for (int i = 0; i < 8; i++)
        {
            float ang = i * 45f + Rand(-8f, 8f);
            var dir = new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad), 0);
            Spawn(Streak, pos + dir * 0.7f, i % 2 == 0 ? Color.white : Cyan,
                0.8f, 0.3f, dir * Rand(5f, 7f), 3.5f, 0f, ang, 0.28f);
        }
    }

    /* ================= 粒子组件 ================= */
    public sealed class FxParticle : MonoBehaviour
    {
        private SpriteRenderer _rd = null!;
        private Color _color;
        private float _s0, _s1, _drag, _angVel, _life, _t;
        private Vector3 _vel;

        public void Init(SpriteRenderer rd, Color color, float s0, float s1, Vector3 vel, float drag, float angVel, float life)
        {
            _rd = rd; _color = color; _s0 = s0; _s1 = s1; _vel = vel; _drag = drag; _angVel = angVel; _life = life;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            if (_t >= _life) { Destroy(gameObject); return; }
            float k = _t / _life;
            transform.position += _vel * Time.deltaTime;
            _vel *= 1f - _drag * Time.deltaTime;
            transform.Rotate(0, 0, _angVel * Time.deltaTime);
            transform.localScale = Vector3.one * Mathf.Lerp(_s0, _s1, k);
            var c = _color;
            c.a = _color.a * (1f - k) * Mathf.Min(1f, _t * 12f); // 快速淡入
            _rd.color = c;
        }
    }

    /* ================= 逆向光尘环（普攻补充） ================= */
    public sealed class WispRing : MonoBehaviour
    {
        private HeroController _hero = null!;
        private float _angVel, _life, _t;
        private readonly List<SpriteRenderer> _dots = new();
        private float _radius;
        private Color _color;

        public static void Spawn(HeroController hero, Sprite spr, Color color, int count, float radius, float angVel, float life)
        {
            var go = new GameObject("GaleFxWisps");
            var w = go.AddComponent<WispRing>();
            w._hero = hero; w._angVel = angVel; w._life = life; w._radius = radius; w._color = color;
            for (int i = 0; i < count; i++)
            {
                var d = new GameObject("wisp");
                d.transform.SetParent(go.transform, false);
                float a = i * (360f / count);
                d.transform.localPosition = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * radius, Mathf.Sin(a * Mathf.Deg2Rad) * radius, 0);
                var rd = d.AddComponent<SpriteRenderer>();
                rd.sprite = spr; rd.color = color; rd.sortingOrder = 100;
                rd.transform.localScale = Vector3.one * 0.22f;
                w._dots.Add(rd);
            }
            go.transform.position = Center(hero);
        }

        private void Update()
        {
            _t += Time.deltaTime;
            if (_hero == null || _t >= _life) { Destroy(gameObject); return; }
            transform.position = Center(_hero);
            transform.Rotate(0, 0, _angVel * Time.deltaTime);
            float a = 1f - _t / _life;
            foreach (var rd in _dots)
            {
                var c = _color; c.a = _color.a * a;
                rd.color = c;
            }
        }
    }

    /* ================= 缚丝光环 ================= */
    public sealed class BindAura : MonoBehaviour
    {
        private HeroController _hero = null!;
        private Sprite _dot = null!, _ring = null!;
        private Color _color;
        private float _t, _nextRing, _nextMote;
        private SpriteRenderer _glow = null!;

        public static void Begin(HeroController hero, Sprite dot, Sprite ring, Color color)
        {
            var go = new GameObject("GaleFxBind");
            var b = go.AddComponent<BindAura>();
            b._hero = hero; b._dot = dot; b._ring = ring; b._color = color;
            var glow = new GameObject("glow");
            glow.transform.SetParent(go.transform, false);
            b._glow = glow.AddComponent<SpriteRenderer>();
            b._glow.sprite = dot; b._glow.sortingOrder = 99;
            glow.transform.localScale = Vector3.one * 2.2f;
            go.transform.position = Center(hero);
        }

        private bool IsBinding()
        {
            try
            {
                var cs = AccessTools.Field(typeof(HeroController), "cState")?.GetValue(_hero);
                return cs != null && (bool)(AccessTools.Field(cs.GetType(), "isBinding")?.GetValue(cs) ?? false);
            }
            catch { return false; }
        }

        private void Update()
        {
            _t += Time.deltaTime;
            if (_hero == null || !IsBinding() || _t > 2.5f) { Destroy(gameObject); return; }
            transform.position = Center(_hero);
            // 中央呼吸光晕
            float pulse = 0.22f + 0.08f * Mathf.Sin(_t * 9f);
            var gc = _color; gc.a = pulse;
            _glow.color = gc;
            // 上升脉冲环
            if (_t >= _nextRing)
            {
                _nextRing = _t + 0.45f;
                GaleFx.Spawn(_ring, transform.position, _color, 0.5f, 1.9f, Vector3.up * 0.6f, 0.5f, 0f, 0f, 0.6f);
            }
            // 环绕上升光尘
            if (_t >= _nextMote)
            {
                _nextMote = _t + 0.09f;
                float ang = Rand(0f, 360f) * Mathf.Deg2Rad;
                var off = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang) * 0.5f, 0) * Rand(0.5f, 0.9f);
                GaleFx.Spawn(_dot, transform.position + off, _color, Rand(0.12f, 0.2f), 0.03f,
                    new Vector3(-off.x * 0.4f, Rand(1.2f, 2f), 0), 0.8f, 0f, 0f, Rand(0.6f, 0.9f));
            }
        }
    }
}
