using UnityEngine;

// Free top-down movement; pushes boxes by walking into them and pulls them (Pull ability) by holding E.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] float speed = 6f;
    [SerializeField] float pushDelay = 0.15f;
    [SerializeField] float pushSlide = 0.18f;

    Rigidbody2D rb;
    Collider2D col;
    float pushTimer;

    public bool Frozen { get; set; }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void FixedUpdate()
    {
        Vector2 input = Frozen ? Vector2.zero : Vector2.ClampMagnitude(GameInput.MoveValue, 1f);
        rb.linearVelocity = input * speed;
        if (Frozen) return;

        var gm = GameManager.Instance;
        if (gm.Grid == null || input.sqrMagnitude < 0.25f)
        {
            pushTimer = 0;
            return;
        }
        // dominant axis → grid direction (grid y grows downwards)
        Vector2Int dir = Mathf.Abs(input.x) > Mathf.Abs(input.y)
            ? new Vector2Int(input.x > 0 ? 1 : -1, 0)
            : new Vector2Int(0, input.y > 0 ? -1 : 1);
        if (!TryPull(gm, dir)) TryPush(gm, dir);
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
        if (gm.Grid.TryPush(boxCell, dir))
        {
            gm.MoveBox(boxCell, boxCell + dir, pushSlide, null);
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
