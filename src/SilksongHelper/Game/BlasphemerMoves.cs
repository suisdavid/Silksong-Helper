using System.Collections.Generic;
using UnityEngine;

namespace SilksongHelper;

/// <summary>
/// 自设纹章「亵渎者」全部自创招式（按 description.md 设计）。
/// 主题：赤红亵渎圣剑 + 血光 + 烈焰。
/// </summary>
public static class BlasphemerTheme
{
    public static readonly Color Blood = new(1f, 0.30f, 0.25f);
    public static readonly Color DarkBlood = new(0.80f, 0.15f, 0.20f);
    public static readonly Color Flame = new(1f, 0.55f, 0.20f);
}

/// <summary>
/// 亵渎圣剑挥砍（普攻/上劈/下劈共用）：程序化刀光月牙 + **真实 PolygonCollider2D 碰撞箱**，
/// 随挥砍角度扫过空间，碰撞箱重叠到的敌人才受伤害（非自动索敌）。
/// </summary>
public sealed class SwordSwing : MonoBehaviour
{
    public enum Dir { Forward, Up, Down }

    private const float Life = 0.24f, SweepTime = 0.18f, ObjScale = 1.8f;
    // 刀光弧带（与 FxTextures.Crescent(160) 参数一致，局部单位 = 像素/64）
    private const float R0 = 0.35f * 160f / 2f / 64f, R1 = 0.62f * 160f / 2f / 64f, HalfSweep = 75f;
    private static bool _alt;

    private HeroController _hero = null!;
    private Dir _dir;
    private float _facing = 1f, _a0, _a1, _t;
    private SpriteRenderer _rd = null!;
    private PolygonCollider2D _poly = null!;
    private readonly HashSet<int> _hit = new();
    private bool _bounced;
    private readonly Collider2D[] _results = new Collider2D[32];
    private ContactFilter2D _filter;

    public static void Start(HeroController hero, Dir dir)
    {
        var go = new GameObject("BlasphemerSwing");
        var c = go.AddComponent<SwordSwing>();
        c._hero = hero;
        c._dir = dir;
        c.Setup(go);
    }

    private void Setup(GameObject go)
    {
        _facing = Mathf.Sign(_hero.transform.localScale.x);
        _rd = go.AddComponent<SpriteRenderer>();
        _rd.sprite = GaleFx.Crescent;
        _rd.color = BlasphemerTheme.Blood;
        _rd.sortingOrder = 102;

        _poly = go.AddComponent<PolygonCollider2D>();
        _poly.isTrigger = true;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        _filter = new ContactFilter2D().NoFilter();
        BuildBladeCollider();

        // 挥砍角度（朝右为基准；连击交替上/下挥）
        switch (_dir)
        {
            case Dir.Forward:
                if (_alt) { _a0 = -85f; _a1 = 70f; } else { _a0 = 85f; _a1 = -70f; }
                _alt = !_alt;
                break;
            case Dir.Up: _a0 = 10f; _a1 = 165f; break;
            case Dir.Down: _a0 = -10f; _a1 = -165f; break;
        }
        go.transform.localScale = Vector3.one * ObjScale;
        UpdateTransform(0f);
    }

    /// <summary>按刀光形状生成多边形碰撞箱（外弧 + 近心小弧闭合，覆盖贴身区域，避免正下方敌人落入空心）。</summary>
    private void BuildBladeCollider()
    {
        const int N = 10;
        float rInner = R0 * 0.25f; // 内缘收到近心处：近身敌人也在判定内
        var pts = new List<Vector2>();
        for (int i = 0; i <= N; i++)
        {
            float a = Mathf.Lerp(-HalfSweep, HalfSweep, (float)i / N) * Mathf.Deg2Rad;
            pts.Add(new Vector2(Mathf.Cos(a) * R1, Mathf.Sin(a) * R1));
        }
        for (int i = N; i >= 0; i--)
        {
            float a = Mathf.Lerp(-HalfSweep, HalfSweep, (float)i / N) * Mathf.Deg2Rad;
            pts.Add(new Vector2(Mathf.Cos(a) * rInner, Mathf.Sin(a) * rInner));
        }
        _poly.points = pts.ToArray();
    }

    private void UpdateTransform(float ease)
    {
        float ang = Mathf.Lerp(_a0, _a1, ease);
        if (_facing < 0) ang = 180f - ang;
        transform.rotation = Quaternion.Euler(0, 0, ang);
        // 下劈时弧心下移，让判定正真罩住脚下；上劈略微上移
        float yOff = _dir == Dir.Down ? 0.55f : _dir == Dir.Up ? 1.1f : 0.9f;
        transform.position = _hero.transform.position + new Vector3(_facing * 0.4f, yOff, 0);
    }

