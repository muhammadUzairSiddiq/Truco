using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TrucoProfilePresentation : MonoBehaviour
{
    TextMeshProUGUI _stats;
    Button[] _avatars;
    public static void Ensure(Transform main)
    {
        var profile = main.Find("Profile Panel");
        if (profile == null || profile.GetComponent<TrucoProfilePresentation>() != null) return;
        var host = profile.gameObject.AddComponent<TrucoProfilePresentation>();
        TrucoMenuTheme.Background(profile);
        var marker = TrucoClubUI.Rect("ProfileTheme", profile, Vector2.zero, Vector2.one); marker.SetAsFirstSibling();
        var logo = profile.Find("logo"); if (logo != null) logo.gameObject.SetActive(false);
        var title = profile.Find("TITLE");
        if (title != null)
        {
            Place((RectTransform)title, new Vector2(.06f, .79f), new Vector2(.94f, .85f));
            var text = title.GetComponent<TextMeshProUGUI>();
            if (text != null) { text.text = "MI PERFIL"; text.color = TrucoMenuTheme.Ink; text.fontSize = 42; text.fontStyle = FontStyles.Bold; }
        }
        var summary = TrucoClubUI.Panel("ProfileSummary", profile, TrucoMenuTheme.Card, new Vector2(.06f, .705f), new Vector2(.94f, .79f));
        host._stats = TrucoClubUI.Text(summary.transform, "", 27, TrucoMenuTheme.Ink, Vector2.zero, Vector2.one, true, TextAlignmentOptions.Center);
        TrucoClubUI.Text(profile, "ELEGÍ TU AVATAR", 28, TrucoMenuTheme.Secondary, new Vector2(.06f, .655f), new Vector2(.94f, .705f), true);
        var grid = profile.Find("Avatar Selection Panel");
        if (grid != null)
        {
            Place((RectTransform)grid, new Vector2(.06f, .355f), new Vector2(.94f, .655f));
            var image = grid.GetComponent<Image>(); if (image != null) TrucoMenuTheme.Surface(image, TrucoMenuTheme.Card);
            var layout = grid.GetComponent<GridLayoutGroup>() ?? grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount; layout.constraintCount = 4;
            layout.cellSize = new Vector2(140, 140); layout.spacing = new Vector2(26, 22);
            layout.padding = new RectOffset(24, 24, 22, 22); layout.childAlignment = TextAnchor.MiddleCenter;
            host._avatars = grid.GetComponentsInChildren<Button>(true).Where(b => b.name.StartsWith("Avatar Button")).OrderBy(b => b.transform.GetSiblingIndex()).ToArray();
            foreach (var button in host._avatars)
            {
                button.transform.localScale = Vector3.one;
                var portrait = button.GetComponent<Image>(); if (portrait != null) portrait.preserveAspect = true;
                if (button.GetComponent<Outline>() == null) button.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3, -3);
            }
        }
        var logout = profile.Find("logout Button");
        if (logout != null) { Place((RectTransform)logout, new Vector2(.51f, .12f), new Vector2(.94f, .18f)); TrucoMenuTheme.StyleButton(logout.GetComponent<Button>(), false); }
        TrucoClubUI.Button(profile, "BILLETERA", () => TrucoWalletScreen.Show(), new Vector2(.06f, .12f), new Vector2(.49f, .18f));
        host.Refresh();
    }
    static void Place(RectTransform rt, Vector2 min, Vector2 max)
    { rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero; rt.localScale = Vector3.one; }
    void OnEnable() { if (Application.isPlaying) InvokeRepeating(nameof(Refresh), 0, .4f); }
    void OnDisable() => CancelInvoke(nameof(Refresh));
    public void Refresh()
    {
        var stats = ApiController.GetSessionUser?.Data?.stats;
        if (_stats != null) _stats.text = stats == null ? "TU PERFIL\nElegí cómo te ven en la mesa" : $"{stats.matchesPlayed} PARTIDAS    ·    {stats.wins} VICTORIAS    ·    {stats.losses} DERROTAS";
        if (_avatars != null) for (int i = 0; i < _avatars.Length; i++)
        {
            var outline = _avatars[i].GetComponent<Outline>();
            if (outline != null) outline.effectColor = i == PlayerAvatarData.SelectedIndex ? TrucoMenuTheme.Gold : Color.clear;
        }
    }
}
