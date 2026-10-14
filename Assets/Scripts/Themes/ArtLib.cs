using System.Collections.Generic;
using UnityEngine;

// Cuts sprites out of the sheets in Resources/Art (16 px = 1 world unit) and caches them.
// Rectangles are given in pixels from the sheet's top-left corner, as an image editor shows them.
public static class ArtLib
{
    public const int PPU = 16;

    static readonly Dictionary<string, Texture2D> textures = new();
    static readonly Dictionary<(string, RectInt, Vector2), Sprite> sprites = new();

    // Domain reload is off when entering Play mode: drop sprites whose textures are gone.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache()
    {
        textures.Clear();
        sprites.Clear();
    }

    public static Texture2D Texture(string sheet)
    {
        if (textures.TryGetValue(sheet, out var tex) && tex) return tex;
        tex = Resources.Load<Texture2D>("Art/" + sheet);
        if (!tex) Debug.LogWarning($"ArtLib: missing sheet Art/{sheet}");
        textures[sheet] = tex;
        return tex;
    }

    public static Sprite Cut(string sheet, int x, int y, int w, int h, Vector2? pivot = null)
    {
        var key = (sheet, new RectInt(x, y, w, h), pivot ?? new Vector2(0.5f, 0.5f));
        if (sprites.TryGetValue(key, out var s) && s) return s;
        var tex = Texture(sheet);
        if (!tex) return null;
        s = Sprite.Create(tex, new Rect(x, tex.height - y - h, w, h), key.Item3, PPU, 0, SpriteMeshType.FullRect);
        s.name = $"{sheet}@{x},{y}";
        sprites[key] = s;
        return s;
    }

    // One 16×16 tile by tile coordinates.
    public static Sprite Tile(string sheet, int tx, int ty) => Cut(sheet, tx * PPU, ty * PPU, PPU, PPU);

    public static Sprite Whole(string sheet)
    {
        var tex = Texture(sheet);
        return tex ? Cut(sheet, 0, 0, tex.width, tex.height) : null;
    }

    // Character and monster sheets: one column per direction (down, up, left, right), one row per walk frame.
    public static Sprite[][] Walk(string sheet, int frames = 4, int size = 16)
    {
        var dirs = new Sprite[4][];
        for (int d = 0; d < 4; d++)
        {
            dirs[d] = new Sprite[frames];
            for (int f = 0; f < frames; f++) dirs[d][f] = Cut(sheet, d * size, f * size, size, size);
        }
        return dirs;
    }

    // A horizontal strip of `count` frames of equal width (projectiles that aren't square).
    public static Sprite[] Frames(string sheet, int count)
    {
        var tex = Texture(sheet);
        if (!tex) return new Sprite[0];
        int w = tex.width / count;
        var frames = new Sprite[count];
        for (int i = 0; i < count; i++) frames[i] = Cut(sheet, i * w, 0, w, tex.height);
        return frames;
    }

    // A horizontal strip of equally sized frames (effects).
    public static Sprite[] Strip(string sheet, int size)
    {
        var tex = Texture(sheet);
        if (!tex) return new Sprite[0];
        var frames = new Sprite[tex.width / size];
        for (int i = 0; i < frames.Length; i++) frames[i] = Cut(sheet, i * size, 0, size, size);
        return frames;
    }
}
