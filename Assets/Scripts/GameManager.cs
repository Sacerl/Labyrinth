using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

// Runs the game loop: menu → level intro → explore/puzzles/fights → goal → next level.
// 0 HP restarts the current level; R restarts it by choice (e.g. a box got stuck).
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] int maxHp = 5;
    [SerializeField] bool showMenu = true;
    [SerializeField] int startLevel = 0; // used when the menu is off
    [SerializeField] float introHold = 2f, introZoom = 1.2f;

    enum State { Menu, Intro, Exploring, Encounter, Transition, Won }

    LevelSet levels;
    BuiltLevel current;
    LevelTheme theme;
    int levelIndex, hp, hpAtStart;
    bool bonusHp, bonusAtStart; // the ♥'s extra HP above the maximum: the next hit takes it instead of a real HP
    State state;
    CameraFollow cam;
    BattleArena arena;
    Hud hud;
    LevelMenu menu;
    CharacterSelect characters;
    bool characterChosen; // asked once per session, C in the level menu asks again
    HashSet<string> abilities = new(), abilitiesAtStart = new();
    Vector2Int lastPlayerCell;
    static readonly HashSet<string> hintsShown = new(); // mechanic hints: once per session

    // Test hook (Autopilot): fights never cost HP.
    public static bool DebugNoDamage;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        DebugNoDamage = false;
        hintsShown.Clear();
    }

    public PuzzleGrid Grid => current?.grid;
    public Vector2 PlayerPosition => current != null ? (Vector2)current.player.transform.position : Vector2.zero;
    public string LevelName => current != null ? Grid.Level.name : "";
    public Dictionary<Vector2Int, Box> Boxes => current?.boxes;
    public bool CanAct => state == State.Exploring;
    public Vector2Int PlayerCell => current != null
        ? Grid.Level.WorldToCell(current.player.transform.position)
        : new Vector2Int(-1, -1);

    void Awake()
    {
        Instance = this;
        levels = LevelSet.Load();

        if (!FindAnyObjectByType<Light2D>())
            new GameObject("Global Light 2D").AddComponent<Light2D>().lightType = Light2D.LightType.Global;

        var camera = Camera.main;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = SpriteFactory.Hex("#05060a");
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true; // bloom, vignette, chaos effects
        cam = camera.GetComponent<CameraFollow>();
        if (!cam) cam = camera.gameObject.AddComponent<CameraFollow>();

        Sound.Create();
        hud = Hud.Create(maxHp);
        arena = BattleArena.Create();
        menu = LevelMenu.Create(levels, StartAt, ShowCharacters);
        characters = CharacterSelect.Create(() =>
        {
            characterChosen = true;
            characters.Hide();
            menu.Show();
        });
    }

    void Start()
    {
        if (showMenu) ShowMenu();
        else StartAt(startLevel);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (state != State.Menu && kb != null && kb.escapeKey.wasPressedThisFrame) ShowMenu();
        else if (state == State.Exploring && GameInput.RestartPressed) Restart();
        if (state == State.Exploring && PlayerCell != lastPlayerCell)
        {
            lastPlayerCell = PlayerCell;
            EnterCell(lastPlayerCell);
        }
    }

    // Rune stones: step on them in the order shown at the level start.
    void EnterCell(Vector2Int cell)
    {
        if (!Grid.Runes.ContainsKey(cell)) return;
        ShowOnce("runes", "Runen: Betritt sie in der Reihenfolge, die beim Levelstart zu sehen war (I → II → III …).");
        switch (Grid.StepRune(cell))
        {
            case PuzzleGrid.RuneStep.Advanced:
                Sound.Play("Accept", 0.8f);
                break;
            case PuzzleGrid.RuneStep.Done:
                Sound.Play("PowerUp");
                break;
            case PuzzleGrid.RuneStep.Reset:
                Sound.Play("Hurt", 0.5f);
                RuneStone.FlashWrong();
                break;
        }
    }

    public void ShowOnce(string key, string text)
    {
        if (hintsShown.Add(key)) hud.ShowHint(text, 7f);
    }

    public void OnSlide() => ShowOnce("ice", "Eis! Du rutschst, bis dich etwas aufhält – Kisten rutschen genauso.");

    public bool EnemyAt(Vector2Int cell)
    {
        if (current == null) return false;
        foreach (var e in current.enemies)
            if (e && Grid.Level.WorldToCell(e.transform.position) == cell) return true;
        return false;
    }

    public void StartAt(int index)
    {
        StopAllCoroutines();
        menu.Hide();
        characters.Hide();
        index = Mathf.Clamp(index, 0, levels.levels.Length - 1);
        hp = maxHp;
        bonusHp = false;
        abilities = new HashSet<string>(levels.levels[index].startAbilities ?? new string[0]);
        LoadLevel(index);
    }

    void ShowMenu()
    {
        StopAllCoroutines();
        arena.Abort();
        if (current != null) Destroy(current.root.gameObject);
        current = null;
        cam.Bounds = null;
        cam.Release();
        hud.SetLevel("");
        hud.SetStatus("");
        hud.ShowCenter("");
        hud.SetInventory(new string[0], new string[0]);
        state = State.Menu;
        Sound.Music("Menu");
        Sound.Ambient(null);
        if (characterChosen) menu.Show();
        else ShowCharacters();
    }

    void ShowCharacters()
    {
        menu.Hide();
        characters.Show();
    }

    void LoadLevel(int index)
    {
        StopAllCoroutines();
        if (current != null) Destroy(current.root.gameObject);
        levelIndex = index;
        var L = levels.levels[index];
        theme = LevelTheme.For(index);
        current = MazeBuilder.Build(L, theme);
        Sound.Music(theme.music ?? "Menu");
        Sound.Ambient(theme.ambientLoop, theme.ambientPitch, theme.ambientOneShot);
        abilities.UnionWith(L.startAbilities ?? new string[0]);
        hpAtStart = hp;
        bonusAtStart = bonusHp;
        abilitiesAtStart = new HashSet<string>(abilities);

        lastPlayerCell = L.start.V;
        cam.Target = current.player.transform;
        cam.Bounds = L.Bounds;
        hud.SetLevel(L.name);
        hud.SetHp(hp, bonusHp);
        hud.SetStatus("");
        UpdateInventory();
        StartCoroutine(Intro(L));
    }

    // The only moment the whole maze is visible: hold, then zoom onto the player.
    IEnumerator Intro(LevelData L)
    {
        state = State.Intro;
        SetFrozen(true);
        float hold = L.reveal > 0f ? L.reveal : introHold; // longer with runes to memorise
        if (Atmosphere.Current) Atmosphere.Current.StartCoroutine(Atmosphere.Current.Reveal(hold, introZoom));
        yield return cam.StartCoroutine(cam.Intro(hold, introZoom));
        SetFrozen(false);
        state = State.Exploring;
    }

    void Restart()
    {
        hp = hpAtStart;
        bonusHp = bonusAtStart;
        abilities = new HashSet<string>(abilitiesAtStart);
        LoadLevel(levelIndex);
    }

    void SetFrozen(bool frozen)
    {
        current.player.Frozen = frozen;
        current.enemies.RemoveAll(e => !e);
        foreach (var e in current.enemies) e.Frozen = frozen;
    }

    public bool HasAbility(string kind) => abilities.Contains(kind);

    public void AddAbility(string kind)
    {
        abilities.Add(kind);
        UpdateInventory();
        Sound.Play("PowerUp");
        hud.ShowHint(kind == "pull"
            ? "Ziehen: E halten und von der Kiste weggehen – sie kommt mit."
            : "Schild: im Kampf Leertaste drücken – blockt einen Treffer pro Welle.", 7f);
    }

    public void AddKey(string color)
    {
        Grid.Keys.Add(color);
        Sound.Play("Key");
        UpdateInventory();
    }

    public void UnlockDoor(string color)
    {
        if (!Grid.Unlock(color)) return;
        Sound.Play("Lever");
        Sound.Play("Key", 0.6f);
        hud.ShowHint("");
        UpdateInventory();
    }

    void UpdateInventory() => hud.SetInventory(Grid != null ? Grid.Keys : new HashSet<string>(), abilities);

    // A box moved one cell, or slid several on ice. A guard in the cell it moves into is shoved one cell further.
    public void MoveBox(Vector2Int from, Vector2Int to, float duration, Collider2D ignore)
    {
        var box = current.boxes[from];
        current.boxes.Remove(from);
        current.boxes[to] = box;
        int cells = Mathf.Abs(to.x - from.x) + Mathf.Abs(to.y - from.y);
        if (cells > 1) duration = cells * LevelData.Cell / PlayerController.SlideSpeed;
        var dir = new Vector2Int(System.Math.Sign(to.x - from.x), System.Math.Sign(to.y - from.y));
        foreach (var e in current.enemies)
            if (e && Grid.Level.WorldToCell(e.transform.position) == from + dir)
                e.Shove(Grid.Level.CellCenter(from + dir * 2));
        box.SlideTo(to, Grid.Level.CellCenter(to), duration, ignore);
    }

    public void StartEncounter(Enemy enemy)
    {
        if (state != State.Exploring) return;
        state = State.Encounter;
        SetFrozen(true);
        cam.Focus(BattleArena.Origin);

        var L = levels.levels[levelIndex];
        float tier = L.fightTier + (enemy.Forced ? 0.3f : 0f);
        int waves = L.waves > 0 ? L.waves : 3;
        bool shield = HasAbility("shield");
        string sheet = enemy.Forced ? theme.forcedSheet : theme.patrolSheet;
        Sound.Play("FightStart");
        Sound.FightMusic(theme.fight.music);
        arena.Begin(tier, waves, shield, OnHit,
            (w, n) => hud.SetStatus($"Welle {w}/{n}" + (shield ? "  ·  Schild: Leertaste" : "")),
            () => EndEncounter(enemy), theme.fight, sheet != null ? ArtLib.Walk(sheet) : null, theme.playerLight);
    }

    void OnHit()
    {
        if (state != State.Encounter || DebugNoDamage) return;
        Sound.Play("Hurt");
        if (bonusHp) bonusHp = false; // the extra HP goes first
        else hp--;
        hud.SetHp(hp, bonusHp);
        if (hp > 0) return;
        arena.Abort();
        StartCoroutine(GameOver());
    }

    void EndEncounter(Enemy enemy)
    {
        if (enemy) Destroy(enemy.gameObject);
        Sound.EndFight();
        Sound.Play("FightWon", 0.7f);
        if (hp < maxHp) Heal(1); // every defeated enemy gives back one HP
        hud.SetStatus("");
        cam.Release();
        SetFrozen(false);
        state = State.Exploring;
    }

    IEnumerator GameOver()
    {
        state = State.Transition;
        hud.SetStatus("");
        Sound.EndFight();
        Sound.Play("GameOver");
        hud.ShowCenter("Keine HP mehr!\nDas Labyrinth startet neu.", 2.5f);
        yield return new WaitForSeconds(2.5f);
        hp = maxHp;
        bonusHp = false;
        abilities = new HashSet<string>(abilitiesAtStart);
        LoadLevel(levelIndex);
    }

    public void Heal(int amount)
    {
        hp = Mathf.Min(maxHp, hp + amount);
        Sound.Play("Heal");
        hud.SetHp(hp, bonusHp);
    }

    // The ♥ in the maze: full HP plus one extra, which the next hit takes away.
    public void CollectHeart()
    {
        hp = maxHp;
        bonusHp = true;
        Sound.Play("Heal");
        hud.SetHp(hp, bonusHp);
    }

    public void ShowHint(string text, float seconds = 6f) => hud.ShowHint(text, seconds);

    public void ReachGoal()
    {
        if (state != State.Exploring) return;
        StartCoroutine(NextLevel());
    }

    IEnumerator NextLevel()
    {
        SetFrozen(true);
        if (levelIndex + 1 >= levels.levels.Length)
        {
            state = State.Won;
            Sound.StopMusic();
            Sound.Play("Victory");
            hud.ShowCenter("Geschafft!\nAlle Labyrinthe gemeistert.");
            yield return new WaitForSeconds(3f);
            ShowMenu();
            yield break;
        }
        state = State.Transition;
        Sound.Play("LevelDone", 0.8f);
        yield return new WaitForSeconds(0.6f);
        LoadLevel(levelIndex + 1);
    }
}
