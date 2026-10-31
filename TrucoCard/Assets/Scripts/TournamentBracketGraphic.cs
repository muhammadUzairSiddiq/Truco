using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentation-only knockout tree. Reads results; never decides or submits them.</summary>
public static class TournamentBracketGraphic
{
    static readonly Color Gold = TrucoMenuTheme.Gold;
    public static Vector2 Center(int round, int index, float pitch) =>
        new Vector2(0, 150f + (index + 0.5f) * (1 << round) * pitch);

    public static void Build(RectTransform host, TournamentBracketState state, string localId)
    {
        if (host == null || state == null || state.rounds == null || state.rounds.Count == 0) return;
        bool eight = state.maxPlayers == 8;
        float width = eight ? 286f : 400f;
        float gap = eight ? 56f : 100f;
        float pitch = eight ? 250f : 420f;
        float contentWidth = 32f + state.rounds.Count * width + (state.rounds.Count - 1) * gap;
        host.localScale = Vector3.one;
        host.sizeDelta = new Vector2(contentWidth, 200f + state.rounds[0].Count * pitch);

        // Draw connections first, behind the participant cards.
        for (int r = 0; r < state.rounds.Count - 1; r++)
            foreach (var match in state.rounds[r])
            {
                float x = 16f + r * (width + gap) + width;
                float y = Center(r, match.index, pitch).y;
                float parentY = Center(r + 1, match.index / 2, pitch).y;
                Color color = match.IsFinished ? Gold : new Color(.64f, .48f, .30f, .65f);
                Line(host, new Vector2(x, y), new Vector2(x + gap / 2f, y), color);
                Line(host, new Vector2(x + gap / 2f, y), new Vector2(x + gap / 2f, parentY), color);
                Line(host, new Vector2(x + gap / 2f, parentY), new Vector2(x + gap, parentY), color);
            }

        for (int r = 0; r < state.rounds.Count; r++)
        {
            float x = 16f + r * (width + gap);
            var heading = Panel(host, "Round_" + r, x, 20f, width, 72f, null);
            TrucoMenuTheme.Surface(heading.GetComponent<Image>(), TrucoMenuTheme.Brown);
            var roundTitle = Text(heading, "Label", state.RoundTitle(r), new Vector2(12f, 8f), new Vector2(width - 24f, 56f), eight ? 24f : 28f, Gold);
            roundTitle.enableAutoSizing = true; roundTitle.fontSizeMin = 20f; roundTitle.fontSizeMax = eight ? 24f : 28f;
            foreach (var match in state.rounds[r])
            {
                float y = Center(r, match.index, pitch).y;
                if (r == state.rounds.Count - 1)
                {
                    var trophy = Panel(host, "FinalTrophy", x + width / 2f - 34f, y - 190f, 68f, 68f, TrucoGameplayUiSprites.IconTrophy);
                    trophy.GetComponent<Image>().preserveAspect = true;
                }
                var card = Panel(host, "Match_" + r + "_" + match.index, x, y - 106f, width, 212f, null);
                TrucoMenuTheme.Surface(card.GetComponent<Image>(), TrucoMenuTheme.Card);
                bool localMatch = !string.IsNullOrEmpty(localId) && (match.first?.playerId == localId || match.second?.playerId == localId);
                var border = card.gameObject.AddComponent<Outline>();
                border.effectColor = localMatch ? TrucoClubUI.Green : new Color(.57f, .39f, .24f, .6f);
                border.effectDistance = localMatch ? new Vector2(3, -3) : new Vector2(1.5f, -1.5f);
                Slot(card, match.first, match.winnerId, localId, width, 12f);
                Slot(card, match.second, match.winnerId, localId, width, 92f);
                Line(card, new Vector2(18f, 88f), new Vector2(width - 18f, 88f), new Color(.47f, .34f, .21f, .18f));
                string status = match.IsFinished ? "FINALIZADO" : match.IsReady ? "LISTO PARA JUGAR" : "ESPERANDO RIVAL";
                Text(card, "Status", status, new Vector2(12f, 177f), new Vector2(width - 24f, 27f), 18f,
                    match.IsFinished ? TrucoClubUI.Green : TrucoMenuTheme.Secondary);
            }
        }
        var viewport = host.parent as RectTransform;
        if (viewport != null)
        {
            var fit = viewport.GetComponent<TournamentBracketViewportFit>() ?? viewport.gameObject.AddComponent<TournamentBracketViewportFit>();
            fit.Configure(host);
        }
        RefreshFit(host);
    }

