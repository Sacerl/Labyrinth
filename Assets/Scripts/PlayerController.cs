using UnityEngine;

// Free top-down movement; pushes boxes by walking into them and pulls them (Pull ability) by holding E.
// On ice the movement is grid-based: you slide cell by cell until something is in the way (PuzzleGrid.Walk).
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public const float SlideSpeed = 9f;

    [SerializeField] float speed = 6f;
    [SerializeField] float pushDelay = 0.15f;
    [SerializeField] float pushSlide = 0.18f;

    Rigidbody2D rb;
    Collider2D col;
    float pushTimer;
    bool sliding;
    Vector2 slideTarget;
    Vector2Int lastCell;

    public bool Frozen { get; set; }
    public bool Sliding => sliding;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void Start()
    {
        var g = GameManager.Instance.Grid;
        if (g != null) lastCell = g.Level.WorldToCell(rb.position);
    }

    void FixedUpdate()
    {
        var gm = GameManager.Instance;
        if (sliding)
        {
            SlideStep(gm);
            return;
        }
        Vector2 input = Frozen ? Vector2.zero : Vector2.ClampMagnitude(GameInput.MoveValue, 1f);
        if (gm.Grid != null && !Frozen && OnIce(gm, input)) return;
        rb.linearVelocity = input * speed;
        if (Frozen) return;

        if (gm.Grid == null || input.sqrMagnitude < 0.25f)
        {
            pushTimer = 0;
            return;
        }
        var dir = Dominant(input);
        if (!TryPull(gm, dir)) TryPush(gm, dir);
    }

    // dominant axis → grid direction (grid y grows downwards)
    static Vector2Int Dominant(Vector2 v) => Mathf.Abs(v.x) > Mathf.Abs(v.y)
        ? new Vector2Int(v.x > 0 ? 1 : -1, 0)
        : new Vector2Int(0, v.y > 0 ? -1 : 1);

    // Ice: walking onto it starts a slide; resting on it, a direction slides you on (pushing a box first).
    // Returns true while the ice is in charge of the movement.
    bool OnIce(GameManager gm, Vector2 input)
    {
        var g = gm.Grid;
        var cell = g.Level.WorldToCell(rb.position);
        var from = lastCell;
        lastCell = cell;
        if (!g.Ice.Contains(cell)) return false;
        var step = cell - from;
        if (cell != from && !g.Ice.Contains(from))
        {
            // just came onto the ice: slide on in the direction you came from
            var dir = Mathf.Abs(step.x) + Mathf.Abs(step.y) == 1 ? step : Dominant(rb.linearVelocity);
            StartSlide(gm, cell - dir, dir);
            return true;
        }
        rb.linearVelocity = Vector2.zero;
        if (input.sqrMagnitude < 0.25f) return true;
        var d = Dominant(input);
        var next = cell + d;
        if (g.Boxes.Contains(next) && g.Passable(cell, next) && ShoveOk(gm, next, d) && g.TryPush(next, d, out var end))
        {
            gm.MoveBox(next, end, pushSlide, null);
            Sound.Play("Push", 0.5f, 0.12f);
        }
        if (g.CanEnter(cell, next)) StartSlide(gm, cell, d);
        return true;
    }

    void StartSlide(GameManager gm, Vector2Int from, Vector2Int dir)
    {
        var path = gm.Grid.Walk(from, dir);
        slideTarget = gm.Grid.Level.CellCenter(path[^1]);
        sliding = true;
        rb.linearVelocity = Vector2.zero;
        gm.OnSlide();
    }

    void SlideStep(GameManager gm)
    {
        var next = Vector2.MoveTowards(rb.position, slideTarget, SlideSpeed * Time.fixedDeltaTime);
        rb.linearVelocity = Vector2.zero;
        rb.MovePosition(next);
        if ((next - slideTarget).sqrMagnitude > 0.0001f) return;
        sliding = false;
        if (gm.Grid != null) lastCell = gm.Grid.Level.WorldToCell(slideTarget);
    }

    // A guard standing where the box would go is shoved one cell further, if there is room for it.
    static bool ShoveOk(GameManager gm, Vector2Int box, Vector2Int dir)
    {
        var to = box + dir;
        if (!gm.EnemyAt(to)) return true;
        var g = gm.Grid;
        return g.CanEnter(to, to + dir) && !gm.EnemyAt(to + dir);
    }

    void TryPush(GameManager gm, Vector2Int dir)
    {
        var L = gm.Grid.Level;
        var cell = L.WorldToCell(rb.position);
        var boxCell = cell + dir;
        if (!gm.Boxes.TryGetValue(boxCell, out var box) || box.Sliding)
        {
            pushTimer = 0;
            return;
        }
        // only when actually pressing against the box, roughly lined up with it
        Vector2 toBox = (Vector2)box.transform.position - rb.position;
        Vector2 axis = new(dir.x, -dir.y);
        float along = Vector2.Dot(toBox, axis), across = Mathf.Abs(Vector2.Dot(toBox, new Vector2(axis.y, axis.x)));
        if (along > 2.05f || across > 1.0f)
        {
            pushTimer = 0;
            return;
        }
        pushTimer += Time.fixedDeltaTime;
        if (pushTimer < pushDelay) return;
        pushTimer = 0;
        if (gm.Grid.Passable(cell, boxCell) && ShoveOk(gm, boxCell, dir) && gm.Grid.TryPush(boxCell, dir, out var end))
        {
            gm.MoveBox(boxCell, end, pushSlide, null);
            Sound.Play("Push", 0.5f, 0.12f);
        }
    }

    // Pull: holding E, walking away from a box right behind you drags it into your cell.
    bool TryPull(GameManager gm, Vector2Int dir)
    {
        if (!gm.HasAbility("pull") || !GameInput.InteractHeld) return false;
        var L = gm.Grid.Level;
        var cell = L.WorldToCell(rb.position);
        var boxCell = cell - dir;
        // a box still sliding from the previous pull may be pulled on, so holding E and walking drags it along
        if (!gm.Boxes.ContainsKey(boxCell)) return false;
        // the box must be right behind us: at most about one cell away, not at the far end of our cell
        // (measured from its cell, since a box that is still sliding trails behind)
        if (Vector2.Distance(L.CellCenter(boxCell), rb.position) > LevelData.Cell + 0.6f) return false;
        // only once we are past the middle of our cell, i.e. really leaving it (as in the solver): otherwise
        // walking into a cell would already drag the box into it, one cell further than planned
        if (Vector2.Dot(rb.position - L.CellCenter(cell), new Vector2(dir.x, -dir.y)) < 0.25f) return false;
        if (!gm.Grid.TryPull(cell, dir)) return false;
        gm.MoveBox(boxCell, cell, LevelData.Cell / speed, col);
        Sound.Play("Push", 0.5f, 0.12f);
        return true;
    }
}
