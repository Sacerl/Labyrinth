using UnityEngine;

public class Bullet : MonoBehaviour
{
    public Vector2 velocity;
    public float radius;
    public bool orient; // turn the sprite along the flight direction (thorns, spikes)
    public float spin;  // degrees per second (tumbling bones)

    // Unarmed bullets are a warning: they blink in place and can't hit until fired.
    public bool Armed { get; private set; } = true;

    SpriteRenderer sr;
    Color baseColor = Color.white;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        baseColor = sr.color;
    }

    public void SetColor(Color c)
    {
        baseColor = c;
        sr.color = c;
    }

    public void Warn() => Armed = false;

    public void Fire(Vector2 v)
    {
        velocity = v;
        Armed = true;
        sr.color = baseColor;
        Face();
    }

    public void Face()
    {
        if (orient && velocity.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
    }

    // Moved by BattleArena, which removes bullets at the box edge in the same step.
    public void Step(float dt)
    {
        if (spin != 0f) transform.Rotate(0f, 0f, spin * dt);
        if (Armed) transform.position += (Vector3)(velocity * dt);
        else sr.color = Mathf.Repeat(Time.time * 8f, 1f) < 0.5f ? baseColor : new Color(baseColor.r, baseColor.g, baseColor.b, 0.2f);
    }
}
