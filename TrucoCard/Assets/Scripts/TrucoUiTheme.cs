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
    /// <summary>1v1 room list — wood/brown bar (matches header feel).</summary>
    public static readonly Color RoomListRowBar = new Color(0.44f, 0.30f, 0.20f, 1f);
    /// <summary>Room title line — gold/cream pop on brown.</summary>
    public static readonly Color RoomListRowTitle = new Color(1f, 0.92f, 0.70f, 1f);
    /// <summary>Meta line (entrada, tipo) — cool light text.</summary>
    public static readonly Color RoomListRowMeta = new Color(0.90f, 0.95f, 1f, 1f);
    /// <summary>Players line — warm mint (distinct from meta).</summary>
    public static readonly Color RoomListRowPlayers = new Color(0.78f, 0.96f, 0.86f, 1f);
    /// <summary>Compact “Entrar” (high contrast on bar).</summary>
    public static readonly Color RoomListJoinButtonBg = new Color(0.20f, 0.55f, 0.32f, 1f);
    public static readonly Color RoomListJoinButtonText = new Color(1f, 1f, 0.99f, 1f);
    public static readonly Color CreateFormBg = new Color(0.48f, 0.4f, 0.3f, 0.95f);
    /// <summary>Outer frame of create-room form (darker wood).</summary>
    public static readonly Color CreateFormPanelCard = new Color(0.36f, 0.26f, 0.18f, 1f);
    /// <summary>Cream “inset” where fields sit (higher contrast for large text).</summary>
    public static readonly Color CreateFormPanelInner = new Color(0.97f, 0.94f, 0.89f, 0.98f);
    public static readonly Color CreateFormButtonPrimary = new Color(0.18f, 0.52f, 0.30f, 1f);
    public static readonly Color CreateFormButtonSecondary = new Color(0.40f, 0.30f, 0.22f, 1f);
    public static readonly Color CreateFormButtonText = new Color(1f, 0.99f, 0.96f, 1f);
    /// <summary>Section labels (Entrada, Pública) on wood/cream — large readable gold-white.</summary>
    public static readonly Color CreateFormLabel = new Color(0.32f, 0.22f, 0.14f, 1f);
    public static readonly Color CreateFormSubLabelCream = new Color(0.18f, 0.12f, 0.08f, 1f);
    public static readonly Color InputBg = new Color(1f, 0.99f, 0.97f, 1f);
    public static readonly Color NavSelected = new Color(0.95f, 0.86f, 0.7f, 1f);
    public static readonly Color NavUnselected = new Color(0.5f, 0.45f, 0.4f, 0.4f);
    /// <summary>Child &quot;Icon&quot; Image tint when its tab is active (Button ColorBlock tints the background, not the icon).</summary>
    public static readonly Color NavIconOnTab = Color.white;
    public static readonly Color NavIconOffTab = new Color(0.55f, 0.5f, 0.45f, 0.8f);

    public static readonly Color TournamentListScreenBg = new Color(0.97f, 0.95f, 0.92f, 1f);
    public static readonly Color TournamentHeaderWoodTint = new Color(0.55f, 0.42f, 0.3f, 1f);
    /// <summary>Single top bar for list screens (no double-strip).</summary>
    public static readonly Color TournamentTopBarSolid = new Color(0.36f, 0.25f, 0.16f, 1f);
    public static readonly Color TournamentTitleBarBg = new Color(1f, 0.99f, 0.97f, 1f);
    public static readonly Color TournamentCardWoodTint = new Color(0.65f, 0.5f, 0.35f, 1f);
    public static readonly Color TournamentPromoGold = new Color(0.92f, 0.75f, 0.35f, 1f);
    /// <summary>Primary CTA for tournament list (e.g. INSCRIBIRSE).</summary>
    public static readonly Color TournamentRegisterButton = new Color(0.22f, 0.58f, 0.34f, 1f);
}
