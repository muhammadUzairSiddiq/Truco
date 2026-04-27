using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Applies <see cref="TrucoUiTheme"/> to live overlay roots at runtime (survives scene refactors).</summary>
public static class TrucoThemeRuntime
{
    public static void ApplyToLoading(Transform contentRoot, TextMeshProUGUI loadingText)
    {
        if (contentRoot == null) return;
        foreach (var img in contentRoot.GetComponentsInChildren<Image>(true))
        {
            if (img == null) continue;
            if (img.transform == contentRoot) img.color = TrucoUiTheme.OverlayScrim;
            else if (string.Equals(img.name, "LoadingBG", System.StringComparison.Ordinal))
                img.color = new Color(0.9f, 0.82f, 0.68f, 1f);
        }
        if (loadingText != null) loadingText.color = TrucoUiTheme.TextPrimary;
    }

    public static void ApplyToNotification(Transform contentRoot, TextMeshProUGUI messageText, Button closeBtn = null)
    {
        if (contentRoot == null) return;
        foreach (var img in contentRoot.GetComponentsInChildren<Image>(true))
        {
            if (img == null) continue;
            if (img.transform == contentRoot) img.color = TrucoUiTheme.OverlayScrim;
            else if (string.Equals(img.name, "NotificationBG", System.StringComparison.Ordinal))
                img.color = new Color(0.9f, 0.82f, 0.68f, 1f);
        }
        if (messageText != null) messageText.color = TrucoUiTheme.TextPrimary;
        if (closeBtn != null)
        {
            var c = closeBtn.colors;
            c.normalColor = TrucoUiTheme.PanelWoodDeep;
            c.highlightedColor = Color.Lerp(TrucoUiTheme.PanelWoodDeep, Color.white, 0.1f);
            closeBtn.colors = c;
        }
    }
}
