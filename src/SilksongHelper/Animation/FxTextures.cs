using UnityEngine;

namespace SilksongHelper;

/// <summary>
/// 精细特效贴图：全部程序化绘制 + 2x 超采样抗锯齿（先按 2 倍尺寸绘制再平均降采样）。
/// 输出为白色带 Alpha 的贴图，实际颜色由 SpriteRenderer.color 染色。
/// </summary>
public static class FxTextures
{
    /// <summary>刀光月牙：±sweepDeg 的弧形刀光带，两端收尖、外缘亮线。用于真实挥砍碰撞箱的视觉。</summary>
    public static Texture2D Crescent(int size = 160, float sweepDeg = 150f, float r0 = 0.35f, float r1 = 0.62f)
    {
        return Render(size, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            if (r < r0 || r > r1) return 0f;
            float ang = Mathf.Atan2(v, u) * Mathf.Rad2Deg; // 指向 +x 的弧
            float half = sweepDeg * 0.5f;
            if (Mathf.Abs(ang) > half) return 0f;
            // 两端收尖
            float taper = 1f - Mathf.Abs(ang) / half;
            taper = Mathf.Pow(taper, 0.6f);
            // 径向：外缘亮、内缘柔
            float radial = (r - r0) / (r1 - r0);
            float glow = 0.25f + 0.75f * Mathf.Pow(radial, 1.4f);
            // 外缘刀锋亮线
            float edge = Mathf.Exp(-Mathf.Pow((r - r1) / 0.045f, 2f));
            return Mathf.Clamp01((glow * 0.8f + edge) * taper);
        });
    }

    /// <summary>柔和光点：径向渐变圆，用于粒子/光尘/光晕。</summary>
    public static Texture2D SoftDot(int size = 64)
    {
        return Render(size, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v); // 0..1
            if (r >= 1f) return 0f;
            float a = 1f - r;
            return Mathf.Pow(a, 1.6f);
        });
    }

    /// <summary>柔和圆环：高斯边缘的细环，用于冲击波/治愈脉冲。</summary>
    public static Texture2D Ring(int size = 128)
    {
        const float ringR = 0.68f, width = 0.10f;
        return Render(size, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float d = Mathf.Abs(r - ringR) / width;
            float a = Mathf.Exp(-d * d * 2.2f);
            // 环外侧额外一点辉光衰减
            if (r > ringR) a *= Mathf.Max(0f, 1f - (r - ringR) * 1.6f);
            return a;
        });
    }

    /// <summary>刀光 streak：水平锥形光刃（亮芯线 + 两端收尖），旋转物体即可改变方向。</summary>
    public static Texture2D Streak(int w = 128, int h = 32)
    {
        return Render(w, h, (u, v) =>
        {
            // u: -1..1 水平, v: -1..1 垂直
            float taper = 1f - Mathf.Abs(u);           // 两端收尖
            taper = Mathf.Pow(taper, 0.7f);
            float thickness = Mathf.Lerp(0.12f, 0.55f, taper); // 尖处细、中段宽
            float d = Mathf.Abs(v) / Mathf.Max(thickness, 0.001f);
            if (d >= 1f) return 0f;
            float core = Mathf.Exp(-d * d * 6f);        // 亮芯
            float glow = Mathf.Exp(-d * d * 1.8f) * 0.6f; // 外辉
            return Mathf.Clamp01((core + glow) * taper);
        });
    }

    private static Texture2D Render(int size, System.Func<float, float, float> alpha)
        => Render(size, size, alpha);

    /// <summary>以 2x 超采样绘制（u/v ∈ -1..1），降采样输出。</summary>
    private static Texture2D Render(int w, int h, System.Func<float, float, float> alpha)
    {
        const int SS = 2;
        int sw = w * SS, sh = h * SS;
        var big = new float[sw * sh];
        for (int y = 0; y < sh; y++)
        {
            for (int x = 0; x < sw; x++)
            {
                float u = ((x + 0.5f) / sw) * 2f - 1f;
                float v = ((y + 0.5f) / sh) * 2f - 1f;
                big[y * sw + x] = alpha(u, v);
            }
        }
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float a = 0f;
                for (int dy = 0; dy < SS; dy++)
                    for (int dx = 0; dx < SS; dx++)
                        a += big[(y * SS + dy) * sw + (x * SS + dx)];
                a /= SS * SS;
                px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
        }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
