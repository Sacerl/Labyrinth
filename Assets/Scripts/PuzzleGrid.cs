using System.Collections.Generic;
using UnityEngine;

// Logical puzzle state of one level, independent of physics. Same rules as Tools/puzzles.py:
// a door opens once unlocked (E beside it with its key, which is used up); a gate is open while a box sits on its
// plate or the player stands on it; switch wall set A is closed at lever 0 and set B at lever 1; the rune gate opens
// once the runes were stepped on in order. Boxes can be pushed (and pulled with the Pull ability, not on ice) and
// never leave their puzzle room. On ice you, and a pushed box, keep sliding until something is in the way or the
// floor is no longer ice. Rocks block a cell; a guard's niche tip is for the guard, never for a box.
public class PuzzleGrid
{
    public enum RuneStep { None, Advanced, Reset, Done }

    public readonly LevelData Level;
    public readonly HashSet<Vector2Int> Boxes = new();
    public readonly HashSet<string> Keys = new();      // carried, not used yet
    public readonly HashSet<string> Unlocked = new();  // door colours opened with their key
    public readonly HashSet<Vector2Int> HeldPlates = new(); // plates the player stands on (game only)
    public readonly HashSet<Vector2Int> Ice = new(), Rocks = new();
    public readonly Dictionary<Vector2Int, int> Runes = new(); // cell → place in the order (0 = first)
    public int Lever { get; private set; }
    public int RuneProgress { get; private set; }
    public bool RunesDone => Runes.Count > 0 && RuneProgress >= Runes.Count;

