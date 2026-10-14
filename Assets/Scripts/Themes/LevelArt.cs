using System.Collections.Generic;
using UnityEngine;

// Dresses a built level in its theme's art. Never touches colliders: a gameplay object keeps its (now hidden)
// placeholder renderer and gets a "Visual" child, scaled back to 16 px = 1 unit.
public static class LevelArt
{
    public const int FloorOrder = -1000, FloorDetailOrder = -990, GroundOrder = -980, ShadowOrder = -975;

    // Draw order of standing things (hedges, trees, characters, crates): lower on screen = in front.
    public static int OrderAt(float y) => 500 - Mathf.RoundToInt(y * 10f);

    public static System.Random Random(LevelData L)
    {
        int seed = 17;
        foreach (char ch in L.name) seed = seed * 31 + ch;
        return new System.Random(seed);
    }

    // The floor is baked into one texture: a tilemap of tiles cut from a shared sheet shows thin seams
    // (neighbouring pixels bleeding in) at some zoom levels, e.g. in the level-start overview.
    public static void Floor(Transform root, LevelData L, LevelTheme theme, System.Random rnd)
    {
        int w = Mathf.RoundToInt(L.width * LevelData.Cell), h = Mathf.RoundToInt(L.height * LevelData.Cell);
        var sheet = theme.floor[0].texture;
        var tex = new Texture2D(w * ArtLib.PPU, h * ArtLib.PPU, sheet.format, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Floor " + L.name };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var r = theme.floor[rnd.Next(theme.floor.Length)].textureRect;
                Graphics.CopyTexture(sheet, 0, 0, (int)r.x, (int)r.y, ArtLib.PPU, ArtLib.PPU,
                    tex, 0, 0, x * ArtLib.PPU, y * ArtLib.PPU);
            }
        var floor = Place(root, Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero, ArtLib.PPU),
            Vector2.zero, FloorOrder);
        floor.name = "Floor";
        floor.color = theme.floorTint;
        floor.gameObject.AddComponent<DestroyTexture>().texture = tex;

        if (theme.floorDetail == null) return;
        var details = new GameObject("Floor details").transform;
        details.SetParent(root, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (rnd.NextDouble() < theme.floorDetailChance)
                    Place(details, theme.floorDetail[rnd.Next(theme.floorDetail.Length)], new Vector2(x + 0.5f, y + 0.5f),
                        FloorDetailOrder);
        if (theme.ripple == null) return;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (rnd.NextDouble() < theme.rippleChance)
                {
                    var r = Place(details, theme.ripple[0], new Vector2(x + 0.5f, y + 0.5f), FloorDetailOrder);
                    var loop = r.gameObject.AddComponent<FrameLoop>();
                    loop.frames = theme.ripple;
                    loop.fps = 3f + (float)rnd.NextDouble() * 2f;
                    loop.phase = (float)rnd.NextDouble() * 4f;
                    if (theme.rippleLight is Color glow)
                    {
                        var vent = Atmosphere.Light(r.transform, "Vent light", glow, 1.8f, 0.8f);
                        vent.gameObject.AddComponent<Flicker>().Init(0.8f, (float)rnd.NextDouble() * 10f);
                    }
                }
    }

    // Hedge pieces strung along every wall, bigger pieces (trees) on some of the points where walls meet.
    public static void Walls(Transform root, LevelData L, LevelTheme theme, System.Random rnd)
    {
        float cell = LevelData.Cell;
        var parent = new GameObject("Hedges").transform;
        parent.SetParent(root, false);
        var points = new Dictionary<Vector2Int, int>(); // grid point → how many wall runs touch it
        const float step = 0.8f;
        foreach (var w in L.walls)
        {
            float from = w.start * cell, to = w.end * cell;
            for (float s = from + step / 2f; s < to; s += step)
            {
                float jitter = (float)(rnd.NextDouble() - 0.5) * 0.12f;
                Vector2 p = w.horizontal
                    ? new Vector2(s, (L.height - w.line) * cell + jitter)
                    : new Vector2(w.line * cell + jitter, (L.height - s / cell) * cell);
                bool crystal = theme.crystals != null && rnd.NextDouble() < theme.crystalChance;
                int pick = rnd.Next(crystal ? theme.crystals.Length : theme.hedge.Length);
                var sr = Place(parent, crystal ? theme.crystals[pick] : theme.hedge[pick], p, OrderAt(p.y));
                sr.flipX = rnd.Next(2) == 0;
                if (theme.hedgeTilt > 0f)
                    sr.transform.rotation = Quaternion.Euler(0, 0, (float)(rnd.NextDouble() * 2 - 1) * theme.hedgeTilt);
                if (crystal) Atmosphere.Light(sr.transform, "Crystal light", theme.crystalColors[pick], 1.6f, 0.55f);
                else sr.color = theme.hedgeTint;
            }
            for (int k = w.start; k <= w.end; k++)
            {
                var g = w.horizontal ? new Vector2Int(k, w.line) : new Vector2Int(w.line, k);
                points[g] = points.TryGetValue(g, out int n) ? n + 1 : 1;
            }
        }
        if (theme.junction == null) return;
        foreach (var (g, n) in points)
        {
            bool corner = n > 1 || g.x == 0 || g.y == 0 || g.x == L.width || g.y == L.height;
            if (!corner) continue;
            double roll = rnd.NextDouble();
            var p = new Vector2(g.x * cell, (L.height - g.y) * cell + 0.5f); // foot on the point, top above it
            if (theme.lamps != null && roll < theme.lampChance)
            {
                int k = rnd.Next(theme.lamps.Length);
                var lamp = Place(parent, theme.lamps[k], p, OrderAt(p.y - 0.6f));
                var light = Atmosphere.Light(parent, "Lamp light", theme.lampColors[k], 3.2f, 0.9f);
                light.transform.position = p + new Vector2(0f, lamp.sprite.bounds.extents.y * 0.6f);
                light.gameObject.AddComponent<Flicker>().Init(0.9f, (float)rnd.NextDouble() * 10f);
            }
            else if (roll < theme.lampChance + theme.junctionChance)
                Place(parent, theme.junction[rnd.Next(theme.junction.Length)], p, OrderAt(p.y - 0.6f)).color =
                    theme.hedgeTint;
        }
    }

