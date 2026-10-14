using UnityEngine;

// Placeholder shapes generated at runtime, so the game needs no art assets yet.
public static class SpriteFactory
{
    static Sprite square, circle, heart, glow;

    // Domain reload is off when entering Play mode, so statics survive while their textures don't.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache() => square = circle = heart = glow = null;

    public static Sprite Square => square ??= Make(4, (x, y) => true);
    public static Sprite Circle => circle ??= Make(64, (x, y) => x * x + y * y <= 1f);
    // Soft round light: full in the middle, fading to nothing at the rim.
    public static Sprite Glow => glow ??= MakeAlpha(128, (x, y) =>
    {
        float d = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y));
        return d * d;
    });

    public static Sprite Heart => heart ??= Make(64, (x, y) =>
    {
        float hx = x * 1.25f, hy = y * 1.3f + 0.25f;
        float a = hx * hx + hy * hy - 1f;
        return a * a * a - hx * hx * hy * hy * hy <= 0f;
    });

    // Ignores 2D lights: the fight box and glowing specks stay fully visible in the dark levels.
    static Material unlit;
    public static Material Unlit => unlit
        ? unlit
        : unlit = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));

    static Sprite Make(int size, System.Func<float, float, bool> inside) => MakeAlpha(size, (x, y) => inside(x, y) ? 1f : 0f);

    static Sprite MakeAlpha(int size, System.Func<float, float, float> alpha)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha(u, v) * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

    // A child-less GameObject with a tinted sprite, scaled to `size` world units.
    public static SpriteRenderer Create(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size,
        Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }
}
