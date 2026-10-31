using UnityEngine;
using UnityEngine.UI;

/// <summary>Highlights the active bottom tab and dims the others (synchronized with main-panel Tournament button).</summary>
public static class MainMenuNavBarVisuals
{
    public static void Apply(Button menu, Button tournament, Button profile, Button notification, int selectedIndex)
    {
        var buttons = new[] { menu, tournament, profile, notification };
        var captions = new[] { "INICIO", "TORNEOS", "PERFIL", "AVISOS" };
        for (var i = 0; i < buttons.Length; i++)
        {
            var b = buttons[i];
            if (b == null) continue;
            var tab = b.GetComponent<TrucoNavigationTab>();
            if (tab == null) { tab = b.gameObject.AddComponent<TrucoNavigationTab>(); tab.Configure(captions[i]); }
            tab.SetSelected(i == selectedIndex);
        }
    }
}
