using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Which character carries the light. Remembered between sessions.
public static class PlayerChoice
{
    public static readonly (string sheet, string name)[] All =
    {
        ("SorcererBlack", "Hexer"),
        ("NinjaMageBlack", "Schattenmagier"),
        ("Shaman", "Schamane"),
        ("Spirit", "Irrlicht"),
        ("Vampire", "Vampir"),
    };

    public static int Index
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt("character", 1), 0, All.Length - 1);
        set
        {
            PlayerPrefs.SetInt("character", value);
            PlayerPrefs.Save();
        }
    }

    public static string Sheet => "Characters/" + All[Index].sheet;
}

// CharacterSelect scene, the first one: the five characters on a pentagon. ←/→ (or A/D) walk around it, Enter picks,
// or click one; then the LevelSelect scene loads. The Canvas is set up in the scene, the slots are built when it loads.
public class CharacterSelect : MonoBehaviour
{
    const float Radius = 290f;
    static readonly Vector2 Center = new(0, -60);

    int selected;
    RectTransform[] slots;
    Image[] frames;
    Text nameText;
    Image walker;
    Sprite[][] walk;

    static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    void Awake() => Build();

    void Start()
    {
        GameManager.Instance.EnterMenu();
        Select(PlayerChoice.Index);
    }

    void Build()
    {
        var bg = Rect("Background", transform, Vector2.zero, Vector2.zero);
        bg.anchorMin = Vector2.zero;
        bg.anchorMax = Vector2.one;
        bg.gameObject.AddComponent<Image>().color = SpriteFactory.Hex("#07060d");

        Label("Wer trägt das Licht?", new Vector2(0, 430), 76, SpriteFactory.Hex("#e9e3ff"));
        Label("←/→ wählen  ·  Enter bestätigen  ·  oder klicken", new Vector2(0, -490), 28, new Color(1, 1, 1, 0.45f));

        // lantern glow in the middle, the chosen character walking in it
        var glow = Rect("Glow", transform, Center, new Vector2(520, 520)).gameObject.AddComponent<Image>();
        glow.sprite = SpriteFactory.Glow;
        glow.color = new Color(1f, 0.72f, 0.38f, 0.55f);
        glow.raycastTarget = false;
        walker = Rect("Walker", transform, Center + new Vector2(0, 10), new Vector2(128, 128)).gameObject.AddComponent<Image>();
        nameText = Label("", Center + new Vector2(0, -110), 40, SpriteFactory.Hex("#ffd8a8"));

        int n = PlayerChoice.All.Length;
        slots = new RectTransform[n];
        frames = new Image[n];
        for (int i = 0; i < n; i++)
        {
            int index = i;
            float a = (90f - i * 360f / n) * Mathf.Deg2Rad; // first on top, then clockwise
            var slot = Rect($"Slot {PlayerChoice.All[i].name}", transform,
                Center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Radius, new Vector2(170, 170));
            frames[i] = slot.gameObject.AddComponent<Image>();
            frames[i].sprite = SpriteFactory.Circle;
            var button = slot.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => { Select(index); Confirm(); });
            // hovering previews the character, like the arrow keys
            var hover = slot.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => Select(index));
            hover.triggers.Add(enter);
            var face = Rect("Face", slot, Vector2.zero, new Vector2(114, 114)).gameObject.AddComponent<Image>();
            face.sprite = ArtLib.Whole($"Characters/{PlayerChoice.All[i].sheet}_Face");
            face.raycastTarget = false;
            slots[i] = slot;
        }
    }

    void Select(int i)
    {
        int n = slots.Length;
        int was = selected;
        selected = (i % n + n) % n;
        if (selected != was && gameObject.activeInHierarchy && walk != null) Sound.Play("Move", 0.6f);
        walk = ArtLib.Walk("Characters/" + PlayerChoice.All[selected].sheet);
        nameText.text = PlayerChoice.All[selected].name;
        for (int k = 0; k < n; k++)
        {
            bool on = k == selected;
            frames[k].color = on ? new Color(1f, 0.78f, 0.45f, 0.95f) : new Color(0.35f, 0.32f, 0.5f, 0.55f);
        }
    }

    void Confirm()
    {
        Sound.Play("Accept");
        PlayerChoice.Index = selected;
        SceneLoader.Instance.Load(SceneLoader.LevelSelect);
    }

    void Update()
    {
        if (walk != null) walker.sprite = walk[0][(int)(Time.unscaledTime * 6f) % walk[0].Length];
        float ease = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime); // the chosen slot grows in smoothly
        for (int k = 0; k < slots.Length; k++)
            slots[k].localScale = Vector3.Lerp(slots[k].localScale, Vector3.one * (k == selected ? 1.18f : 1f), ease);
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Select(selected + 1);
        else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Select(selected - 1);
        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame ||
                 kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) Confirm();
    }

    static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    Text Label(string s, Vector2 pos, int size, Color color)
    {
        var rt = Rect("Text", transform, pos, new Vector2(1600, size * 1.6f));
        var t = rt.gameObject.AddComponent<Text>();
        t.font = Font;
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.text = s;
        t.raycastTarget = false;
        return t;
    }
}
