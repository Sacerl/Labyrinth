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
        // a box in the way blocks the patrol: turn around
        var grid = GameManager.Instance.Grid;
        Vector2 ahead = (Vector2)transform.position + (target - (Vector2)transform.position).normalized * 1.6f;
        if (grid != null && grid.Boxes.Contains(grid.Level.WorldToCell(ahead)))
        {
            forward = !forward;
            target = forward ? to : from;
        }
        transform.position = Vector2.MoveTowards(transform.position, target, speed * Time.deltaTime);
        if ((Vector2)transform.position == target) forward = !forward;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>()) GameManager.Instance.StartEncounter(this);
    }
}