    // A barrier on a cell edge (door, plate gate, lever wall): the theme's bars tiled across the edge, with an
    // emblem in the middle that says what opens it (a key, the plate, the letter A/B). Not parented to the
    // (non-uniformly scaled) bar, so the bars stay upright in vertical walls; EdgeBar fades it while open.
    public static void Barrier(Transform root, EdgeBar bar, LevelTheme theme, Sprite emblem, Color emblemTint,
        bool vertical, float length)
    {
        var go = BarrierBase(root, bar, theme, vertical, length);
        var sr = Visual(go, emblem, 1f, ySort: false, order: go.GetComponent<SpriteRenderer>().sortingOrder + 1,
            hidePlaceholder: false);
        sr.color = emblemTint;
        sr.sharedMaterial = SpriteFactory.Unlit; // glows in the dark
    }

    public static void Door(Transform root, EdgeBar bar, LevelTheme theme, string color, bool vertical, float length)
    {
        var go = BarrierBase(root, bar, theme, vertical, length);
        var emblem = Key(go, theme, color, ySort: false, hidePlaceholder: false);
        emblem.sharedMaterial = SpriteFactory.Unlit; // glows in the dark
        emblem.sortingOrder = go.GetComponent<SpriteRenderer>().sortingOrder + 1;
    }

    static GameObject BarrierBase(Transform root, EdgeBar bar, LevelTheme theme, bool vertical, float length)
    {
        var go = new GameObject(bar.kind + " art");
        go.transform.SetParent(root, false);
        go.transform.position = bar.transform.position;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = theme.door;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = vertical ? new Vector2(1f, length) : new Vector2(length, 1f); // bars stay upright
        sr.sortingOrder = OrderAt(bar.transform.position.y);
        bar.art = go.transform;
        bar.GetComponent<SpriteRenderer>().enabled = false;
        return go;
    }

    // The key sprite for a key colour: gold as it is, silver tinted for the blue key.
    public static SpriteRenderer Key(GameObject host, LevelTheme theme, string color, bool ySort,
        bool hidePlaceholder = true)
    {
        bool blue = KeyColors.Name(color) == "blau" && theme.keyBlue;
        var sr = Visual(host, blue ? theme.keyBlue : theme.key, 1f, ySort, hidePlaceholder: hidePlaceholder);
        if (blue) sr.color = new Color(0.55f, 0.8f, 1f);
        return sr;
    }

    static SpriteRenderer Place(Transform parent, Sprite sprite, Vector2 pos, int order)
    {
        var go = new GameObject(sprite ? sprite.name : "Piece");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    // Hides the placeholder and adds a 16 px = 1 unit visual child. ySort: draw order follows height.
    public static SpriteRenderer Visual(GameObject host, Sprite sprite, float scale = 1f, bool ySort = true,
        int order = GroundOrder, bool hidePlaceholder = true)
    {
        var placeholder = host.GetComponent<SpriteRenderer>();
        if (placeholder && hidePlaceholder) placeholder.enabled = false;
        var go = new GameObject("Visual");
        go.transform.SetParent(host.transform, false);
        Vector3 ls = host.transform.lossyScale;
        go.transform.localScale = new Vector3(scale / ls.x, scale / ls.y, 1f);
        go.transform.rotation = Quaternion.identity; // e.g. the placeholder key is a rotated square
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        if (ySort) go.AddComponent<YSort>();
        return sr;
    }

    // Walking character: animated visual plus a soft shadow under its feet.
    public static SpriteRenderer Character(GameObject host, string sheet, int frames = 4, bool wobble = false)
    {
        var walk = ArtLib.Walk(sheet, frames);
        var sr = Visual(host, walk[0][0]);
        var anim = sr.gameObject.AddComponent<SpriteAnimator>();
        anim.walk = walk;
        anim.mover = host.transform;
        anim.alwaysAnimate = wobble;
        anim.fps = wobble ? 5f : 8f;

        var shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
        shadow.transform.SetParent(sr.transform, false);
        shadow.transform.localPosition = new Vector3(0f, -0.5f, 0f);
        shadow.sprite = ArtLib.Whole("Characters/Shadow");
        shadow.color = new Color(0f, 0f, 0f, 0.45f);
        shadow.sortingOrder = ShadowOrder;
        return sr;
    }
}

// Frees a texture made at runtime (the baked floor) together with its level.
public class DestroyTexture : MonoBehaviour
{
    public Texture2D texture;

    void OnDestroy()
    {
        if (texture) Destroy(texture);
    }
}
