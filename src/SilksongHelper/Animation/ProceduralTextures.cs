using UnityEngine;

namespace SilksongHelper;

public static class ProceduralTextures
{
    private const int Size = 64;

    public static Texture2D[] Build(CharmPart part, float hue = 0f, int frameCount = 8)
    {
        hue -= Mathf.Floor(hue);
        var frames = new Texture2D[frameCount];
        for (int f = 0; f < frameCount; f++)
            frames[f] = DrawFrame(part, hue, f, frameCount);
        return frames;
    }

    /// <summary>亵渎者纹章图标：赤红圣剑（剑刃+护手+剑柄）。</summary>
    public static Texture2D BuildSwordIcon(int size = 128)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = ((x + 0.5f) / size) * 2f - 1f;
                float v = ((y + 0.5f) / size) * 2f - 1f;
                Color c = new Color(0, 0, 0, 0);

                // 剑刃（上段，渐收至剑尖）
                if (v > 0.02f && v <= 0.92f)
                {
                    float t = (v - 0.02f) / 0.9f;
                    float hw = Mathf.Lerp(0.13f, 0.015f, Mathf.Pow(t, 1.3f));
                    float d = Mathf.Abs(u) / Mathf.Max(hw, 0.001f);
                    if (d <= 1f)
                    {
                        float a = Mathf.Pow(1f - d, 0.4f);
                        c = Color.Lerp(new Color(0.75f, 0.12f, 0.12f), new Color(1f, 0.45f, 0.35f), 1f - t * 0.5f);
                        c.a = Mathf.Clamp01(a + 0.25f);
                        // 中央血槽亮线
                        if (Mathf.Abs(u) < hw * 0.22f)
                            c = Color.Lerp(c, Color.white, 0.55f * (1f - t * 0.4f));
                    }
                }
                // 护手
                if (Mathf.Abs(v) < 0.05f && Mathf.Abs(u) < 0.42f)
                    c = new Color(0.45f, 0.08f, 0.10f, 1f);
                // 剑柄
                if (v <= -0.05f && v > -0.42f && Mathf.Abs(u) < 0.07f)
                    c = new Color(0.30f, 0.05f, 0.07f, 1f);
                // 柄首圆珠
                if (new Vector2(u, v + 0.5f).magnitude < 0.10f)
                    c = new Color(0.9f, 0.2f, 0.15f, 1f);

                px[y * size + x] = c;
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    /// <summary>由彩色贴图生成黑色剪影（保留 alpha）。用于纹章剪影图标。</summary>
    public static Texture2D Silhouette(Texture2D src)
    {
        var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false) { filterMode = src.filterMode };
        var px = src.GetPixels32();
        for (int i = 0; i < px.Length; i++)
            px[i] = new Color32(10, 10, 14, px[i].a);
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    /// <summary>亵渎者烈焰贴图：泪滴形火焰，边缘正弦摆动产生跳动闪烁感，红黄核心渐变。</summary>
    public static Texture2D[] BuildFlame(int frameCount = 6, int w = 64, int h = 96)
    {
        var frames = new Texture2D[frameCount];
        for (int f = 0; f < frameCount; f++)
            frames[f] = DrawFlameFrame(w, h, f * 1.31f);
        return frames;
    }

    private static Texture2D DrawFlameFrame(int w, int h, float phase)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = ((x + 0.5f) / w) * 2f - 1f;    // -1..1
                float v = (y + 0.5f) / h;                // 0(底)..1(顶)
                // 泪滴轮廓：底宽顶尖 + 左右摆动
                float sway = 0.18f * Mathf.Sin(v * 5f + phase) * v;
                float width = Mathf.Pow(Mathf.Max(0f, 1f - v), 0.55f) * 0.55f
                              * (1f + 0.16f * Mathf.Sin(v * 9f + phase * 1.7f));
                float d = Mathf.Abs(u - sway) / Mathf.Max(width, 0.001f);
                Color c = new Color(0, 0, 0, 0);
                if (d <= 1f)
                {
                    float a = Mathf.Pow(1f - d, 0.6f);
                    // 核心黄白 → 外缘血红
                    float core = Mathf.Exp(-d * d * 4f) * Mathf.Max(0f, 1f - v * 0.7f);
                    var outer = new Color(0.9f, 0.15f, 0.1f);
                    var inner = new Color(1f, 0.75f, 0.3f);
                    c = Color.Lerp(outer, inner, Mathf.Clamp01(core));
                    c.a = Mathf.Clamp01(a);
                }
                px[y * w + x] = c;
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// 冲刺招式「残影连突」的钻头动画帧：一枚螺旋织针朝 +x 方向，
    /// 螺旋纹相位随帧移动产生旋转钻进的动势。
    /// </summary>
    public static Texture2D[] BuildDrill(int frameCount = 8, int w = 128, int h = 64)
    {
        var frames = new Texture2D[frameCount];
        for (int f = 0; f < frameCount; f++)
            frames[f] = DrawDrillFrame(w, h, f * (Mathf.PI * 2f / frameCount));
        return frames;
    }