    readonly HashSet<(Vector2Int, Vector2Int)> open = new();
    readonly Dictionary<(Vector2Int, Vector2Int), string> doors = new();
    readonly Dictionary<(Vector2Int, Vector2Int), Vector2Int> gates = new();
    readonly HashSet<(Vector2Int, Vector2Int)> switchA = new(), switchB = new(), runeGates = new();
    readonly Dictionary<Vector2Int, int> roomOf = new();
    readonly HashSet<Vector2Int> tips = new();

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
        foreach (var c in L.ice ?? new CellPos[0]) Ice.Add(c.V);
        foreach (var c in L.rocks ?? new CellPos[0]) Rocks.Add(c.V);
        foreach (var r in L.runes ?? new RuneData[0]) Runes[r.cell.V] = r.order;
        foreach (var e in L.runeGates ?? new Edge[0]) runeGates.Add(Key(e));
        foreach (var g in L.guards ?? new GuardData[0]) tips.Add(g.tip.V);
        var rooms = L.rooms ?? new RoomData[0];
        for (int i = 0; i < rooms.Length; i++)
            foreach (var c in rooms[i].cells) roomOf[c.V] = i;
    }

    public bool InBounds(Vector2Int v) => v.x >= 0 && v.y >= 0 && v.x < Level.width && v.y < Level.height;

    public bool DoorOpen(string color) => Unlocked.Contains(color);

    // The key is used up: it leaves the inventory and its door stays open.
    public bool Unlock(string color)
    {
        if (!Keys.Remove(color)) return false;
        Unlocked.Add(color);
        return true;
    }

    // The closed door with a key in hand on one of the edges of `cell`, if any.
    public string UnlockableDoorAt(Vector2Int cell)
    {
        foreach (var (edge, color) in doors)
            if ((edge.Item1 == cell || edge.Item2 == cell) && Keys.Contains(color) && !DoorOpen(color)) return color;
        return null;
    }

    public bool GateOpen(Vector2Int plate) => Boxes.Contains(plate) || HeldPlates.Contains(plate);
    public bool SwitchClosed(bool setA) => setA ? Lever == 0 : Lever == 1;
    int RoomOf(Vector2Int v) => roomOf.TryGetValue(v, out int i) ? i : -1;

    // Can something move across the edge a→b (walls, doors, gates, switch walls, rune gate)? Boxes and rocks are
    // not checked here.
    public bool Passable(Vector2Int a, Vector2Int b)
    {
        var k = Key(a, b);
        if (!open.Contains(k)) return false;
        if (doors.TryGetValue(k, out var color) && !DoorOpen(color)) return false;
        if (gates.TryGetValue(k, out var plate) && !GateOpen(plate)) return false;
        if (switchA.Contains(k) && SwitchClosed(true)) return false;
        if (switchB.Contains(k) && SwitchClosed(false)) return false;
        if (runeGates.Contains(k) && !RunesDone) return false;
        return true;
    }

    public bool CanEnter(Vector2Int from, Vector2Int to) =>
        InBounds(to) && Passable(from, to) && !Boxes.Contains(to) && !Rocks.Contains(to);

    bool BoxCanMove(Vector2Int a, Vector2Int b) => CanEnter(a, b) && !tips.Contains(b) && RoomOf(a) == RoomOf(b);

    // Where a box pushed one step in `dir` comes to rest (it slides on ice); null if it can't move.
    public Vector2Int? PushTarget(Vector2Int box, Vector2Int dir)
    {
        if (!Boxes.Contains(box) || !BoxCanMove(box, box + dir)) return null;
        Boxes.Remove(box); // a box doesn't stop its own slide
        var to = box + dir;
        while (Ice.Contains(to) && BoxCanMove(to, to + dir)) to += dir;
        Boxes.Add(box);
        return to;
    }

    public bool TryPush(Vector2Int box, Vector2Int dir, out Vector2Int end)
    {
        var target = PushTarget(box, dir);
        end = target ?? box;
        if (target == null) return false;
        Boxes.Remove(box);
        Boxes.Add(end);
        return true;
    }

    // The player walks from `player` in `dir`; the box right behind them follows into `player`. Not on ice.
    public bool TryPull(Vector2Int player, Vector2Int dir)
    {
        var box = player - dir;
        if (!Boxes.Contains(box) || Ice.Contains(box) || Ice.Contains(player) || Ice.Contains(player + dir)) return false;
        if (RoomOf(box) != RoomOf(player) || !Passable(box, player) || !CanEnter(player, player + dir)) return false;
        Boxes.Remove(box);
        Boxes.Add(player);
        return true;
    }

    // The cells the player passes stepping from `from` in `dir`: the next cell, and on ice the rest of the slide.
    public List<Vector2Int> Walk(Vector2Int from, Vector2Int dir)
    {
        var path = new List<Vector2Int> { from + dir };
        var cur = from + dir;
        while (Ice.Contains(cur) && CanEnter(cur, cur + dir))
        {
            cur += dir;
            path.Add(cur);
        }
        return path;
    }

    public void ToggleLever() => Lever = 1 - Lever;

    // Stepping onto a cell: right rune → one further; the rune just done → nothing; the first rune → count restarts
    // at one; any other → back to zero.
    public RuneStep StepRune(Vector2Int cell)
    {
        if (!Runes.TryGetValue(cell, out int k) || RunesDone) return RuneStep.None;
        if (k == RuneProgress)
        {
            RuneProgress++;
            return RunesDone ? RuneStep.Done : RuneStep.Advanced;
        }
        if (k == RuneProgress - 1) return RuneStep.None;
        RuneProgress = k == 0 ? 1 : 0;
        return k == 0 ? RuneStep.Advanced : RuneStep.Reset;
    }

    // Plays the generator's solution through these rules; returns null on success, otherwise what went wrong.
    public static string Replay(LevelData L)
    {
        var g = new PuzzleGrid(L);
        var abilities = new HashSet<string>(L.startAbilities ?? new string[0]);
        var p = L.start.V;
        for (int i = 0; i < L.solution.Length; i++)
        {
            var s = L.solution[i];
            var to = new Vector2Int(s.c, s.r);
            g.HeldPlates.Clear();
            g.HeldPlates.Add(p); // only matters if p is a plate
            if (s.toggle)
            {
                if (L.levers.Length == 0 || L.levers[0].V != p) return $"{L.name}: step {i} toggles away from the lever";
                g.ToggleLever();
                continue;
            }
            if (s.unlock)
            {
                var color = g.UnlockableDoorAt(p);
                if (color == null) return $"{L.name}: step {i} unlocks at {p}, no locked door with its key there";
                g.Unlock(color);
                continue;
            }
            var d = to - p;
            var dir = new Vector2Int(System.Math.Sign(d.x), System.Math.Sign(d.y));
            if (dir == Vector2Int.zero || (dir.x != 0 && dir.y != 0)) return $"{L.name}: step {i} {p}→{to} is no straight move";
            List<Vector2Int> path;
            if (s.pull)
            {
                if (!abilities.Contains("pull") || !g.TryPull(p, dir)) return $"{L.name}: step {i} pull {p}→{to} not allowed";
                path = new List<Vector2Int> { p + dir };
            }
            else if (g.Boxes.Contains(p + dir))
            {
                if (!g.Passable(p, p + dir) || !g.TryPush(p + dir, dir, out _)) return $"{L.name}: step {i} push {p}→{to} not allowed";
                path = g.Walk(p, dir);
            }
            else
            {
                if (!g.CanEnter(p, p + dir)) return $"{L.name}: step {i} {p}→{to} not allowed";
                path = g.Walk(p, dir);
            }
            foreach (var c in path)
            {
                foreach (var k in L.keys ?? new KeyData[0])
                    if (k.cell.V == c) g.Keys.Add(k.color);
                foreach (var a in L.abilities ?? new AbilityData[0])
                    if (a.cell.V == c) abilities.Add(a.kind);
                g.StepRune(c);
            }
            p = path[^1];
            if (p != to) return $"{L.name}: step {i} ended at {p}, solution says {to}";
        }
        return p == L.goal.V ? null : $"{L.name}: ended at {p}, goal is {L.goal.V}";
    }
}
