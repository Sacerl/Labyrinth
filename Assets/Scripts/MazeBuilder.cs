using System.Collections.Generic;
using UnityEngine;

public class BuiltLevel
{
    public Transform root;
    public PlayerController player;
    public PuzzleGrid grid;
    public readonly List<Enemy> enemies = new();
    public readonly Dictionary<Vector2Int, Box> boxes = new();
}

// Turns one LevelData into GameObjects: floor, walls with colliders, puzzles, player, enemies, pickups, hints.
public static class MazeBuilder
{
    const float Wall = 0.3f, BarThickness = 0.5f;
    const float PatrolSpeed = 2.6f, ForcedSpeed = 2f;

    // The theme only changes the look (see LevelArt): every collider is the same with or without art.
    public static BuiltLevel Build(LevelData L, LevelTheme theme)
    {
        float cell = LevelData.Cell;
        var level = new BuiltLevel { root = new GameObject($"Level: {L.name}").transform, grid = new PuzzleGrid(L) };
        var root = level.root;
        Vector2 size = new(L.width * cell, L.height * cell);
        var rnd = LevelArt.Random(L);

        if (theme.HasArt) LevelArt.Floor(root, L, theme, rnd);
        else
        {
            SpriteFactory.Create("Floor", root, SpriteFactory.Square, size / 2f, size,
                SpriteFactory.Hex("#e9ecef"), -10);
            var grid = SpriteFactory.Hex("#dee2e6");
            for (int c = 1; c < L.width; c++)
                SpriteFactory.Create("Grid", root, SpriteFactory.Square, new Vector2(c * cell, size.y / 2f),
                    new Vector2(0.04f, size.y), grid, -9);
            for (int r = 1; r < L.height; r++)
                SpriteFactory.Create("Grid", root, SpriteFactory.Square, new Vector2(size.x / 2f, r * cell),
                    new Vector2(size.x, 0.04f), grid, -9);
        }

        var wallColor = SpriteFactory.Hex("#495057");
        foreach (var w in L.walls)
        {
            Vector2 pos, dim;
            if (w.horizontal)
            {
                pos = new Vector2((w.start + w.end) / 2f * cell, (L.height - w.line) * cell);
                dim = new Vector2((w.end - w.start) * cell + Wall, Wall);
            }
            else
            {
                pos = new Vector2(w.line * cell, (L.height - (w.start + w.end) / 2f) * cell);
                dim = new Vector2(Wall, (w.end - w.start) * cell + Wall);
            }
            var sr = SpriteFactory.Create("Wall", root, SpriteFactory.Square, pos, dim, wallColor, 0);
            sr.gameObject.AddComponent<BoxCollider2D>();
            sr.enabled = !theme.HasArt;
        }
        if (theme.HasArt) LevelArt.Walls(root, L, theme, rnd);
        else
            SpriteFactory.Create("Start", root, SpriteFactory.Square, L.CellCenter(L.start), Vector2.one * 2.2f,
                SpriteFactory.Hex("#b2f2bb"), -8);

        var goal = Trigger("Goal", root, SpriteFactory.Square, L.CellCenter(L.goal), 2.2f, "#ff8787", -8, Pickup.Kind.Goal, null);
        if (theme.goal != null)
            LevelArt.Visual(goal, theme.goal[0], ySort: false).gameObject.AddComponent<FrameLoop>().frames = theme.goal;
        Atmosphere.Glow(goal, theme.goalLight, 4f, 1.4f, 0.25f);
        var heart = Trigger("Heart", root, SpriteFactory.Heart, L.CellCenter(L.heart), 1.1f, "#e64980", 1, Pickup.Kind.Heart, null);
        if (theme.heart) LevelArt.Visual(heart, theme.heart);
        Atmosphere.Glow(heart, new Color(1f, 0.35f, 0.45f), 1.8f, 1f, 0.3f);
        foreach (var h in L.hints ?? new Hint[0])
        {
            var hint = Trigger("Hint", root, SpriteFactory.Circle, L.CellCenter(h.cell), 2.4f, "#fff3bf", -7, Pickup.Kind.Hint, h.text);
            if (theme.hint) LevelArt.Visual(hint, theme.hint, ySort: false);
            Atmosphere.Glow(hint, new Color(1f, 0.9f, 0.6f), 1.5f, 0.6f);
        }

        BuildPuzzles(L, level, theme);

        foreach (var run in L.patrols) level.enemies.Add(MakeEnemy(L, root, run, false, theme));
        foreach (var run in L.forced) level.enemies.Add(MakeEnemy(L, root, run, true, theme));

        var player = SpriteFactory.Create("Player", root, SpriteFactory.Circle, L.CellCenter(L.start),
            Vector2.one * 1.2f, SpriteFactory.Hex("#1971c2"), 3).gameObject;
        player.AddComponent<Rigidbody2D>();
        player.AddComponent<CircleCollider2D>().radius = 0.5f;
        level.player = player.AddComponent<PlayerController>();
        LevelArt.Character(player, PlayerChoice.Sheet);
        Atmosphere.Create(root, L, theme, player.transform);
        return level;
    }

