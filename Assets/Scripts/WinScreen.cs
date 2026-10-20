using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// WinScreen scene, loaded after the last level. "Nochmal spielen" (Enter) starts again at Level1,
// "Levelauswahl" (Esc) goes back to the level menu. The Canvas is set up in the scene.
public class WinScreen : MonoBehaviour
{
    static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    void Awake()
    {
        var bg = Rect("Background", transform, Vector2.zero, Vector2.zero);
        bg.anchorMin = Vector2.zero;
        bg.anchorMax = Vector2.one;
        bg.gameObject.AddComponent<Image>().color = SpriteFactory.Hex("#07060d");

        var glow = Rect("Glow", transform, new Vector2(0, 170), new Vector2(900, 500)).gameObject.AddComponent<Image>();
        glow.sprite = SpriteFactory.Glow;
        glow.color = new Color(1f, 0.72f, 0.38f, 0.35f);
        glow.raycastTarget = false;

        Label("Du hast gewonnen!", new Vector2(0, 220), 110, SpriteFactory.Hex("#ffd8a8"));
        Label("Alle Labyrinthe gemeistert.", new Vector2(0, 100), 44, new Color(1, 1, 1, 0.7f));
        MakeButton("Nochmal spielen", new Vector2(0, -80), PlayAgain);
        MakeButton("Levelauswahl", new Vector2(0, -200), ToLevels);
        Label("Enter = nochmal  ·  Esc = Levelauswahl", new Vector2(0, -400), 30, new Color(1, 1, 1, 0.45f));
    }

    void Start()
    {
        GameManager.Instance.EnterMenu(null);
        Sound.Play("Victory");
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) PlayAgain();
        else if (kb.escapeKey.wasPressedThisFrame) ToLevels();
    }

    static void PlayAgain()
    {
        Sound.Play("Accept");
        GameManager.Instance.StartAt(0);
    }

    static void ToLevels()
    {
        Sound.Play("Accept");
        SceneLoader.Instance.Load(SceneLoader.LevelSelect);
    }

    void MakeButton(string text, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var rt = Rect($"Button {text}", transform, pos, new Vector2(580, 95));
        rt.gameObject.AddComponent<Image>().color = SpriteFactory.Hex("#495057");
        var button = rt.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = SpriteFactory.Hex("#868e96");
        colors.pressedColor = SpriteFactory.Hex("#1971c2");
        button.colors = colors;
        button.onClick.AddListener(onClick);
        var label = Label(text, Vector2.zero, 40, Color.white, rt);
        label.rectTransform.sizeDelta = rt.sizeDelta;
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
}
