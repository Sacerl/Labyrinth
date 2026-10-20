using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// LevelSelect scene: pick a level by click or number key 1–6; C goes back to the character select.
// The Canvas is set up in the scene, the buttons are built from levels.json when it loads.
public class LevelMenu : MonoBehaviour
{
    int count;

    static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    void Awake()
    {
        var levels = GameManager.Instance.Levels;
        count = levels.levels.Length;
        Build(levels);
    }

    void Start() => GameManager.Instance.EnterMenu();

    void Build(LevelSet levels)
    {
        var bg = Rect("Background", transform, Vector2.zero, Vector2.zero);
        bg.anchorMin = Vector2.zero;
        bg.anchorMax = Vector2.one;
        bg.gameObject.AddComponent<Image>().color = SpriteFactory.Hex("#212529");

        Label("Labyrinth", new Vector2(0, 300), 110, Color.white);
        Label("Level wählen", new Vector2(0, 190), 40, new Color(1, 1, 1, 0.6f));

        for (int i = 0; i < levels.levels.Length; i++)
        {
            int index = i;
            var L = levels.levels[i];
            int col = i % 2, row = i / 2; // 2 columns × 3 rows
            var rt = Rect($"Button {L.name}", transform, new Vector2(-310 + col * 620, 70 - row * 120),
                new Vector2(580, 95));
            rt.gameObject.AddComponent<Image>().color = SpriteFactory.Hex("#495057");
            var button = rt.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = SpriteFactory.Hex("#868e96");
            colors.pressedColor = SpriteFactory.Hex("#1971c2");
            button.colors = colors;
            button.onClick.AddListener(() => { Sound.Play("Accept"); GameManager.Instance.StartAt(index); });
            var label = Label($"{i + 1}   {L.name}  ({L.width}×{L.height})", Vector2.zero, 40, Color.white, rt);
            label.rectTransform.sizeDelta = rt.sizeDelta;
        }

        Label($"Taste 1–{levels.levels.Length} oder klicken  ·  C = Figur wählen  ·  E = Hebel  ·  M = Ton aus/an  ·  Esc = Menü  ·  R = Level neu",
            new Vector2(0, -330), 30, new Color(1, 1, 1, 0.5f));
    }

    static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    Text Label(string s, Vector2 pos, int size, Color color, Transform parent = null)
    {
        var rt = Rect("Text", parent ? parent : transform, pos, new Vector2(1600, size * 1.6f));
        var t = rt.gameObject.AddComponent<Text>();
        t.font = Font;
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.text = s;
        t.raycastTarget = false;
        return t;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.cKey.wasPressedThisFrame)
        {
            SceneLoader.Instance.Load(SceneLoader.CharacterSelect);
            return;
        }
        for (int i = 0; i < count && i < 9; i++)
            if (kb[Key.Digit1 + i].wasPressedThisFrame || kb[Key.Numpad1 + i].wasPressedThisFrame)
            {
                Sound.Play("Accept");
                GameManager.Instance.StartAt(i);
                return;
            }
    }
}