    private void Update()
    {
        if (_hero == null) { Destroy(gameObject); return; }
        _t += Time.deltaTime;
        float k = Mathf.Clamp01(_t / SweepTime);
        float e = 1f - Mathf.Pow(1f - k, 3f); // easeOutCubic：快出刀、缓收刀
        UpdateTransform(e);
        DamageSweep();
        var c = _rd.color;
        c.a = _t > Life - 0.07f ? Mathf.Max(0f, (Life - _t) / 0.07f) : 1f;
        _rd.color = c;
        if (_t >= Life) Destroy(gameObject);
    }

    /// <summary>用真实碰撞箱做重叠查询（物理查询不受碰撞矩阵开关影响）。</summary>
    private void DamageSweep()
    {
        int n = _poly.OverlapCollider(_filter, _results);
        for (int i = 0; i < n; i++)
        {
            // 下劈命中敌人或障碍物（尖刺/机关等 DamageHero）都会弹起
            if (_dir == Dir.Down && !_bounced && !GaleCombat.CStateBool(_hero, "onGround"))
            {
                bool bounceable = false;
                try
                {
                    bounceable = _results[i].GetComponentInParent<DamageHero>() != null
                                 || _results[i].GetComponentInParent<HealthManager>() != null;
                }
                catch { }
                if (bounceable && _hero.Body != null)
                {
                    _bounced = true;
                    _hero.Body.linearVelocity = new Vector2(_hero.Body.linearVelocity.x, 12f);
                }
            }

            HealthManager? hm = null;
            try { hm = _results[i].GetComponentInParent<HealthManager>(); } catch { }
            if (hm == null || !_hit.Add(hm.GetInstanceID())) continue;

            float kb = _dir switch
            {
                Dir.Up => 90f,
                Dir.Down => -90f,
                _ => _facing > 0 ? 0f : 180f,
            };
            GaleCombat.ApplyHit(hm, _hero.gameObject, GaleCombat.NailDamage(_hero, 1f), kb);
            GaleFx.Spawn(GaleFx.Dot, hm.transform.position, BlasphemerTheme.Blood, 0.3f, 0.05f,
                Vector3.zero, 0f, 0f, 0f, 0.2f);
        }
    }
}

/// <summary>冲刺：化为虚影向前闪现（地形射线钳制，两端虚影特效）。</summary>
public static class PhantomBlink
{
    private const float Dist = 4f;

    public static void Do(HeroController hc)
    {
        float facing = Mathf.Sign(hc.transform.localScale.x);
        Vector2 from = hc.transform.position + new Vector3(0, 0.9f, 0);
        float d = Dist;
        int terrain = LayerMask.GetMask("Terrain");
        if (terrain != 0)
        {
            var hit = Physics2D.Raycast(from, new Vector2(facing, 0), Dist, terrain);
            if (hit.collider != null) d = Mathf.Max(0.5f, hit.distance - 0.4f);
        }
        // 起点虚影
        GaleFx.Spawn(GaleFx.Dot, (Vector3)from, BlasphemerTheme.DarkBlood, 1.2f, 0.2f, Vector3.zero, 0f, 0f, 0f, 0.3f);
        GaleFx.Spawn(GaleFx.Ring, (Vector3)from, BlasphemerTheme.Blood, 0.4f, 1.6f, Vector3.zero, 0f, 0f, 0f, 0.25f);
        for (int i = 1; i <= 3; i++) // 路径残影
            GaleFx.Spawn(GaleFx.Dot, (Vector3)(from + new Vector2(facing * d * i / 4f, 0)), BlasphemerTheme.DarkBlood,
                0.7f, 0.1f, Vector3.zero, 0f, 0f, 0f, 0.25f);

        hc.transform.position += new Vector3(facing * d, 0, 0);
        // 保留冲刺动能：闪现后向前滑行而不是骤停
        if (hc.Body != null) hc.Body.linearVelocity = new Vector2(facing * 16f, hc.Body.linearVelocity.y);

        // 落点实影
        Vector3 to = hc.transform.position + new Vector3(0, 0.9f, 0);
        GaleFx.Spawn(GaleFx.Dot, to, BlasphemerTheme.Blood, 1.0f, 0.2f, Vector3.zero, 0f, 0f, 0f, 0.25f);
    }
}

/// <summary>冲刺攻击：化身血光穿刺前方敌人（自驱动位移），经过区域留下燃烧的烈焰。</summary>
public sealed class BloodRush : MonoBehaviour
{
    private const float Duration = 0.35f, Tick = 0.05f, FlameEvery = 0.8f, RushSpeed = 22f;
    private static bool _active;

    private HeroController _hero = null!;
    private readonly HashSet<int> _hit = new();
    private float _t, _nextTick, _traveled;
    private Vector2 _lastPos;
    private GameObject? _glow;

