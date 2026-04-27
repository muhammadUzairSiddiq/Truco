using UnityEngine;

/// <summary>Truco product UI: woody browns, cream, dark text. Use for runtime UI and overlays.</summary>
public static class TrucoUiTheme
{
    public static readonly Color BackgroundCream = new Color(0.98f, 0.95f, 0.9f, 0.97f);
    public static readonly Color PanelWoodLight = new Color(0.72f, 0.58f, 0.42f, 1f);
    public static readonly Color PanelWoodDeep = new Color(0.42f, 0.32f, 0.22f, 1f);
    public static readonly Color OverlayScrim = new Color(0.12f, 0.1f, 0.08f, 0.45f);
    public static readonly Color TextPrimary = new Color(0.2f, 0.12f, 0.08f, 1f);
    public static readonly Color TextSecondary = new Color(0.35f, 0.25f, 0.18f, 1f);
    public static readonly Color AccentGreen = new Color(0.25f, 0.55f, 0.32f, 1f);
    public static readonly Color RoomRowPanel = new Color(0.55f, 0.45f, 0.32f, 0.4f);
    public static readonly Color RoomRowJoin = new Color(0.62f, 0.5f, 0.32f, 1f);
    public static readonly Color CreateFormBg = new Color(0.48f, 0.4f, 0.3f, 0.95f);
    public static readonly Color InputBg = new Color(0.98f, 0.95f, 0.9f, 0.95f);
    public static readonly Color NavSelected = new Color(0.95f, 0.86f, 0.7f, 1f);
    public static readonly Color NavUnselected = new Color(0.5f, 0.45f, 0.4f, 0.4f);
    /// <summary>Child &quot;Icon&quot; Image tint when its tab is active (Button ColorBlock tints the background, not the icon).</summary>
    public static readonly Color NavIconOnTab = Color.white;
    public static readonly Color NavIconOffTab = new Color(0.55f, 0.5f, 0.45f, 0.8f);
}
