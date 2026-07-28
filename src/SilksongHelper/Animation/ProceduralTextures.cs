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
