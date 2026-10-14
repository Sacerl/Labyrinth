using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Start screen: pick a level by click or number key 1–4.
public class LevelMenu : MonoBehaviour
{
    Action<int> onPick;
    Action onCharacters;
    int count;

    static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    public static LevelMenu Create(LevelSet levels, Action<int> pick, Action characters)
    {
        if (!FindAnyObjectByType<EventSystem>())
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var go = new GameObject("Level Menu");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10; // above the HUD
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();

        var menu = go.AddComponent<LevelMenu>();
        menu.onPick = pick;
        menu.onCharacters = characters;
        menu.count = levels.levels.Length;
        menu.Build(levels);
        return menu;
    }

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
            button.onClick.AddListener(() => { Sound.Play("Accept"); onPick(index); });
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

    public bool Visible => gameObject.activeSelf;
    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.cKey.wasPressedThisFrame)
        {
            onCharacters?.Invoke();
            return;
        }
        for (int i = 0; i < count && i < 9; i++)
            if (kb[Key.Digit1 + i].wasPressedThisFrame || kb[Key.Numpad1 + i].wasPressedThisFrame)
            {
                Sound.Play("Accept");
                onPick(i);
                return;
            }
    }
}