    public static void RefreshFit(RectTransform host)
    {
        if (host == null) return;
        var viewport = host.parent as RectTransform;
        if (viewport == null || viewport.rect.width < 50 || viewport.rect.height < 50) return;
        float width = host.sizeDelta.x;
        if (width <= 0 || host.sizeDelta.y <= 0) return;
        // Fit the whole tree where possible. Retain readable cards and scrolling on smaller views.
        float scale = Mathf.Clamp(Mathf.Min((viewport.rect.width - 16) / width,
            (viewport.rect.height - 16) / host.sizeDelta.y), .78f, 1f);
        host.localScale = Vector3.one * scale;
        host.anchoredPosition = new Vector2(Mathf.Max(0, (viewport.rect.width - width * scale) * .5f), 0);
    }

    static void Slot(RectTransform parent, TournamentBracketState.Slot slot, string winnerId, string localId, float width, float y)
    {
        bool known = slot != null && !string.IsNullOrEmpty(slot.playerId);
        bool won = known && slot.playerId == winnerId;
        bool local = known && slot.playerId == localId;
        string name = known ? slot.playerName : "Por definir";
        if (known && string.IsNullOrWhiteSpace(name)) name = "Jugador";
        if (won)
        {
            var highlight = Panel(parent, "Winner", 8f, y, width - 16f, 72f, null);
            TrucoMenuTheme.Surface(highlight.GetComponent<Image>(), new Color32(237, 217, 169, 255));
        }
        var avatar = Panel(parent, "Avatar", 18f, y + 10f, 42f, 48f, TrucoGameplayUiSprites.IconPerson);
        avatar.GetComponent<Image>().preserveAspect = true;
        avatar.GetComponent<Image>().color = local ? TrucoClubUI.Green : known ? TrucoMenuTheme.Secondary : new Color(.62f, .56f, .47f);
        Color color = slot != null && slot.eliminated && !won ? TrucoMenuTheme.Secondary : TrucoMenuTheme.Ink;
        var label = Text(parent, "Player", name + (local && name != "Vos" ? " (vos)" : ""),
            new Vector2(70f, y + 4f), new Vector2(width - 84f, 64f), 26f, color);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.enableAutoSizing = true;
        label.fontSizeMin = 20f;
        label.fontSizeMax = 26f;
    }

    static void Line(RectTransform parent, Vector2 from, Vector2 to, Color color)
    {
        var delta = to - from;
        if (delta.sqrMagnitude < 0.01f) return;
        var rt = Panel(parent, "Connection", (from.x + to.x) / 2f, (from.y + to.y) / 2f, delta.magnitude, 3f, null);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        rt.GetComponent<Image>().color = color;
    }

    static RectTransform Panel(Transform parent, string name, float x, float y, float width, float height, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(width, height);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null && sprite.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple;
        image.color = Color.white;
        image.raycastTarget = false;
        return rt;
    }

    static TextMeshProUGUI Text(Transform parent, string name, string value, Vector2 position, Vector2 size, float font, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(position.x, -position.y);
        rt.sizeDelta = size;
        var text = go.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = font;
        text.color = color;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }
}

/// <summary>Refits after the canvas resolves or the device viewport changes.</summary>
[ExecuteAlways]
public sealed class TournamentBracketViewportFit : MonoBehaviour
{
    RectTransform _host;
    Vector2 _viewportSize, _contentSize;
    public void Configure(RectTransform host)
    {
        _host = host; _viewportSize = _contentSize = Vector2.zero;
        RefreshIfChanged();
    }
    void LateUpdate() => RefreshIfChanged();
    public void Refresh() { _viewportSize = _contentSize = Vector2.zero; RefreshIfChanged(); }
    void OnRectTransformDimensionsChange() => RefreshIfChanged();
    void RefreshIfChanged()
    {
        if (_host == null || transform is not RectTransform viewport) return;
        Vector2 size = viewport.rect.size;
        if (size == _viewportSize && _host.sizeDelta == _contentSize) return;
        _viewportSize = size; _contentSize = _host.sizeDelta;
        TournamentBracketGraphic.RefreshFit(_host);
    }
}
