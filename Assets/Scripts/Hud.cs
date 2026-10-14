using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Screen overlay built at runtime: HP squares, level name, fight status, hints and big messages.
public class Hud : MonoBehaviour
{
    Image[] hpBoxes;
    Text levelText, statusText, hintText, centerText, inventoryText;
    GameObject hintPanel, centerPanel;
    Coroutine hintRoutine, centerRoutine;

    static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    public static Hud Create(int maxHp)
    {
        var go = new GameObject("HUD");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var hud = go.AddComponent<Hud>();
        hud.Build(maxHp);
        return hud;
    }

    void Build(int maxHp)
    {
        var hpLabel = MakeText("HP", new Vector2(0, 1), new Vector2(40, -40), 40, TextAnchor.UpperLeft);
        hpLabel.rectTransform.sizeDelta = new Vector2(80, 50);
        hpBoxes = new Image[maxHp];
        for (int i = 0; i < maxHp; i++)
        {
            var box = new GameObject("HP", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(transform, false);
            var rt = (RectTransform)box.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(120 + i * 48, -44);
            rt.sizeDelta = new Vector2(38, 38);
            hpBoxes[i] = box.GetComponent<Image>();
        }
        inventoryText = MakeText("", new Vector2(0, 1), new Vector2(40, -100), 28, TextAnchor.UpperLeft);
        inventoryText.rectTransform.sizeDelta = new Vector2(700, 90);
        inventoryText.supportRichText = true;
        var top = Panel("Top", new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(520, 110));
        levelText = MakeText("", new Vector2(0.5f, 1), new Vector2(0, -8), 40, TextAnchor.UpperCenter, top.transform);
        statusText = MakeText("", new Vector2(0.5f, 1), new Vector2(0, -58), 30, TextAnchor.UpperCenter, top.transform);
        levelText.rectTransform.sizeDelta = statusText.rectTransform.sizeDelta = new Vector2(500, 50);

        hintPanel = Panel("Hint", new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1400, 110));
        hintText = MakeText("", new Vector2(0.5f, 0.5f), Vector2.zero, 34, TextAnchor.MiddleCenter, hintPanel.transform);
        hintText.rectTransform.sizeDelta = new Vector2(1340, 100);
        hintPanel.SetActive(false);

        centerPanel = Panel("Center", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 220));
        centerText = MakeText("", new Vector2(0.5f, 0.5f), Vector2.zero, 60, TextAnchor.MiddleCenter, centerPanel.transform);
        centerText.rectTransform.sizeDelta = new Vector2(1060, 200);
        centerPanel.SetActive(false);
    }

    GameObject Panel(string name, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.GetComponent<Image>().color = new Color(0, 0, 0, 0.75f);
        return go;
    }

    Text MakeText(string s, Vector2 anchor, Vector2 pos, int size, TextAnchor align, Transform parent = null)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Outline));
        go.transform.SetParent(parent ? parent : transform, false);
        var t = go.GetComponent<Text>();
        t.font = Font;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.text = s;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        go.GetComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(1200, 60);
        return t;
    }

    public void SetHp(int hp)
    {
        for (int i = 0; i < hpBoxes.Length; i++)
            hpBoxes[i].color = i < hp ? new Color(0.88f, 0.19f, 0.19f) : new Color(1, 1, 1, 0.25f);
    }

    public void SetLevel(string name) => levelText.text = name;

    // Keys as colored dots, abilities with their button.
    public void SetInventory(System.Collections.Generic.IEnumerable<string> keys,
        System.Collections.Generic.IEnumerable<string> abilities)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var k in keys) sb.Append($"<color={k}>●</color> ");
        if (sb.Length > 0) sb.Insert(0, "Schlüssel ").Append('\n');
        foreach (var a in abilities)
            sb.Append(a == "pull" ? "Ziehen [E]   " : a == "shield" ? "Schild [Leertaste]   " : a + "   ");
        inventoryText.text = sb.ToString();
    }
    public void SetStatus(string s) => statusText.text = s;

    public void ShowHint(string text, float seconds = 6f)
    {
        if (hintRoutine != null) StopCoroutine(hintRoutine);
        hintRoutine = StartCoroutine(Timed(hintPanel, hintText, text, seconds));
    }

    public void ShowCenter(string text, float seconds = 0f)
    {
        if (centerRoutine != null) StopCoroutine(centerRoutine);
        centerText.text = text;
        centerPanel.SetActive(text.Length > 0);
        if (seconds > 0f) centerRoutine = StartCoroutine(Timed(centerPanel, centerText, text, seconds));
    }

    IEnumerator Timed(GameObject panel, Text label, string text, float seconds)
    {
        label.text = text;
        if (panel) panel.SetActive(true);
        yield return new WaitForSeconds(seconds);
        label.text = "";
        if (panel) panel.SetActive(false);
    }
}
