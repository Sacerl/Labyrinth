using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Music, ambience and sound effects from Resources/Audio (Ninja Adventure pack, CC0).
// Two music layers: the world's track keeps playing quietly under the fight track, so a fight doesn't restart it.
// M mutes everything (remembered between sessions).
public class Sound : MonoBehaviour
{
    const float MusicVolume = 0.55f, AmbientVolume = 0.3f, FadeTime = 0.8f;

    static Sound instance;
    AudioSource world, fight, ambient;
    readonly AudioSource[] sfx = new AudioSource[6]; // a pool: each sound keeps its own pitch
    int nextSfx;
    string worldTrack, fightTrack;
    bool inFight;
    Coroutine fade;
    readonly Dictionary<string, AudioClip> clips = new();

    // occasional one-shots over the ambient loop (dripping water, fire crackle)
    string sporadic;
    float sporadicVolume, nextSporadic;

    public static bool Muted
    {
        get => PlayerPrefs.GetInt("muted", 0) == 1;
        set
        {
            PlayerPrefs.SetInt("muted", value ? 1 : 0);
            PlayerPrefs.Save();
            AudioListener.volume = value ? 0f : 1f;
        }
    }

    public static void Create()
    {
        if (instance) return;
        instance = new GameObject("Sound").AddComponent<Sound>();
        AudioListener.volume = Muted ? 0f : 1f;
    }

    void Awake()
    {
        world = Source(true);
        fight = Source(true);
        ambient = Source(true);
        for (int i = 0; i < sfx.Length; i++) sfx[i] = Source(false);
    }

    AudioSource Source(bool loop)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.loop = loop;
        s.playOnAwake = false;
        s.volume = 0f;
        return s;
    }

    AudioClip Clip(string path)
    {
        if (clips.TryGetValue(path, out var c) && c) return c;
        c = Resources.Load<AudioClip>("Audio/" + path);
        if (!c) Debug.LogWarning($"Sound: missing Audio/{path}");
        return clips[path] = c;
    }

    // The world's music (menu, a level). Same track: keeps playing.
    public static void Music(string track)
    {
        if (!instance || track == instance.worldTrack) return;
        instance.worldTrack = track;
        instance.inFight = false;
        instance.Crossfade(instance.world, track);
    }

    // A fight starts: its track fades in over the (quietened) world music.
    public static void FightMusic(string track)
    {
        if (!instance) return;
        instance.inFight = true;
        if (track != instance.fightTrack || !instance.fight.isPlaying)
        {
            instance.fightTrack = track;
            instance.fight.clip = instance.Clip("Music/" + track);
            instance.fight.time = 0f;
            instance.fight.Play();
        }
        instance.FadeLayers();
    }

    public static void EndFight()
    {
        if (!instance) return;
        instance.inFight = false;
        instance.FadeLayers();
    }

    public static void StopMusic()
    {
        if (!instance) return;
        instance.worldTrack = null;
        instance.inFight = false;
        instance.Crossfade(instance.world, null);
    }

    // A looping background sound for the level, plus an optional one-shot that turns up every few seconds.
    public static void Ambient(string loop, float pitch = 1f, string oneShot = null, float oneShotVolume = 0.25f)
    {
        if (!instance) return;
        var a = instance.ambient;
        var clip = loop != null ? instance.Clip("Ambient/" + loop) : null;
        if (a.clip != clip || !a.isPlaying)
        {
            a.clip = clip;
            if (clip) a.Play();
            else a.Stop();
        }
        a.pitch = pitch;
        a.volume = clip ? AmbientVolume : 0f;
        instance.sporadic = oneShot;
        instance.sporadicVolume = oneShotVolume;
        instance.nextSporadic = Time.time + 2f;
    }

    public static void Play(string name, float volume = 1f, float pitchJitter = 0.06f)
    {
        if (!instance) return;
        var clip = instance.Clip("Sfx/" + name);
        if (!clip) return;
        instance.OneShot(clip, volume, 1f + Random.Range(-pitchJitter, pitchJitter));
    }

    void OneShot(AudioClip clip, float volume, float pitch)
    {
        var s = sfx[nextSfx];
        nextSfx = (nextSfx + 1) % sfx.Length;
        s.pitch = pitch;
        s.volume = 1f;
        s.PlayOneShot(clip, volume);
    }

    void Crossfade(AudioSource layer, string track)
    {
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(SwapTrack(layer, track));
    }

    IEnumerator SwapTrack(AudioSource layer, string track)
    {
        for (float v = layer.volume; v > 0f; v -= Time.unscaledDeltaTime / FadeTime * MusicVolume)
        {
            layer.volume = v;
            fight.volume = Mathf.Min(fight.volume, v);
            yield return null;
        }
        layer.volume = 0f;
        fight.Stop();
        fightTrack = null;
        if (track == null)
        {
            layer.Stop();
            yield break;
        }
        layer.clip = Clip("Music/" + track);
        layer.time = 0f;
        layer.Play();
        for (float v = 0f; v < MusicVolume; v += Time.unscaledDeltaTime / FadeTime * MusicVolume)
        {
            layer.volume = v;
            yield return null;
        }
        layer.volume = MusicVolume;
        fade = null;
    }

    void FadeLayers()
    {
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(Layers());
    }

    IEnumerator Layers()
    {
        float worldTo = inFight ? MusicVolume * 0.15f : MusicVolume, fightTo = inFight ? MusicVolume : 0f;
        float w0 = world.volume, f0 = fight.volume;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / (FadeTime * 0.6f))
        {
            world.volume = Mathf.Lerp(w0, worldTo, t);
            fight.volume = Mathf.Lerp(f0, fightTo, t);
            yield return null;
        }
        world.volume = worldTo;
        fight.volume = fightTo;
        if (!inFight) fight.Stop();
        fade = null;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.mKey.wasPressedThisFrame) Muted = !Muted;
        if (sporadic != null && Time.time > nextSporadic)
        {
            nextSporadic = Time.time + Random.Range(2.5f, 7f);
            var clip = Clip("Ambient/" + sporadic);
            if (clip) OneShot(clip, sporadicVolume, Random.Range(0.85f, 1.1f));
        }
    }
}
