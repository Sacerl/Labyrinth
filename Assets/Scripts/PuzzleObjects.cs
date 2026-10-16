using System.Collections;
using UnityEngine;

// A box occupying one grid cell. PuzzleGrid decides whether it may move; this only animates the slide.
public class Box : MonoBehaviour
{
    public Vector2Int Cell { get; private set; }
    public bool Sliding { get; private set; }

    Rigidbody2D rb;
    Collider2D col;

    public void Init(Vector2Int cell)
    {
        Cell = cell;
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    public void SlideTo(Vector2Int cell, Vector2 world, float duration, Collider2D ignore = null)
    {
        Cell = cell;
        StopAllCoroutines();
        StartCoroutine(Slide(world, duration, ignore));
    }

    IEnumerator Slide(Vector2 target, float duration, Collider2D ignore)
    {
        Sliding = true;
        if (ignore) Physics2D.IgnoreCollision(col, ignore, true);
        Vector2 from = rb.position;
        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            rb.MovePosition(Vector2.Lerp(from, target, t / duration));
            yield return null;
        }
        rb.MovePosition(target);
        yield return new WaitForFixedUpdate();
        Sliding = false;
        if (ignore)
        {
            // wait until the player has left the box before colliding again
            while (ignore && Vector2.Distance(ignore.transform.position, target) < 1.9f) yield return null;
            if (ignore) Physics2D.IgnoreCollision(col, ignore, false);
        }
    }
}

// A bar on a cell edge that can be closed or open: door (key), gate (plate) or switch wall (lever).
// Doors don't open by themselves: standing beside one with its key, E unlocks it.
public class EdgeBar : MonoBehaviour
{
    public enum Kind { Door, Gate, SwitchA, SwitchB, Rune }

    public Kind kind;
    public Vector2Int a, b;   // the two cells on either side
    public string color;      // door: key color
    public Vector2Int plate;  // gate: its plate
    public Transform art;     // themed look (LevelArt.Door), faded like the placeholder

    SpriteRenderer sr;
    SpriteRenderer[] artParts;
    Color[] artColors;
    Collider2D col;
    Color baseColor;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        baseColor = sr.color;
    }

    void Start()
    {
        if (!art) return;
        artParts = art.GetComponentsInChildren<SpriteRenderer>();
        artColors = System.Array.ConvertAll(artParts, p => p.color);
    }

    bool Closed(PuzzleGrid g) => kind switch
    {
        Kind.Door => !g.DoorOpen(color),
        Kind.Gate => !g.GateOpen(plate),
        Kind.SwitchA => g.SwitchClosed(true),
        Kind.Rune => !g.RunesDone,
        _ => g.SwitchClosed(false),
    };

    void Update()
    {
        var g = GameManager.Instance.Grid;
        if (g == null) return;
        bool closed = Closed(g);
        if (kind == Kind.Door) DoorInteraction(g, closed);
        col.enabled = closed;
        sr.color = closed ? baseColor : new Color(baseColor.r, baseColor.g, baseColor.b, 0.15f);
        if (artParts != null)
            for (int i = 0; i < artParts.Length; i++)
            {
                var c = artColors[i];
                artParts[i].color = closed ? c : new Color(c.r, c.g, c.b, 0.12f);
            }
    }

    bool wasNear;

    // Walking up to a locked door says what it needs; with the key in hand, E unlocks it.
    void DoorInteraction(PuzzleGrid g, bool closed)
    {
        var gm = GameManager.Instance;
        var cell = gm.PlayerCell;
        bool near = closed && gm.CanAct && (cell == a || cell == b);
        bool hasKey = g.Keys.Contains(color);
        string name = KeyColors.Name(color);
        if (near && !wasNear)
            gm.ShowHint(hasKey ? $"[E] {Capital(name)}e Tür aufschließen" : $"Verschlossen – du brauchst den {name}en Schlüssel.", 3f);
        wasNear = near;
        if (near && hasKey && GameInput.InteractPressed) gm.UnlockDoor(color);
    }

    static string Capital(string s) => s.Length > 0 ? char.ToUpper(s[0]) + s[1..] : s;
}

// Pressure plate: a box on it keeps its gate open; so does standing on it yourself, but only while you stand there
// (plus a moment to step through a gate right beside it). Same as the solver: the plate counts as held while the
// player's cell is the plate.
public class PressurePlate : MonoBehaviour
{
    public Vector2Int cell;
    public SpriteRenderer art; // themed look: rune dark (up) / lit (down)
    public Sprite up, down;
    const float Grace = 0.25f;

    float heldUntil;
    SpriteRenderer sr;

    void Awake() => sr = GetComponent<SpriteRenderer>();

    void Update()
    {
        var gm = GameManager.Instance;
        var g = gm.Grid;
        if (g == null) return;
        if (gm.PlayerCell == cell) heldUntil = Time.time + Grace;
        if (Time.time < heldUntil) g.HeldPlates.Add(cell);
        else g.HeldPlates.Remove(cell);
        bool boxOn = g.Boxes.Contains(cell);
        sr.color = boxOn || Time.time < heldUntil ? SpriteFactory.Hex("#63e6be") : SpriteFactory.Hex("#0ca678");
        bool pressed = boxOn || Time.time < heldUntil;
        if (art) art.sprite = pressed ? down : up;
        if (pressed && !wasPressed) Sound.Play("Plate", 0.8f);
        wasPressed = pressed;
    }

    bool wasPressed;
}

// Lever: E while standing on it flips switch walls A ↔ B.
public class Lever : MonoBehaviour
{
    public Vector2Int cell;
    public SpriteRenderer art; // themed look: thrown to one side or the other
    public Sprite off, on;
    SpriteRenderer sr;

    void Awake() => sr = GetComponent<SpriteRenderer>();

    void Update()
    {
        var gm = GameManager.Instance;
        var g = gm.Grid;
        if (g == null) return;
        sr.color = SpriteFactory.Hex(g.Lever == 0 ? "#7048e8" : "#be4bdb");
        if (art) art.sprite = g.Lever == 0 ? off : on;
        if (!gm.CanAct || gm.PlayerCell != cell) return;
        if (GameInput.InteractPressed)
        {
            g.ToggleLever();
            Sound.Play("Lever");
        }
    }
}

// A rune stone. Dim until it is done in the current run of the order; its numeral (I, II, …) only shows while the
// whole maze is lit: the level-start overview and lightning flashes.
public class RuneStone : MonoBehaviour
{
    public Vector2Int cell;
    public int order;
    public SpriteRenderer stone, numeral;
    public Sprite dim, lit;

    static float wrongUntil;

    public static void FlashWrong() => wrongUntil = Time.time + 0.6f;

    void Update()
    {
        var g = GameManager.Instance.Grid;
        if (g == null) return;
        bool done = g.RunesDone || g.RuneProgress > order;
        stone.sprite = done ? lit : dim;
        stone.color = Time.time < wrongUntil ? new Color(1f, 0.35f, 0.3f) : Color.white;
        var atmo = Atmosphere.Current;
        var c = numeral.color;
        c.a = atmo ? atmo.Revealed : 0f;
        numeral.color = c;
    }
}