    static void BuildPuzzles(LevelData L, BuiltLevel level, LevelTheme theme)
    {
        var root = level.root;
        foreach (var k in L.keys ?? new KeyData[0])
        {
            var key = Trigger("Key", root, SpriteFactory.Square, L.CellCenter(k.cell), 0.9f, k.color, 1, Pickup.Kind.Key, k.color);
            key.transform.rotation = Quaternion.Euler(0, 0, 45);
            if (theme.key) LevelArt.Key(key, theme, k.color, ySort: true);
            Atmosphere.Glow(key, SpriteFactory.Hex(k.color), 1.6f, 1f, 0.3f);
        }
        foreach (var d in L.doors ?? new DoorData[0])
        {
            var door = Bar(L, root, d.edge, d.color, EdgeBar.Kind.Door);
            door.color = d.color;
            if (theme.door)
            {
                LevelArt.Door(root, door, theme, d.color, Vertical(d.edge), LevelData.Cell - Wall);
                Atmosphere.Glow(door.gameObject, SpriteFactory.Hex(d.color), 1.4f, 0.6f, 0.3f);
            }
        }
        foreach (var p in L.plates ?? new PlateData[0])
        {
            var plate = SpriteFactory.Create("Plate", root, SpriteFactory.Square, L.CellCenter(p.cell), Vector2.one * 1.8f,
                SpriteFactory.Hex("#0ca678"), -6).gameObject;
            var col = plate.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = Vector2.one * 0.6f;
            var pp = plate.AddComponent<PressurePlate>();
            pp.cell = p.cell.V;
            var gate = Bar(L, root, p.gate, "#0ca678", EdgeBar.Kind.Gate);
            gate.plate = p.cell.V;
            if (theme.plateUp)
            {
                pp.art = LevelArt.Visual(plate, theme.plateUp, 2f, ySort: false);
                pp.up = theme.plateUp;
                pp.down = theme.plateDown;
                LevelArt.Barrier(root, gate, theme, theme.plateDown, Color.white, Vertical(p.gate), LevelData.Cell - Wall);
                Atmosphere.Glow(gate.gameObject, new Color(0.65f, 0.85f, 0.3f), 1.4f, 0.5f, 0.3f);
            }
        }
        foreach (var s in L.switchWalls ?? new SwitchWallData[0])
        {
            var wall = Bar(L, root, s.edge, s.set == "A" ? "#7048e8" : "#ae3ec9",
                s.set == "A" ? EdgeBar.Kind.SwitchA : EdgeBar.Kind.SwitchB);
            if (theme.letterA)
                LevelArt.Barrier(root, wall, theme, s.set == "A" ? theme.letterA : theme.letterB, Color.white,
                    Vertical(s.edge), LevelData.Cell - Wall);
        }
        foreach (var lv in L.levers ?? new CellPos[0])
        {
            var lever = SpriteFactory.Create("Lever", root, SpriteFactory.Circle, L.CellCenter(lv), Vector2.one * 1.3f,
                SpriteFactory.Hex("#7048e8"), -5).gameObject;
            var lv2 = lever.AddComponent<Lever>();
            lv2.cell = lv.V;
            if (theme.leverOff)
            {
                lv2.art = LevelArt.Visual(lever, theme.leverOff, 2f);
                lv2.off = theme.leverOff;
                lv2.on = theme.leverOn;
            }
            Atmosphere.Glow(lever, SpriteFactory.Hex("#9775fa"), 1.6f, 0.8f, 0.3f);
        }
        foreach (var a in L.abilities ?? new AbilityData[0])
        {
            var ability = Trigger(a.kind == "pull" ? "Glove" : "Shield", root, SpriteFactory.Circle, L.CellCenter(a.cell),
                1.2f, a.kind == "pull" ? "#1098ad" : "#74c0fc", 1, Pickup.Kind.Ability, a.kind);
            var look = a.kind == "pull" ? theme.glove : theme.shield;
            if (look) LevelArt.Visual(ability, look, 1f);
            Atmosphere.Glow(ability, new Color(0.5f, 0.9f, 1f), 2f, 1f, 0.35f);
        }
        foreach (var b in L.boxes ?? new CellPos[0])
        {
            var go = SpriteFactory.Create("Box", root, SpriteFactory.Square, L.CellCenter(b), Vector2.one * 2.3f,
                SpriteFactory.Hex("#b08968"), 2).gameObject;
            if (theme.box) LevelArt.Visual(go, theme.box, 2f);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            go.AddComponent<BoxCollider2D>();
            var box = go.AddComponent<Box>();
            box.Init(b.V);
            level.boxes[b.V] = box;
        }
    }

