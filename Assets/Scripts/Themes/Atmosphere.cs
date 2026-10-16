using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The mood of one level: darkness, the player's lantern, glowing pickups, fog, fireflies and screen effects.
// Lives on the level root, so it is rebuilt with every level and gone in the menu.
public class Atmosphere : MonoBehaviour
{
    public static Atmosphere Current { get; private set; }

    // 1 while the level-start overview shows the whole maze (fading out with the zoom), or during a lightning flash:
    // the moments the rune order can be seen.
    public float Revealed => Mathf.Max(reveal, flashUntil > 0f ? 1f : 0f);

    LevelTheme theme;
    Light2D global, lantern;
    VolumeProfile profile;
    Vignette vignette;
    float vignetteIntensity;
    float reveal; // 1 while the level-start overview shows the whole maze, fading to 0 with the zoom
    Transform player;

    public static Atmosphere Create(Transform root, LevelData L, LevelTheme theme, Transform player)
    {
        var a = new GameObject("Atmosphere").AddComponent<Atmosphere>();
        a.transform.SetParent(root, false);
        a.theme = theme;
        a.Build(L, player);
        Current = a;
        return a;
    }

    void Build(LevelData L, Transform player)
    {
        born = Time.time;
        nextLightning = born + 8f;
        this.player = player;
        foreach (var l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            if (l.lightType == Light2D.LightType.Global) { global = l; break; }
        SetAmbient(theme.ambient, theme.ambientIntensity);

        lantern = Light(player, "Lantern", theme.playerLight, theme.playerLightRadius, theme.playerLightIntensity);
        lantern.pointLightInnerRadius = 0.5f;
        lantern.falloffIntensity = 0.55f; // soft fade into the dark

        BuildVolume();
        var rnd = LevelArt.Random(L);
        if (theme.fogAlpha > 0f)
        {
            int banks = Mathf.RoundToInt(L.width * L.height / 10f * theme.fogPer10Cells);
            for (int i = 0; i < banks; i++)
            {
                var home = new Vector2((float)rnd.NextDouble() * L.width, (float)rnd.NextDouble() * L.height) * LevelData.Cell;
                Mist.Create(transform, home, theme.fogColor, theme.fogAlpha, 5f + (float)rnd.NextDouble() * 5f, i * 3.1f);
            }
        }
        int flies = Mathf.RoundToInt(L.width * L.height / 10f * theme.firefliesPer10Cells);
        for (int i = 0; i < flies; i++)
        {
            var home = new Vector2((float)rnd.NextDouble() * L.width, (float)rnd.NextDouble() * L.height) * LevelData.Cell;
            Firefly.Create(transform, home, theme.fireflyColor, i * 7.3f);
        }
    }

    void SetAmbient(Color c, float intensity)
    {
        if (!global) return;
        global.color = c;
        global.intensity = intensity;
    }

    public static Light2D Light(Transform parent, string name, Color color, float radius, float intensity)
    {
        var l = new GameObject(name).AddComponent<Light2D>();
        l.transform.SetParent(parent, false);
        l.lightType = Light2D.LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.pointLightOuterRadius = radius;
        l.pointLightInnerRadius = 0f;
        l.falloffIntensity = 0.6f;
        // keep the world-space radius when the parent is scaled (placeholder objects are)
        Vector3 ls = parent.lossyScale;
        l.transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f);
        return l;
    }

    // A small light that makes a pickup or the goal findable in the dark.
    public static void Glow(GameObject host, Color color, float radius, float intensity, float pulse = 0f)
    {
        var l = Light(host.transform, "Glow", color, radius, intensity);
        if (pulse > 0f) l.gameObject.AddComponent<Pulse>().Init(intensity, pulse);
    }

    void BuildVolume()
    {
        var v = gameObject.AddComponent<Volume>();
        v.isGlobal = true;
        v.priority = 10f;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        v.sharedProfile = profile;
        float c = theme.chaos;
        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(0.45f);
        bloom.threshold.Override(1f);
        bloom.scatter.Override(0.7f);
        vignette = profile.Add<Vignette>(true);
        vignetteIntensity = 0.32f + 0.12f * c;
        vignette.intensity.Override(vignetteIntensity);
        vignette.smoothness.Override(0.75f);
        vignette.color.Override(Color.black);
        var colors = profile.Add<ColorAdjustments>(true);
        colors.saturation.Override(-12f - 35f * c);
        colors.contrast.Override(8f + 12f * c);
        if (c > 0f)
        {
            profile.Add<ChromaticAberration>(true).intensity.Override(0.6f * c);
            var grain = profile.Add<FilmGrain>(true);
            grain.intensity.Override(0.5f * c);
            grain.type.Override(FilmGrainLookup.Medium3);
        }
    }

    float born, nextLightning, flashUntil, flashBase;

    void Update()
    {
        if (theme.lightning && global && Time.time - born > 4f) Lightning(); // not during the level-start reveal
        // the darkness frames the player, not the screen: at the maze edge the camera stops and you'd walk
        // into the dark screen corner otherwise
        var cam = Camera.main;
        if (vignette != null && player && cam)
        {
            Vector2 v = cam.WorldToViewportPoint(player.position);
            bool onScreen = v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
            // off screen = the camera is at the fight box: frame the box, not a far-away screen corner
            Vector2 center = onScreen ? new Vector2(Mathf.Clamp01(v.x), Mathf.Clamp01(v.y)) : new Vector2(0.5f, 0.5f);
            // the level-start overview shows the whole maze: no darkness around the (tiny, corner) player then
            vignette.center.Override(Vector2.Lerp(center, new Vector2(0.5f, 0.5f), reveal));
            vignette.intensity.Override(vignetteIntensity * (1f - reveal));
        }
        if (lantern && theme.flicker > 0f)
        {
            float n = Mathf.PerlinNoise(Time.time * 3f, 0.37f) - 0.5f;
            lantern.intensity = theme.playerLightIntensity * (1f + 2f * theme.flicker * n);
        }
    }

