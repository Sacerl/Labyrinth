using System.Collections;
using UnityEngine;

// Undertale-style bullet patterns. `tier` grows with the level (0 = tutorial) and is +0.5 for must-fight enemies.
public static class AttackPatterns
{
    public delegate IEnumerator Pattern(BattleArena arena, float tier, float duration);

    public static readonly Pattern[] All = { Rain, Sweep, Aimed, Ring };

    // Bullets fall from the top edge; from tier 2 on they drift sideways.
    public static IEnumerator Rain(BattleArena arena, float tier, float duration)
    {
        Vector2 h = arena.Half;
        float interval = 0.35f / (1f + 0.35f * tier), speed = 3f + 0.8f * tier;
        for (float t = 0; t < duration; t += interval)
        {
            float drift = tier >= 2 ? Random.Range(-1f, 1f) : 0f;
            arena.Spawn(new Vector2(Random.Range(-h.x, h.x), h.y), new Vector2(drift, -speed));
            yield return new WaitForSeconds(interval);
        }
    }

    // A wall of bullets crosses the box from one side; it has a gap the soul must slip through.
    public static IEnumerator Sweep(BattleArena arena, float tier, float duration)
    {
        Vector2 h = arena.Half;
        float period = Mathf.Max(0.7f, 1.4f - 0.15f * tier), speed = 3f + 0.6f * tier;
        const float spacing = 0.55f;
        int count = Mathf.FloorToInt(2f * h.y / spacing) + 1;
        for (float t = 0; t < duration; t += period)
        {
            bool fromLeft = Random.value < 0.5f;
            int gap = Random.Range(1, count - 2);
            for (int i = 0; i < count; i++)
            {
                if (i == gap || i == gap + 1) continue;
                float y = -h.y + i * spacing;
                arena.Spawn(new Vector2(fromLeft ? -h.x : h.x, y), new Vector2(fromLeft ? speed : -speed, 0));
            }
            yield return new WaitForSeconds(period);
        }
    }

    // Shots from the box edge aimed at the soul; a three-way spread from tier 2.
    public static IEnumerator Aimed(BattleArena arena, float tier, float duration)
    {
        Vector2 h = arena.Half;
        float interval = Mathf.Max(0.4f, 0.7f - 0.06f * tier), speed = 3.5f + 0.5f * tier;
        float volleySpeed = 3f + 0.35f * tier;
        const float warning = 0.35f, spread = 22f;
        int shot = 0;
        for (float t = 0; t < duration; t += interval)
        {
            Vector2 from = Random.value < 0.5f
                ? new Vector2(Random.value < 0.5f ? -h.x : h.x, Random.Range(-h.y, h.y))
                : new Vector2(Random.Range(-h.x, h.x), Random.value < 0.5f ? -h.y : h.y);
            Vector2 dir = (arena.SoulLocal - from).normalized;
            if (tier >= 2 && shot++ % 2 == 1)
            {
                // Shotgun volley: shown blinking at the edge first, then fired at the soul's position from then.
                var volley = new[]
                {
                    arena.Spawn(from, Vector2.zero, armed: false),
                    arena.Spawn(from, Vector2.zero, armed: false),
                    arena.Spawn(from, Vector2.zero, armed: false),
                };
                yield return new WaitForSeconds(warning);
                t += warning;
                for (int i = 0; i < volley.Length; i++)
                    if (volley[i]) volley[i].Fire(Quaternion.Euler(0, 0, (i - 1) * spread) * dir * volleySpeed);
            }
            else
            {
                arena.Spawn(from, dir * speed);
            }
            yield return new WaitForSeconds(interval);
        }
    }

    // Radial bursts from a point near the top of the box.
    public static IEnumerator Ring(BattleArena arena, float tier, float duration)
    {
        Vector2 h = arena.Half;
        float period = Mathf.Max(0.6f, 1.2f - 0.12f * tier), speed = 2.5f + 0.5f * tier;
        int n = 8 + Mathf.RoundToInt(2 * tier);
        for (float t = 0; t < duration; t += period)
        {
            Vector2 c = new(Random.Range(-h.x * 0.7f, h.x * 0.7f), h.y * 0.8f);
            float offset = Random.Range(0f, 360f);
            for (int i = 0; i < n; i++)
            {
                float a = (offset + i * 360f / n) * Mathf.Deg2Rad;
                arena.Spawn(c, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed);
            }
            yield return new WaitForSeconds(period);
        }
    }
}