    static bool Vertical(Edge e) => e.a.V.y == e.b.V.y; // same row: the edge lies in a vertical wall

    // A colored bar across the edge between two cells, with a collider that EdgeBar switches on and off.
    static EdgeBar Bar(LevelData L, Transform root, Edge e, string color, EdgeBar.Kind kind)
    {
        Vector2Int a = e.a.V, b = e.b.V;
        float cell = LevelData.Cell;
        Vector2 pos, dim;
        if (a.y == b.y) // same row: vertical bar on the column line between them
        {
            pos = new Vector2(Mathf.Max(a.x, b.x) * cell, (L.height - a.y) * cell - cell / 2f);
            dim = new Vector2(BarThickness, cell - Wall);
        }
        else
        {
            pos = new Vector2(a.x * cell + cell / 2f, (L.height - Mathf.Max(a.y, b.y)) * cell);
            dim = new Vector2(cell - Wall, BarThickness);
        }
        var go = SpriteFactory.Create(kind.ToString(), root, SpriteFactory.Square, pos, dim, SpriteFactory.Hex(color), 1)
            .gameObject;
        go.AddComponent<BoxCollider2D>();
        var bar = go.AddComponent<EdgeBar>();
        bar.kind = kind;
        return bar;
    }

    static GameObject Trigger(string name, Transform root, Sprite sprite, Vector2 pos, float size, string color,
        int order, Pickup.Kind kind, string text)
    {
        var go = SpriteFactory.Create(name, root, sprite, pos, Vector2.one * size, SpriteFactory.Hex(color), order)
            .gameObject;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;
        var p = go.AddComponent<Pickup>();
        p.kind = kind;
        p.text = text;
        return go;
    }

    static Enemy MakeEnemy(LevelData L, Transform root, EnemyRun run, bool forced, LevelTheme theme)
    {
        Vector2 a = L.CellCenter(run.cells[0]), b = L.CellCenter(run.cells[^1]);
        var go = SpriteFactory.Create(forced ? "Enemy (forced)" : "Enemy (patrol)", root, SpriteFactory.Circle, a,
            Vector2.one * 1.4f, SpriteFactory.Hex(forced ? "#e03131" : "#fd7e14"), 2).gameObject;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;
        string sheet = forced ? theme.forcedSheet : theme.patrolSheet;
        if (sheet != null)
        {
            var look = LevelArt.Character(go, sheet);
            if (!forced)
            {
                look.color = theme.patrolTint;
                if (theme.patrolGlow is Color glow) Atmosphere.Glow(go, glow, 1.6f, 0.7f, 0.3f);
            }
        }
        if (forced) Atmosphere.Glow(go, new Color(1f, 0.25f, 0.2f), 1.5f, 0.6f, 0.4f); // must-fight: a red warning glow
        var e = go.AddComponent<Enemy>();
        e.Init(a, b, forced, forced ? ForcedSpeed : PatrolSpeed);
        return e;
    }
}
