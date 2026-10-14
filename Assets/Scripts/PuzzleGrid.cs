using System.Collections.Generic;
using UnityEngine;

// Logical puzzle state of one level, independent of physics. Same rules as solve() in Tools/gen_levels.py:
// doors open with their key, a gate is open while a box sits on its plate, switch wall set A is closed at
// lever 0 and set B at lever 1, boxes can be pushed (and pulled with the Pull ability).
public class PuzzleGrid
{
    public readonly LevelData Level;
    public readonly HashSet<Vector2Int> Boxes = new();
    public readonly HashSet<string> Keys = new();
    public readonly HashSet<Vector2Int> HeldPlates = new(); // plates the player stands on (game only)
    public int Lever { get; private set; }

    readonly HashSet<(Vector2Int, Vector2Int)> open = new();
    readonly Dictionary<(Vector2Int, Vector2Int), string> doors = new();
    readonly Dictionary<(Vector2Int, Vector2Int), Vector2Int> gates = new();
    readonly HashSet<(Vector2Int, Vector2Int)> switchA = new(), switchB = new();

    public static (Vector2Int, Vector2Int) Key(Vector2Int a, Vector2Int b) =>
        a.x < b.x || (a.x == b.x && a.y < b.y) ? (a, b) : (b, a);

    public static (Vector2Int, Vector2Int) Key(Edge e) => Key(e.a.V, e.b.V);

    public PuzzleGrid(LevelData L)
    {
        Level = L;
        var hWall = new HashSet<Vector2Int>(); // (col, line) for horizontal walls on row line `line`
        var vWall = new HashSet<Vector2Int>(); // (line, row) for vertical walls on column line `line`
        foreach (var w in L.walls)
            for (int i = w.start; i < w.end; i++)
                if (w.horizontal) hWall.Add(new Vector2Int(i, w.line));
                else vWall.Add(new Vector2Int(w.line, i));
        for (int r = 0; r < L.height; r++)
            for (int c = 0; c < L.width; c++)
            {
                if (r + 1 < L.height && !hWall.Contains(new Vector2Int(c, r + 1)))
                    open.Add(Key(new Vector2Int(c, r), new Vector2Int(c, r + 1)));
                if (c + 1 < L.width && !vWall.Contains(new Vector2Int(c + 1, r)))
                    open.Add(Key(new Vector2Int(c, r), new Vector2Int(c + 1, r)));
            }

        foreach (var b in L.boxes ?? new CellPos[0]) Boxes.Add(b.V);
        foreach (var d in L.doors ?? new DoorData[0]) doors[Key(d.edge)] = d.color;
        foreach (var p in L.plates ?? new PlateData[0]) gates[Key(p.gate)] = p.cell.V;
        foreach (var s in L.switchWalls ?? new SwitchWallData[0])
            (s.set == "A" ? switchA : switchB).Add(Key(s.edge));
    }

    public bool InBounds(Vector2Int v) => v.x >= 0 && v.y >= 0 && v.x < Level.width && v.y < Level.height;

    public bool DoorOpen(string color) => Keys.Contains(color);
    public bool GateOpen(Vector2Int plate) => Boxes.Contains(plate) || HeldPlates.Contains(plate);
    public bool SwitchClosed(bool setA) => setA ? Lever == 0 : Lever == 1;

    // Can something move across the edge a→b (walls, doors, gates, switch walls)? Boxes are not checked here.
    public bool Passable(Vector2Int a, Vector2Int b)
    {
        var k = Key(a, b);
        if (!open.Contains(k)) return false;
        if (doors.TryGetValue(k, out var color) && !DoorOpen(color)) return false;
        if (gates.TryGetValue(k, out var plate) && !GateOpen(plate)) return false;
        if (switchA.Contains(k) && SwitchClosed(true)) return false;
        if (switchB.Contains(k) && SwitchClosed(false)) return false;
        return true;
    }

    public bool CanEnter(Vector2Int from, Vector2Int to) => Passable(from, to) && !Boxes.Contains(to);

    public bool TryPush(Vector2Int box, Vector2Int dir)
    {
        var to = box + dir;
        if (!Boxes.Contains(box) || !InBounds(to) || !CanEnter(box, to)) return false;
        Boxes.Remove(box);
        Boxes.Add(to);
        return true;
    }

    // The player walks from `player` in `dir`; the box right behind them follows into `player`.
    public bool TryPull(Vector2Int player, Vector2Int dir)
    {
        var box = player - dir;
        if (!Boxes.Contains(box) || !Passable(box, player) || !InBounds(player + dir) || !CanEnter(player, player + dir))
            return false;
        Boxes.Remove(box);
        Boxes.Add(player);
        return true;
    }

    public void ToggleLever() => Lever = 1 - Lever;

    // Plays the generator's solution through these rules; returns null on success, otherwise what went wrong.
    public static string Replay(LevelData L)
    {
        var g = new PuzzleGrid(L);
        var pull = new HashSet<string>(L.startAbilities ?? new string[0]);
        var p = L.start.V;
        for (int i = 0; i < L.solution.Length; i++)
        {
            var s = L.solution[i];
            var to = new Vector2Int(s.c, s.r);
            if (s.toggle)
            {
                if (L.levers.Length == 0 || L.levers[0].V != p) return $"{L.name}: step {i} toggles away from the lever";
                g.ToggleLever();
                continue;
            }
            var dir = to - p;
            bool ok = s.pull ? pull.Contains("pull") && g.TryPull(p, dir)
                : g.Boxes.Contains(to) ? g.TryPush(to, dir) && g.CanEnter(p, to)
                : g.CanEnter(p, to);
            if (!ok) return $"{L.name}: step {i} {p}→{to} not allowed (pull={s.pull})";
            p = to;
            foreach (var k in L.keys ?? new KeyData[0])
                if (k.cell.V == p) g.Keys.Add(k.color);
            foreach (var a in L.abilities ?? new AbilityData[0])
                if (a.cell.V == p) pull.Add(a.kind);
        }
        return p == L.goal.V ? null : $"{L.name}: ended at {p}, goal is {L.goal.V}";
    }
}
