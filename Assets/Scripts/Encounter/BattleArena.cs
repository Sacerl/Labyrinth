using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The fight box. Lives far away from the maze; the camera jumps here during an encounter.
public class BattleArena : MonoBehaviour
{
    public static readonly Vector2 Origin = new(1000f, 1000f);

    [SerializeField] float soulSpeed = 5f;
    [SerializeField] float soulRadius = 0.18f;
    [SerializeField] float invulnerableTime = 1f;
    [SerializeField] float waveDuration = 3.3f;
    [SerializeField] float shieldTime = 0.6f;

    public Vector2 Half { get; } = new(4f, 2.5f);
    public Vector2 SoulLocal => (Vector2)soul.transform.position - Origin;
    public bool Running { get; private set; }
    public bool ShieldReady => hasShield && shieldCharge;

    SpriteRenderer soul, soulHalo, shieldRing, background, boxGlow, enemyView;
    readonly SpriteRenderer[] borders = new SpriteRenderer[4];
    readonly SpriteRenderer[] motes = new SpriteRenderer[14];
    FightLook look = new();
    Sprite[] enemyFrames;
    Color soulColor = new(1f, 0.85f, 0.55f);
    bool hasShield, shieldCharge;
    float shieldUntil;
    readonly List<Bullet> bullets = new();
    Coroutine routine;
    float invulnerableUntil;
    Action onHit;

    public static BattleArena Create()
    {
        var arena = new GameObject("Battle Arena").AddComponent<BattleArena>();
        arena.transform.position = Origin;
        arena.Build();
        return arena;
    }

