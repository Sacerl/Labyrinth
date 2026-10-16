using UnityEngine;

// Patrols back and forth along a straight corridor; touching the player starts a fight.
public class Enemy : MonoBehaviour
{
    public bool Forced { get; private set; }
    public bool Frozen { get; set; }

    Vector2 from, to;
    float speed;
    bool forward = true;

    public void Init(Vector2 a, Vector2 b, bool forced, float patrolSpeed)
    {
        from = a;
        to = b;
        Forced = forced;
        speed = patrolSpeed;
        transform.position = Vector2.Lerp(a, b, 0.5f);
    }

    void Update()
    {
        if (Frozen || from == to) return;
        Vector2 target = forward ? to : from;
        // a box in the way blocks the patrol: turn around; boxed in on both sides (a guard shoved into its niche):
        // stay put
        if (BoxAhead(target))
        {
            forward = !forward;
            target = forward ? to : from;
            if (BoxAhead(target)) return;
        }
        transform.position = Vector2.MoveTowards(transform.position, target, speed * Time.deltaTime);
        if ((Vector2)transform.position == target) forward = !forward;
    }

    bool BoxAhead(Vector2 target)
    {
        var grid = GameManager.Instance.Grid;
        Vector2 ahead = (Vector2)transform.position + (target - (Vector2)transform.position).normalized * 1.6f;
        return grid != null && grid.Boxes.Contains(grid.Level.WorldToCell(ahead));
    }

    // A box pushed into this enemy's cell moves it one cell on. Outside its patrol it stays stuck behind the box
    // (both ends of its patrol lie on the other side): a guard shoved into its niche is out of the way for good.
    public void Shove(Vector2 to) => transform.position = to;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>()) GameManager.Instance.StartEncounter(this);
    }
}
