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
public class EdgeBar : MonoBehaviour
{
    public enum Kind { Door, Gate, SwitchA, SwitchB }

    public Kind kind;
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
        _ => g.SwitchClosed(false),
    };

    void Update()
    {
        var g = GameManager.Instance.Grid;
        if (g == null) return;
        bool closed = Closed(g);
        col.enabled = closed;
        sr.color = closed ? baseColor : new Color(baseColor.r, baseColor.g, baseColor.b, 0.15f);
        if (artParts != null)
            for (int i = 0; i < artParts.Length; i++)
            {
                var c = artColors[i];
                artParts[i].color = closed ? c : new Color(c.r, c.g, c.b, 0.12f);
            }
    }
}

// Pressure plate: a box on it keeps its gate open; standing on it yourself opens the gate for a moment.
public class PressurePlate : MonoBehaviour
{
    public Vector2Int cell;
    public SpriteRenderer art; // themed look: rune dark (up) / lit (down)
    public Sprite up, down;
    const float HoldTime = 2f;

    float heldUntil;
    SpriteRenderer sr;

    void Awake() => sr = GetComponent<SpriteRenderer>();

    void OnTriggerStay2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>()) heldUntil = Time.time + HoldTime;
    }

    void Update()
    {
        var g = GameManager.Instance.Grid;
        if (g == null) return;
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
