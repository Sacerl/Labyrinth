using System.Collections;
using UnityEngine;

// Follows the player with a small view (about 5×3 maze cells) and stops at the maze edge.
// Can also lock onto the fight arena, or show the whole maze and zoom in (level intro).
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] float smoothTime = 0.12f;
    [SerializeField] float viewSize = 4.5f;

    public Transform Target { get; set; }
    public Rect? Bounds { get; set; }

    Camera cam;
    Vector2? focus;
    Vector3 velocity;
    bool scripted; // the intro drives the camera

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = viewSize;
    }

    // Where the camera wants to be: the focus point, or the target clamped so the view stays inside the maze.
    Vector3 Goal(float size)
    {
        Vector2 p = focus ?? (Target ? (Vector2)Target.position : (Vector2)transform.position);
        if (!focus.HasValue && Bounds is Rect b)
        {
            float hw = size * cam.aspect, hh = size;
            p.x = b.width <= 2 * hw ? b.center.x : Mathf.Clamp(p.x, b.xMin + hw, b.xMax - hw);
            p.y = b.height <= 2 * hh ? b.center.y : Mathf.Clamp(p.y, b.yMin + hh, b.yMax - hh);
        }
        return new Vector3(p.x, p.y, -10f);
    }

    void LateUpdate()
    {
        if (scripted) return;
        transform.position = focus.HasValue
            ? Goal(viewSize)
            : Vector3.SmoothDamp(transform.position, Goal(viewSize), ref velocity, smoothTime);
    }

    public void Snap()
    {
        velocity = Vector3.zero;
        cam.orthographicSize = viewSize;
        transform.position = Goal(viewSize);
    }

    public void Focus(Vector2 point)
    {
        StopAllCoroutines();
        scripted = false;
        focus = point;
        Snap();
    }

    public void Release()
    {
        StopAllCoroutines();
        scripted = false;
        focus = null;
        Snap();
    }

    // Level intro: the whole maze for `hold` seconds, then a smooth zoom onto the target.
    public IEnumerator Intro(float hold, float zoom)
    {
        focus = null;
        scripted = true;
        Rect b = Bounds ?? new Rect(0, 0, 10, 10);
        float overview = Mathf.Max(b.height / 2f, b.width / 2f / cam.aspect) * 1.08f;
        Vector3 start = new(b.center.x, b.center.y, -10f);
        cam.orthographicSize = overview;
        transform.position = start;
        yield return new WaitForSeconds(hold);
        for (float t = 0; t < zoom; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / zoom);
            float size = Mathf.Lerp(overview, viewSize, k);
            cam.orthographicSize = size;
            transform.position = Vector3.Lerp(start, Goal(size), k);
            yield return null;
        }
        scripted = false;
        Snap();
    }
}
