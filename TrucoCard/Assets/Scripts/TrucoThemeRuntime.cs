using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Applies <see cref="TrucoUiTheme"/> and Panel sprite to live overlay roots at runtime.</summary>
public static class TrucoThemeRuntime
{
    const string PanelBgName = "NotificationBG";
    const string LoadingBgName = "LoadingBG";

    public static void ApplyToLoading(Transform contentRoot, TextMeshProUGUI loadingText)
    {
        if (contentRoot == null) return;
        ApplyScrim(contentRoot);
        foreach (var img in contentRoot.GetComponentsInChildren<Image>(true))
        {
            if (img == null || img.transform == contentRoot) continue;
            if (!string.Equals(img.name, LoadingBgName, System.StringComparison.Ordinal)) continue;
            StylePromptPanel(img);
        }
        StylePromptText(loadingText, 30f);
    }

    public static void ApplyToNotification(Transform contentRoot, TextMeshProUGUI messageText, Button closeBtn = null)
    {
        if (contentRoot == null) return;
        ApplyScrim(contentRoot);
        foreach (var img in contentRoot.GetComponentsInChildren<Image>(true))
        {
            if (img == null || img.transform == contentRoot) continue;
            if (!string.Equals(img.name, PanelBgName, System.StringComparison.Ordinal)) continue;
            StylePromptPanel(img);
        }
        StylePromptText(messageText, 28f);
        if (closeBtn != null) StylePromptCloseButton(closeBtn);
    }

    static void ApplyScrim(Transform contentRoot)
    {
        var rootImg = contentRoot.GetComponent<Image>();
        if (rootImg != null) rootImg.color = TrucoUiTheme.OverlayScrim;
    }

    static void StylePromptPanel(Image img)
    {
        var panel = TrucoUiAssetLoader.Panel;
        if (panel != null)
        {
            img.sprite = panel;
            img.type = Image.Type.Sliced;
            img.preserveAspect = false;
            img.color = new Color(1f, 1f, 1f, 0.97f);
        }
        else
        {
            img.color = new Color(0.14f, 0.38f, 0.22f, 0.94f);
        }

        var rt = img.rectTransform;
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(780f, 420f);
            var le = img.GetComponent<LayoutElement>();
            if (le == null) le = img.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 640f;
            le.preferredWidth = 780f;
            le.minHeight = 320f;
            le.preferredHeight = 420f;
        }
    }

    static void StylePromptText(TextMeshProUGUI text, float baseSize)
    {
        if (text == null) return;
        text.color = Color.white;
        text.fontStyle = FontStyles.Bold;
        text.fontSize = baseSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = 20f;
        text.fontSizeMax = baseSize;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.margin = new Vector4(48f, 36f, 48f, 36f);
        text.raycastTarget = false;

        var rt = text.rectTransform;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.08f, 0.22f);
            rt.anchorMax = new Vector2(0.92f, 0.88f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    static void StylePromptCloseButton(Button closeBtn)
    {
        var green = TrucoUiAssetLoader.GreenButton;
        var img = closeBtn.GetComponent<Image>();
        if (img != null)
        {
            if (green != null)
            {
                img.sprite = green;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.color = TrucoUiTheme.AccentGreen;
            }
        }

        var c = closeBtn.colors;
        c.normalColor = Color.white;
        c.highlightedColor = new Color(0.92f, 0.98f, 0.94f, 1f);
        c.pressedColor = new Color(0.82f, 0.9f, 0.85f, 1f);
        closeBtn.colors = c;

        var label = closeBtn.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.text = TrucoTextosClient.Ok;
            label.color = Color.white;
            label.fontStyle = FontStyles.Bold;
            label.fontSize = 26f;
            label.alignment = TextAlignmentOptions.Center;
        }
    }
}
