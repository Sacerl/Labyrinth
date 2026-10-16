using System;
using UnityEngine;

// Mirrors Assets/Resources/levels.json, written by Tools/gen_levels.py.
[Serializable] public class CellPos
{
    public int r, c;

    // Grid cell as Vector2Int: x = column, y = row (row 0 is the top row).
    public Vector2Int V => new(c, r);
}

[Serializable] public class WallRun
{
    public bool horizontal;
    public int line, start, end;
}

[Serializable] public class EnemyRun { public CellPos[] cells; }

[Serializable] public class Hint
{
    public CellPos cell;
    public string text;
}

[Serializable] public class Edge { public CellPos a, b; }
[Serializable] public class KeyData { public CellPos cell; public string color; }
[Serializable] public class DoorData { public Edge edge; public string color; }
[Serializable] public class PlateData { public CellPos cell; public Edge gate; }
[Serializable] public class SwitchWallData { public Edge edge; public string set; }
[Serializable] public class AbilityData { public string kind; public CellPos cell; }
[Serializable] public class SolutionStep { public int r, c; public bool toggle, unlock, pull; }
[Serializable] public class RuneData { public CellPos cell; public int order; }
// A must-fight guard patrolling `cells`; a box pushed along its corridor shoves it past `seal` into `tip`.
[Serializable] public class GuardData { public CellPos[] cells; public CellPos seal, tip; }
[Serializable] public class RoomData { public string kind; public CellPos[] cells; }

[Serializable] public class LevelData
{
    public string name, intro;
    public int width, height, waves;
    public float fightTier;
    public WallRun[] walls;
    public CellPos start, goal, heart;
    public EnemyRun[] forced, patrols;
    public Hint[] hints;
    public CellPos[] boxes, levers;
    public KeyData[] keys;
    public DoorData[] doors;
    public PlateData[] plates;
    public SwitchWallData[] switchWalls;
    public AbilityData[] abilities;
    public string[] startAbilities;
    public CellPos[] ice, rocks;
    public RuneData[] runes;
    public Edge[] runeGates;
    public GuardData[] guards;
    public RoomData[] rooms;
    public float reveal; // seconds the level-start overview is shown (longer with runes to memorise)
    public SolutionStep[] solution;

    public const float Cell = 3f;

    // Row 0 is the top row, matching the paper prototype.
    public Vector2 CellCenter(CellPos p) => CellCenter(p.V);
    public Vector2 CellCenter(Vector2Int v) => new(v.x * Cell + Cell / 2f, (height - v.y) * Cell - Cell / 2f);

    public Vector2Int WorldToCell(Vector2 w) =>
        new(Mathf.FloorToInt(w.x / Cell), height - 1 - Mathf.FloorToInt(w.y / Cell));

    public Rect Bounds => new(0, 0, width * Cell, height * Cell);
}

[Serializable] public class LevelSet
{
    public LevelData[] levels;

    public static LevelSet Load() =>
        JsonUtility.FromJson<LevelSet>(Resources.Load<TextAsset>("levels").text);
}

public static class KeyColors
{
    public static string Name(string hex) => hex.ToLowerInvariant() switch
    {
        "#f59f00" => "gelb",
        "#1c7ed6" => "blau",
        _ => "farbig",
    };
}