    // A double flash every 7–14 s: for a heartbeat the whole maze is visible again.
    void Lightning()
    {
        float t = Time.time;
        if (flashUntil == 0f && t > nextLightning)
        {
            flashBase = global.intensity;
            flashUntil = t + 0.32f;
            Sound.Play("Thunder", 0.5f, 0.2f);
            nextLightning = t + Random.Range(7f, 14f);
        }
        if (flashUntil == 0f) return;
        if (t > flashUntil)
        {
            global.intensity = flashBase;
            flashUntil = 0f;
            return;
        }
        float k = flashUntil - t; // two short bursts
        global.intensity = flashBase + (k > 0.22f || (k < 0.14f && k > 0.06f) ? 0.55f : 0.05f);
    }

    // Level start: moonlight shows the whole maze while the camera holds the overview, then the dark returns.
    public IEnumerator Reveal(float hold, float fade)
    {
        reveal = 1f;
        SetAmbient(theme.revealColor, theme.revealIntensity);
        yield return new WaitForSeconds(hold);
        for (float t = 0; t < fade; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / fade);
            reveal = 1f - k;
            SetAmbient(Color.Lerp(theme.revealColor, theme.ambient, k),
                Mathf.Lerp(theme.revealIntensity, theme.ambientIntensity, k));
            yield return null;
        }
        reveal = 0f;
        SetAmbient(theme.ambient, theme.ambientIntensity);
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
        if (profile) Destroy(profile);
    }
}

// A bank of mist: a big soft blot that drifts and breathes. Lit like the world, so it glows where the lantern
// shines into it and stays a faint shadow in the dark.
public class Mist : MonoBehaviour
{
    Vector2 home;
    float seed, alpha;
    SpriteRenderer sr;

    public static void Create(Transform parent, Vector2 home, Color color, float alpha, float size, float seed)
    {
        var sr = new GameObject("Mist").AddComponent<SpriteRenderer>();
        sr.transform.SetParent(parent, false);
        sr.sprite = SpriteFactory.Glow;
        sr.transform.localScale = new Vector3(size * 1.7f, size, 1f);
        sr.color = new Color(color.r, color.g, color.b, alpha);
        sr.sortingOrder = 1400;
        var m = sr.gameObject.AddComponent<Mist>();
        m.home = home;
        m.seed = seed;
        m.alpha = alpha;
        m.sr = sr;
        m.Update();
    }

    void Update()
    {
        float t = Time.time * 0.04f + seed;
        transform.position = home + new Vector2(Mathf.PerlinNoise(t, seed) - 0.5f, (Mathf.PerlinNoise(seed, t) - 0.5f) * 0.5f) * 10f;
        var c = sr.color;
        c.a = alpha * (0.6f + 0.8f * Mathf.PerlinNoise(t * 3f, seed + 7f));
        sr.color = c;
    }
}

// A restless flame: lamp posts and torches.
public class Flicker : MonoBehaviour
{
    float baseIntensity, seed;
    Light2D l;

    public void Init(float intensity, float noiseSeed)
    {
        baseIntensity = intensity;
        seed = noiseSeed;
        l = GetComponent<Light2D>();
    }

    void Update()
    {
        if (l) l.intensity = baseIntensity * (0.75f + 0.5f * Mathf.PerlinNoise(Time.time * 6f, seed));
    }
}

// Slowly breathing light intensity.
public class Pulse : MonoBehaviour
{
    float baseIntensity, amount;
    Light2D l;

    public void Init(float intensity, float pulse)
    {
        baseIntensity = intensity;
        amount = pulse;
        l = GetComponent<Light2D>();
    }

    void Update()
    {
        if (l) l.intensity = baseIntensity * (1f + amount * Mathf.Sin(Time.time * 2.2f));
    }
}

// A drifting point of light around its home spot.
public class Firefly : MonoBehaviour
{
    Vector2 home;
    float seed;
    Light2D l;

    public static void Create(Transform parent, Vector2 home, Color color, float seed)
    {
        var sr = new GameObject("Firefly").AddComponent<SpriteRenderer>();
        sr.transform.SetParent(parent, false);
        sr.sprite = SpriteFactory.Circle;
        sr.transform.localScale = Vector3.one * 0.14f;
        sr.color = color;
        sr.sharedMaterial = SpriteFactory.Unlit;
        sr.sortingOrder = 1500;
        var f = sr.gameObject.AddComponent<Firefly>();
        f.home = home;
        f.seed = seed;
        f.l = Atmosphere.Light(sr.transform, "Light", color, 1.6f, 0.9f);
        f.Update();
    }

    void Update()
    {
        float t = Time.time * 0.25f + seed;
        transform.position = home + new Vector2(Mathf.PerlinNoise(t, seed) - 0.5f, Mathf.PerlinNoise(seed, t) - 0.5f) * 5f;
        if (l) l.intensity = 0.5f + 0.5f * Mathf.PerlinNoise(t * 4f, seed + 3f);
    }
}