    private static Texture2D DrawDrillFrame(int w, int h, float phase)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var px = new Color32[w * h];
        var cyan = new Color(0.55f, 0.95f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = ((x + 0.5f) / w) * 2f - 1f;   // -1..1 长轴
                float v = ((y + 0.5f) / h) * 2f - 1f;   // -1..1 宽轴
                Color c = new Color(0, 0, 0, 0);

                float t = (u + 0.65f) / 1.6f;           // 0=柄端 1=针尖
                if (t >= 0f && t <= 1f)
                {
                    float width = Mathf.Lerp(0.55f, 0.03f, Mathf.Pow(t, 0.8f)); // 逐渐收尖
                    float d = Mathf.Abs(v) / Mathf.Max(width, 0.001f);
                    if (d <= 1f)
                    {
                        // 螺旋纹：斜向亮带随帧移动 → 旋转感
                        float stripe = 0.5f + 0.5f * Mathf.Sin((u * 5f + v * 7f) * Mathf.PI - phase);
                        float edge = Mathf.Pow(1f - d, 0.45f);
                        float a = edge * (0.45f + 0.55f * stripe);
                        // 针体
                        c = new Color(cyan.r, cyan.g, cyan.b, Mathf.Clamp01(a));
                        // 亮芯
                        float core = Mathf.Exp(-d * d * 5f) * (0.5f + 0.5f * stripe);
                        c = Color.Lerp(c, new Color(1f, 1f, 1f, Mathf.Clamp01(a + 0.25f)), Mathf.Clamp01(core));
                        // 针尖白热
                        if (t > 0.88f)
                            c = Color.Lerp(c, Color.white, (t - 0.88f) / 0.12f * 0.9f);
                    }
                }
                px[y * w + x] = c;
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// 自创招式「旋风丝刃」的动画帧：两片相对的丝刃月牙环绕中心旋转。
    /// 每帧旋转 step 度，完全是程序化绘制的全新动画，不取自任何游戏资产。
    /// </summary>
    public static Texture2D[] BuildCyclone(int frameCount = 16, int size = 128)
    {
        var frames = new Texture2D[frameCount];
        for (int f = 0; f < frameCount; f++)
            frames[f] = DrawCycloneFrame(size, f * (720f / frameCount));
        return frames;
    }

    private static Texture2D DrawCycloneFrame(int size, float angleDeg)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var px = new Color32[size * size];
        float half = size * 0.5f;
        var bladeCol = new Color(0.55f, 0.95f, 1f);   // 疾风青蓝
        float rInner = size * 0.22f, rOuter = size * 0.44f;
        const float bladeWidth = 42f;                  // 月牙角宽（度）

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - half, dy = y - half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                Color c = new Color(0, 0, 0, 0);
                if (r >= rInner && r <= rOuter)
                {
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    for (int b = 0; b < 2; b++)
                    {
                        float d = Mathf.Abs(Mathf.DeltaAngle(ang, angleDeg + b * 180f));
                        if (d < bladeWidth)
                        {
                            // 角向羽化 + 径向外缘亮、内缘暗
                            float feather = 1f - d / bladeWidth;
                            feather *= feather;
                            float radial = (r - rInner) / (rOuter - rInner);
                            float glow = 0.35f + 0.65f * radial;
                            float a = feather * glow;
                            if (a > c.a)
                                c = new Color(bladeCol.r, bladeCol.g, bladeCol.b, a);
                        }
                    }
                    // 外缘亮线（刀锋）
                    float edge = Mathf.Abs(r - rOuter);
                    float dEdge = Mathf.Min(
                        Mathf.Abs(Mathf.DeltaAngle(ang, angleDeg)),
                        Mathf.Abs(Mathf.DeltaAngle(ang, angleDeg + 180f)));
                    if (edge < 1.6f && dEdge < bladeWidth + 6f)
                        c = new Color(1f, 1f, 1f, Mathf.Max(c.a, 0.9f));
                }
                px[y * size + x] = c;
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    private static Texture2D DrawFrame(CharmPart part, float hue, int frame, int total)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var px = new Color32[Size * Size];
        float t = (float)frame / total;
        Color baseCol = Color.HSVToRGB(hue, 0.85f, 0.95f);
        Color bg = Color.HSVToRGB(hue, 0.30f, 0.14f);
        float half = Size * 0.5f;

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2(x - half, y - half);
                float dist = p.magnitude;
                Color c = bg;

                switch (part)
                {
                    case CharmPart.Slot:
                    {
                        float r = Size * 0.30f;
                        bool ring = Mathf.Abs(dist - r) < 3f;
                        bool dot = dist < 4f;
                        if (ring || dot) c = Color.Lerp(baseCol, Color.white, 0.6f);
                        break;
                    }
                    case CharmPart.NormalAttack:
                    {
                        float ang = Mathf.Atan2(p.y, p.x);
                        float sweep = Mathf.Repeat(t * Mathf.PI * 2f, Mathf.PI * 2f);
                        float d = Mathf.DeltaAngle(ang * Mathf.Rad2Deg, sweep * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                        if (Mathf.Abs(d) < 0.5f && dist < Size * 0.44f)
                            c = Color.Lerp(baseCol, Color.white, 1f - Mathf.Abs(d) / 0.5f);
                        break;
                    }
                    case CharmPart.HealMethod:
                    {
                        float pulse = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f);
                        float w = 4f + 5f * pulse;
                        if ((Mathf.Abs(p.x) < w || Mathf.Abs(p.y) < w) && dist < Size * 0.40f)
                            c = Color.Lerp(baseCol, Color.white, pulse);
                        break;
                    }
                    case CharmPart.ChargedAttack:
                    {
                        float grow = Mathf.Lerp(0f, 1f, t);
                        float r = Size * 0.42f * grow;
                        if (Mathf.Abs(dist - r) < 3f) c = baseCol;
                        if (dist < r * 0.25f) c = Color.Lerp(baseCol, Color.white, 0.8f);
                        break;
                    }
                    case CharmPart.DashAttack:
                    {
                        float xOff = Mathf.Lerp(-Size * 0.40f, Size * 0.40f, t);
                        if (Mathf.Abs(p.x - xOff) < 3f && Mathf.Abs(p.y) < 8f)
                            c = Color.Lerp(baseCol, Color.white, 0.7f);
                        break;
                    }
                    case CharmPart.DownSlashJump:
                    {
                        float fall = Mathf.Lerp(-Size * 0.30f, Size * 0.30f, t);
                        bool shaft = Mathf.Abs(p.y - fall) < 3f && Mathf.Abs(p.x) < 3f;
                        bool head = Mathf.Abs(p.y - (fall - 8f)) < 5f && Mathf.Abs(p.x) < 10f;
                        if (shaft || head) c = baseCol;
                        break;
                    }
                    case CharmPart.UpSlash:
                    {
                        float rise = Mathf.Lerp(Size * 0.30f, -Size * 0.30f, t);
                        bool shaft = Mathf.Abs(p.y - rise) < 3f && Mathf.Abs(p.x) < 3f;
                        bool head = Mathf.Abs(p.y - (rise + 8f)) < 5f && Mathf.Abs(p.x) < 10f;
                        if (shaft || head) c = baseCol;
                        break;
                    }
                    case CharmPart.PostHealEffect:
                    {
                        float rot = t * Mathf.PI * 2f;
                        for (int i = 0; i < 3; i++)
                        {
                            float a = rot + i * (Mathf.PI * 2f / 3f);
                            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Size * 0.30f;
                            if ((p - d).magnitude < 6f) c = baseCol;
                        }
                        if (dist < 4f) c = Color.Lerp(baseCol, Color.white, 0.7f);
                        break;
                    }
                }

                px[y * Size + x] = c;
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