    public static void Start(HeroController hero)
    {
        if (_active) return;
        _active = true;
        var c = hero.gameObject.AddComponent<BloodRush>();
        c._hero = hero;
        c._lastPos = hero.transform.position;
        // 血光附体
        c._glow = new GameObject("BloodGlow");
        var rd = c._glow.AddComponent<SpriteRenderer>();
        rd.sprite = GaleFx.Dot;
        rd.color = new Color(1f, 0.25f, 0.2f, 0.65f);
        rd.sortingOrder = 101;
        c._glow.transform.localScale = Vector3.one * 2.2f;
    }

    private void Update()
    {
        _t += Time.deltaTime;
        if (_hero == null || _t >= Duration) { End(); return; }

        // 位移交给原版突进（NailSlashTravel），血光只负责附体特效/额外穿刺/烈焰
        float facing = Mathf.Sign(_hero.transform.localScale.x);
        Vector2 center = GaleFx.Center(_hero);
        if (_glow != null)
        {
            _glow.transform.position = center;
            float k = 1f - _t / Duration;
            _glow.transform.localScale = Vector3.one * (1.4f + 1.2f * k);
        }

        // 穿刺伤害：血光经过的所有敌人
        if (_t >= _nextTick)
        {
            _nextTick += Tick;
            foreach (var hm in GaleCombat.EnemiesInCircle(center, 0.9f))
            {
                if (!_hit.Add(hm.GetInstanceID())) continue;
                GaleCombat.ApplyHit(hm, _hero.gameObject, GaleCombat.NailDamage(_hero, 1f),
                    Mathf.Sign(_hero.transform.localScale.x) > 0 ? 0f : 180f, magnitude: 0.6f);
            }
            GaleFx.Spawn(GaleFx.Streak, center, BlasphemerTheme.Blood, 1.2f, 0.4f,
                Vector3.zero, 0f, 0f, Mathf.Sign(_hero.transform.localScale.x) > 0 ? 0f : 180f, 0.2f);
        }

        // 沿途留下烈焰
        _traveled += Vector2.Distance(center, _lastPos);
        _lastPos = center;
        if (_traveled >= FlameEvery)
        {
            _traveled = 0f;
            FlamePatch.Spawn((Vector3)center + Vector3.down * 0.7f, _hero.gameObject);
        }
    }

    private void End()
    {
        if (_glow != null) Destroy(_glow);
        _active = false;
        Destroy(this);
    }
}

/// <summary>烈焰地带：燃烧一段时间后熄灭，周期性灼烧范围内的敌人。</summary>
public sealed class FlamePatch : MonoBehaviour
{
    private const float Life = 3f, Tick = 0.5f, Radius = 0.75f, Fps = 12f;
    private const int MaxPatches = 12;
    private static readonly List<FlamePatch> _patches = new();
    private static Sprite[]? _frames;

    private GameObject _source = null!;
    private SpriteRenderer _rd = null!;
    private float _t, _nextTick;

    public static void Spawn(Vector3 pos, GameObject source)
    {
        if (_patches.Count >= MaxPatches && _patches[0] != null)
            Destroy(_patches[0].gameObject);
        _patches.RemoveAll(p => p == null);

        if (_frames == null)
        {
            var texs = ProceduralTextures.BuildFlame(6, 64, 96);
            _frames = new Sprite[texs.Length];
            for (int i = 0; i < texs.Length; i++)
                _frames[i] = Sprite.Create(texs[i], new Rect(0, 0, texs[i].width, texs[i].height),
                    new Vector2(0.5f, 0.15f), 64f);
        }
        var go = new GameObject("BlasphemerFlame");
        go.transform.position = pos;
        var f = go.AddComponent<FlamePatch>();
        f._source = source;
        f._rd = go.AddComponent<SpriteRenderer>();
        f._rd.sortingOrder = 99;
        _patches.Add(f);
    }

    private void Update()
    {
        _t += Time.deltaTime;
        if (_t >= Life) { _patches.Remove(this); Destroy(gameObject); return; }
        _rd.sprite = _frames![Mathf.FloorToInt(_t * Fps) % _frames.Length];
        var c = _rd.color;
        c.a = _t > Life - 0.6f ? Mathf.Max(0f, (Life - _t) / 0.6f) : 1f; // 熄灭前渐弱
        _rd.color = c;
        if (_t >= _nextTick)
        {
            _nextTick += Tick;
            foreach (var hm in GaleCombat.EnemiesInCircle(transform.position, Radius))
                GaleCombat.ApplyHit(hm, _source, GaleCombat.NailDamage(null, 0.3f), 90f, magnitude: 0.2f);
        }
    }
}