    void Build()
    {
        var t = transform;
        background = SpriteFactory.Create("Background", t, SpriteFactory.Square, Origin, new Vector2(30f, 18f), Color.black, 8);
        boxGlow = SpriteFactory.Create("Box glow", t, SpriteFactory.Glow, Origin, new Vector2(15f, 10f), Color.clear, 9);
        const float b = 0.12f;
        Vector2 h = Half;
        borders[0] = SpriteFactory.Create("Border", t, SpriteFactory.Square, Origin + new Vector2(0, h.y + b), new Vector2(2 * h.x + 4 * b, b * 2), Color.white, 11);
        borders[1] = SpriteFactory.Create("Border", t, SpriteFactory.Square, Origin - new Vector2(0, h.y + b), new Vector2(2 * h.x + 4 * b, b * 2), Color.white, 11);
        borders[2] = SpriteFactory.Create("Border", t, SpriteFactory.Square, Origin + new Vector2(h.x + b, 0), new Vector2(b * 2, 2 * h.y + 4 * b), Color.white, 11);
        borders[3] = SpriteFactory.Create("Border", t, SpriteFactory.Square, Origin - new Vector2(h.x + b, 0), new Vector2(b * 2, 2 * h.y + 4 * b), Color.white, 11);
        for (int i = 0; i < motes.Length; i++)
            motes[i] = SpriteFactory.Create("Mote", t, SpriteFactory.Glow, Origin, Vector2.one * 0.35f, Color.clear, 10);
        // the enemy you ran into watches from beside the box
        enemyView = SpriteFactory.Create("Enemy", t, SpriteFactory.Square, Origin + new Vector2(-(h.x + 1.75f), 0.4f),
            Vector2.one * 2.5f, Color.white, 11);
        enemyView.enabled = false;
        // your soul is the lantern's light: a bright core in a soft halo
        soul = SpriteFactory.Create("Soul", t, SpriteFactory.Circle, Origin, Vector2.one * 0.42f, soulColor, 13);
        soul.enabled = false;
        soulHalo = SpriteFactory.Create("Halo", soul.transform, SpriteFactory.Glow, Origin, Vector2.one * 1.6f / 0.42f,
            soulColor, 12);
        soulHalo.transform.localScale = Vector3.one * (1.6f / 0.42f);
        soulHalo.transform.localPosition = Vector3.zero;
        shieldRing = SpriteFactory.Create("Shield", t, SpriteFactory.Circle, Origin, Vector2.one * 1.1f,
            new Color(0.45f, 0.75f, 0.99f, 0.45f), 12);
        shieldRing.enabled = false;
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true)) sr.sharedMaterial = SpriteFactory.Unlit;
    }

    // look: the level's colours and bullets; enemy: walk frames of the monster you touched (null = none shown);
    // soulTint: the lantern colour of the level.
    public void Begin(float tier, int waves, bool shield, Action hit, Action<int, int> waveStarted, Action won,
        FightLook look = null, Sprite[][] enemy = null, Color? soulTint = null)
    {
        onHit = hit;
        hasShield = shield;
        shieldUntil = 0;
        Dress(look ?? new FightLook(), enemy, soulTint ?? new Color(1f, 0.85f, 0.55f));
        soul.transform.position = Origin + new Vector2(0, -Half.y * 0.5f);
        soul.enabled = true;
        soul.color = soulColor;
        Running = true;
        routine = StartCoroutine(Run(tier, waves, waveStarted, won));
    }

    public void Abort()
    {
        if (routine != null) StopCoroutine(routine);
        StopAllCoroutines();
        Finish();
    }

    void Dress(FightLook l, Sprite[][] enemy, Color soulTint)
    {
        look = l;
        background.color = l.background;
        boxGlow.color = new Color(l.glow.r, l.glow.g, l.glow.b, 0.34f);
        foreach (var b in borders) b.color = l.border;
        foreach (var m in motes) m.color = new Color(l.glow.r, l.glow.g, l.glow.b, 0.45f);
        soulColor = soulTint;
        soulHalo.color = new Color(soulTint.r, soulTint.g, soulTint.b, 0.55f);
        enemyFrames = enemy?[3]; // facing right, towards the box
        enemyView.enabled = enemyFrames != null;
    }

    void Finish()
    {
        Running = false;
        soul.enabled = false;
        enemyView.enabled = false;
        shieldRing.enabled = false;
        foreach (var b in bullets) Destroy(b.gameObject);
        bullets.Clear();
    }

    IEnumerator Run(float tier, int waves, Action<int, int> waveStarted, Action won)
    {
        yield return new WaitForSeconds(0.8f);
        var order = new List<AttackPatterns.Pattern>(AttackPatterns.All);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }
        for (int w = 0; w < waves; w++)
        {
            shieldCharge = hasShield;
            waveStarted?.Invoke(w + 1, waves);
            var pattern = tier < 0.5f ? AttackPatterns.Rain : order[w % order.Count];
            yield return StartCoroutine(pattern(this, tier, waveDuration));
            yield return new WaitForSeconds(0.6f);
        }
        Finish();
        won?.Invoke();
    }

    public Bullet Spawn(Vector2 local, Vector2 velocity, float size = 0.35f, bool armed = true)
    {
        var sr = SpriteFactory.Create("Bullet", transform, SpriteFactory.Circle, Origin + local,
            Vector2.one * size, Color.white, 12);
        sr.sharedMaterial = SpriteFactory.Unlit;
        var b = sr.gameObject.AddComponent<Bullet>();
        if (look.bullet != null && look.bullet.Length > 0)
        {
            // themed look; the hitbox stays the circle of diameter `size`
            var frame = look.bullet[0];
            sr.sprite = frame;
            float longest = Mathf.Max(frame.bounds.size.x, frame.bounds.size.y);
            sr.transform.localScale = Vector3.one * (size * look.bulletSize / longest);
            var anim = sr.gameObject.AddComponent<FrameLoop>();
            anim.frames = look.bullet;
            anim.fps = 10f;
            anim.phase = UnityEngine.Random.value * look.bullet.Length;
            b.orient = look.bulletOrient;
            b.spin = look.bulletSpin * (UnityEngine.Random.value < 0.5f ? 1f : -1f);
            sr.transform.rotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 360f));
            b.SetColor(look.bulletTint);
        }
        b.velocity = velocity;
        b.Face();
        b.radius = size / 2f;
        if (!armed) b.Warn();
        bullets.Add(b);
        return b;
    }

    void Update()
    {
        // background life: motes drifting up, the enemy bobbing
        for (int i = 0; i < motes.Length; i++)
        {
            float t = Time.time * 0.15f + i * 0.71f;
            motes[i].transform.position = Origin + new Vector2(Mathf.Sin(i * 12.9f) * 7.5f + Mathf.Sin(t * 2f + i) * 0.4f,
                Mathf.Repeat(t + i * 0.37f, 1f) * 10f - 5f);
        }
        if (enemyView.enabled)
        {
            enemyView.sprite = enemyFrames[(int)(Time.time * 6f) % enemyFrames.Length];
            enemyView.transform.position = Origin + new Vector2(-(Half.x + 1.75f), 0.4f + Mathf.Sin(Time.time * 2.5f) * 0.12f);
        }
        if (!Running) return;

        Vector2 h = Half, input = Vector2.ClampMagnitude(GameInput.MoveValue, 1f);
        Vector2 p = SoulLocal + input * (soulSpeed * Time.deltaTime);
        p.x = Mathf.Clamp(p.x, -h.x + soulRadius, h.x - soulRadius);
        p.y = Mathf.Clamp(p.y, -h.y + soulRadius, h.y - soulRadius);
        soul.transform.position = Origin + p;

        if (ShieldReady && GameInput.ShieldPressed)
        {
            shieldCharge = false;
            shieldUntil = Time.time + shieldTime;
            Sound.Play("Shield", 0.8f);
        }
        bool shielded = Time.time < shieldUntil;
        shieldRing.enabled = shielded;
        shieldRing.transform.position = soul.transform.position;

        bool invulnerable = Time.time < invulnerableUntil;
        bool blink = invulnerable && Mathf.Repeat(Time.time * 10f, 1f) < 0.5f;
        soul.color = blink ? new Color(1f, 0.2f, 0.15f, 0.35f) : soulColor; // hit: flickers red
        soulHalo.enabled = !blink;

        for (int i = bullets.Count - 1; i >= 0; i--)
        {
            var b = bullets[i];
            b.Step(Time.deltaTime);
            Vector2 bp = (Vector2)b.transform.position - Origin;
            if (Mathf.Abs(bp.x) > h.x || Mathf.Abs(bp.y) > h.y)
            {
                Destroy(b.gameObject);
                bullets.RemoveAt(i);
                continue;
            }
            float r = soulRadius + b.radius;
            if (shielded && b.Armed && (bp - p).sqrMagnitude < (r + 0.35f) * (r + 0.35f))
            {
                Destroy(b.gameObject); // blocked by the shield
                bullets.RemoveAt(i);
                continue;
            }
            if (!invulnerable && b.Armed && (bp - p).sqrMagnitude < r * r)
            {
                invulnerableUntil = Time.time + invulnerableTime;
                invulnerable = true;
                onHit?.Invoke();
                if (!Running) return; // the hit was fatal and the fight was aborted
            }
        }
    }
}
