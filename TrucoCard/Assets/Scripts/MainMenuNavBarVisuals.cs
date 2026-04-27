using UnityEngine;
using UnityEngine.UI;

/// <summary>Highlights the active bottom tab and dims the others (synchronized with main-panel Tournament button).</summary>
public static class MainMenuNavBarVisuals
{
    public static void Apply(Button menu, Button tournament, Button profile, Button notification, int selectedIndex)
    {
        var hi = TrucoUiTheme.NavSelected;
        var lo = TrucoUiTheme.NavUnselected;
        var buttons = new[] { menu, tournament, profile, notification };
        for (var i = 0; i < buttons.Length; i++)
        {
            var b = buttons[i];
            if (b == null) continue;
            var c = b.colors;
            c.fadeDuration = 0.12f;
            c.colorMultiplier = 1f;
            var on = (i == selectedIndex) ? hi : lo;
            c.normalColor = on;
            c.highlightedColor = Color.Lerp(on, Color.white, 0.12f);
            c.pressedColor = Color.Lerp(on, Color.black, 0.08f);
            c.selectedColor = on;
            c.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.35f);
            b.colors = c;
            if (b.targetGraphic != null) b.targetGraphic.color = Color.white;
            var selected = (i == selectedIndex);
            var icon = b.transform.Find("Icon");
            if (icon != null)
            {
                var g = icon.GetComponent<Graphic>();
                if (g != null) g.color = selected ? TrucoUiTheme.NavIconOnTab : TrucoUiTheme.NavIconOffTab;
            }
        }
    }
}
