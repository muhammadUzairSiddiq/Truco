using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared menu components. Text stays live/localizable; generated art is decoration only.</summary>
public static class TrucoClubUI
{
    public static readonly Color Ink = new Color32(43, 27, 18, 255);
    public static readonly Color Surface = new Color32(78, 47, 29, 255);
    public static readonly Color Raised = new Color32(108, 69, 43, 255);
    public static readonly Color Gold = new Color32(226, 191, 118, 255);
    public static readonly Color Cream = new Color32(247, 240, 218, 255);
    public static readonly Color Muted = new Color32(211, 192, 160, 255);
    public static readonly Color Green = new Color32(36, 115, 77, 255);
    static Sprite _background, _hero;
    public static Sprite Background => _background != null ? _background : _background = Load("MenuBackground");
    public static Sprite Hero => _hero != null ? _hero : _hero = Load("TournamentHero");
    static Sprite Load(string name)
    {
        var imported = Resources.Load<Sprite>("TrucoUI/Club/" + name);
        if (imported != null) return imported;
        var texture = Resources.Load<Texture2D>("TrucoUI/Club/" + name);
        return texture == null ? null : Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f);
    }
    public static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max,
        Vector2 bottomLeft = default, Vector2 topRight = default)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false); rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = bottomLeft; rt.offsetMax = topRight; return rt;
    }
    public static Image Panel(string name, Transform parent, Color color, Vector2 min, Vector2 max,
        Vector2 bottomLeft = default, Vector2 topRight = default)
    {
        var image = Rect(name, parent, min, max, bottomLeft, topRight).gameObject.AddComponent<Image>();
        TrucoMenuTheme.Surface(image, color); return image;
    }
    public static TextMeshProUGUI Text(Transform parent, string text, float size, Color color,
        Vector2 min, Vector2 max, bool bold = false, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
    {
        var tmp = Rect("Label", parent, min, max, new Vector2(20, 8), new Vector2(-20, -8)).gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.font = TMP_Settings.defaultFontAsset; tmp.fontSize = size;
        tmp.color = color; tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.alignment = align; tmp.raycastTarget = false;
        tmp.enableAutoSizing = true; tmp.fontSizeMin = size * .75f; tmp.fontSizeMax = size;
        tmp.textWrappingMode = TextWrappingModes.Normal; return tmp;
    }
    public static Button Button(Transform parent, string caption, Action action, Vector2 min, Vector2 max,
        bool secondary = false)
    {
        var image = Panel(caption, parent, secondary ? Raised : Green, min, max, new Vector2(8, 8), new Vector2(-8, -8));
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
        colors.pressedColor = new Color(.78f, .86f, .8f); colors.disabledColor = new Color(.5f, .5f, .5f, .65f);
        button.colors = colors; button.onClick.AddListener(() => action?.Invoke());
        Text(image.transform, caption, 27, Cream, Vector2.zero, Vector2.one, true, TextAlignmentOptions.Center);
        image.gameObject.AddComponent<TrucoButtonFeedback>(); return button;
    }
    public static TMP_InputField Input(Transform parent, string placeholder, Vector2 min, Vector2 max)
    {
        var image = Panel(placeholder, parent, new Color32(240, 235, 216, 255), min, max, new Vector2(8, 8), new Vector2(-8, -8));
        var field = image.gameObject.AddComponent<TMP_InputField>();
        var viewport = Rect("Viewport", image.transform, Vector2.zero, Vector2.one, new Vector2(12, 4), new Vector2(-12, -4));
        viewport.gameObject.AddComponent<RectMask2D>();
        field.textViewport = viewport;
        field.textComponent = Text(viewport, "", 27, Ink, Vector2.zero, Vector2.one);
        field.placeholder = Text(viewport, placeholder, 25, new Color32(87, 103, 90, 255), Vector2.zero, Vector2.one);
        field.characterLimit = 160; return field;
    }
    public static RectTransform Screen(GameObject root, int order = TrucoCanvasOrder.Screen)
    {
        var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
        root.AddComponent<GraphicRaycaster>();
        var scaler = root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = .5f;
        var bg = Panel("Background", root.transform, Ink, Vector2.zero, Vector2.one);
        TrucoMenuTheme.Surface(bg, Ink, false);
        var safe = Rect("SafeArea", root.transform, Vector2.zero, Vector2.one);
        safe.gameObject.AddComponent<TrucoSafeArea>(); return safe;
    }
}
