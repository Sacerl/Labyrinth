using UnityEngine;

// Walk animation for a character visual: picks the direction from how `mover` moved since the last frame.
// Standing still shows the first frame of the last direction (unless `alwaysAnimate`, e.g. a wobbling slime).
public class SpriteAnimator : MonoBehaviour
{
    public Sprite[][] walk; // [direction: down, up, left, right][frame]
    public Transform mover;
    public float fps = 8f;
    public bool alwaysAnimate;

    SpriteRenderer sr;
    Vector3 last;
    int dir;
    float t;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (!mover) mover = transform;
        last = mover.position;
    }

    void LateUpdate()
    {
        if (walk == null) return;
        Vector2 d = mover.position - last;
        last = mover.position;
        bool moving = d.sqrMagnitude > 1e-6f;
        if (moving)
            dir = Mathf.Abs(d.x) > Mathf.Abs(d.y) ? (d.x < 0 ? 2 : 3) : (d.y > 0 ? 1 : 0);
        t = moving || alwaysAnimate ? t + Time.deltaTime * fps : 0f;
        var frames = walk[dir];
        sr.sprite = frames[(int)t % frames.Length];
    }
}

// Loops through frames at a fixed rate (effects such as the goal's magic circle).
public class FrameLoop : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 8f, phase;
    SpriteRenderer sr;

    void Start() => sr = GetComponent<SpriteRenderer>();

    void Update()
    {
        if (frames != null && frames.Length > 0) sr.sprite = frames[(int)(Time.time * fps + phase) % frames.Length];
    }
}

// Keeps a moving visual's draw order in step with its height: lower on screen = drawn in front.
public class YSort : MonoBehaviour
{
    public int offset;
    SpriteRenderer sr;

    void Awake() => sr = GetComponent<SpriteRenderer>();
    void LateUpdate() => sr.sortingOrder = LevelArt.OrderAt(transform.position.y) + offset;
}
