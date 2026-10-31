using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Brown, cream and green surfaces shared by player screens. Home artwork stays authored.</summary>
public static class TrucoMenuTheme
{
    public static readonly Color Brown = new Color32(78, 47, 29, 255);
    public static readonly Color DeepBrown = new Color32(47, 29, 20, 255);
    public static readonly Color Paper = new Color32(243, 235, 218, 255);
    public static readonly Color Card = new Color32(255, 250, 238, 255);
    public static readonly Color Ink = new Color32(65, 43, 29, 255);
    public static readonly Color Secondary = new Color32(120, 96, 70, 255);
    public static readonly Color Gold = new Color32(228, 190, 112, 255);
    static Sprite _rounded;
    public static Sprite Rounded
    {
        get
        {
            if (_rounded != null) return _rounded;
            const int size = 64; const float radius = 12;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "TrucoRoundedSurface", filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x - .5f, x + .5f - (size - radius));
                float dy = Mathf.Max(radius - y - .5f, y + .5f - (size - radius));
                float distance = new Vector2(Mathf.Max(0, dx), Mathf.Max(0, dy)).magnitude;
                texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - distance + .5f)));
            }
            texture.Apply();
            _rounded = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 100, 0,
                SpriteMeshType.FullRect, new Vector4(14, 14, 14, 14));
            return _rounded;
        }
    }
    public static void Surface(Image image, Color color, bool rounded = true)
    {
        if (image == null) return;
        image.enabled = true;
        image.sprite = rounded ? Rounded : null; image.type = rounded ? Image.Type.Sliced : Image.Type.Simple;
        image.overrideSprite = null;
        image.pixelsPerUnitMultiplier = 1;
        image.preserveAspect = false;
        image.fillCenter = true;
        image.color = color;
    }
    public static void Background(Transform root)
    {
        var image = root.GetComponent<Image>() ?? root.gameObject.AddComponent<Image>();
        Surface(image, Paper, false);
    }
    public static RectTransform Header(Transform parent, string title, string subtitle = null)
    {
        var header = TrucoClubUI.Panel("ScreenHeader", parent, Brown, new Vector2(0, .87f), Vector2.one).rectTransform;
        Surface(header.GetComponent<Image>(), Brown, false);
        TrucoClubUI.Text(header, title, 43, Gold, new Vector2(.04f, .35f), new Vector2(.96f, .94f), true);
        if (subtitle != null) TrucoClubUI.Text(header, subtitle, 25, Paper, new Vector2(.04f, .04f), new Vector2(.96f, .38f));
        return header;
    }
    public static void StyleButton(Button button, bool primary = true)
    {
        if (button == null) return;
        button.transition = Selectable.Transition.ColorTint;
        var image = button.GetComponent<Image>();
        if (image != null) Surface(image, primary ? TrucoClubUI.Green : Brown);
        var colors = button.colors; colors.normalColor = colors.selectedColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.06f, 1.02f); colors.pressedColor = new Color(.8f, .8f, .8f);
        colors.disabledColor = new Color(.55f, .55f, .55f, .65f); button.colors = colors;
        foreach (var label in button.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            label.color = Card; label.fontStyle = FontStyles.Bold;
            label.fontSize = 30; label.enableAutoSizing = true; label.fontSizeMin = 23; label.fontSizeMax = 30;
            label.alignment = TextAlignmentOptions.Center;
        }
        if (button.GetComponent<TrucoButtonFeedback>() == null) button.gameObject.AddComponent<TrucoButtonFeedback>();
    }
    public static void DestroyUI(GameObject value)
    { if (value == null) return; value.SetActive(false); if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
}
