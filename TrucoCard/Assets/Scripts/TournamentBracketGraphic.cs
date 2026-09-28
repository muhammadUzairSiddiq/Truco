using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentation-only knockout tree. Reads results; never decides or submits them.</summary>
public static class TournamentBracketGraphic
{
    static readonly Color Gold = new Color(0.96f, 0.75f, 0.28f);
    public static Vector2 Center(int round, int index, float pitch) =>
        new Vector2(0, 150f + (index + 0.5f) * (1 << round) * pitch);

    public static void Build(RectTransform host, TournamentBracketState state, string localId)
    {
        if (host == null || state == null || state.rounds == null || state.rounds.Count == 0) return;
        bool eight = state.maxPlayers == 8;
        float width = eight ? 286f : 400f;
        float gap = eight ? 68f : 124f;
        float pitch = eight ? 290f : 480f;
        float contentWidth = 32f + state.rounds.Count * width + (state.rounds.Count - 1) * gap;
        host.sizeDelta = new Vector2(contentWidth, 200f + state.rounds[0].Count * pitch);

        // Draw connections first, behind the participant cards.
        for (int r = 0; r < state.rounds.Count - 1; r++)
            foreach (var match in state.rounds[r])
            {
                float x = 16f + r * (width + gap) + width;
                float y = Center(r, match.index, pitch).y;
                float parentY = Center(r + 1, match.index / 2, pitch).y;
                Color color = match.IsFinished ? Gold : new Color(0.55f, 0.39f, 0.14f, 0.65f);
                Line(host, new Vector2(x, y), new Vector2(x + gap / 2f, y), color);
                Line(host, new Vector2(x + gap / 2f, y), new Vector2(x + gap / 2f, parentY), color);
                Line(host, new Vector2(x + gap / 2f, parentY), new Vector2(x + gap, parentY), color);
            }

        for (int r = 0; r < state.rounds.Count; r++)
        {
            float x = 16f + r * (width + gap);
            var heading = Panel(host, "Round_" + r, x, 20f, width, 72f, TrucoGameplayUiSprites.BarBrown);
            Text(heading, "Label", state.RoundTitle(r), new Vector2(12f, 8f), new Vector2(width - 24f, 56f), eight ? 22f : 28f, Gold);
            foreach (var match in state.rounds[r])
            {
                float y = Center(r, match.index, pitch).y;
                if (r == state.rounds.Count - 1)
                {
                    var trophy = Panel(host, "FinalTrophy", x + width / 2f - 42f, y - 196f, 84f, 84f, TrucoGameplayUiSprites.IconTrophy);
                    trophy.GetComponent<Image>().preserveAspect = true;
                }
                var card = Panel(host, "Match_" + r + "_" + match.index, x, y - 110f, width, 220f, TrucoGameplayUiSprites.PanelGrey);
                Slot(card, match.first, match.winnerId, localId, width, 22f);
                Slot(card, match.second, match.winnerId, localId, width, 105f);
                Line(card, new Vector2(22f, 100f), new Vector2(width - 22f, 100f), new Color(0.7f, 0.5f, 0.2f, 0.4f));
                string status = match.IsFinished ? "FINALIZADO" : match.IsReady ? "EMPAREJADO" : "ESPERANDO RIVAL";
                Text(card, "Status", status, new Vector2(12f, 186f), new Vector2(width - 24f, 24f), 16f, Gold);
            }
        }
    }

    static void Slot(RectTransform parent, TournamentBracketState.Slot slot, string winnerId, string localId, float width, float y)
    {
        bool known = slot != null && !string.IsNullOrEmpty(slot.playerId);
        bool won = known && slot.playerId == winnerId;
        string name = known ? slot.playerName : "Por definir";
        if (known && string.IsNullOrWhiteSpace(name)) name = "Jugador";
        var avatar = Panel(parent, "Avatar", 20f, y + 5f, 52f, 52f, TrucoGameplayUiSprites.IconPerson);
        avatar.GetComponent<Image>().preserveAspect = true;
        avatar.GetComponent<Image>().color = known ? Gold : new Color(0.4f, 0.45f, 0.47f);
        Color color = won ? Gold : slot != null && slot.eliminated ? new Color(0.63f, 0.59f, 0.5f) : Color.white;
        var label = Text(parent, "Player", name + (known && slot.playerId == localId ? " (vos)" : ""),
            new Vector2(82f, y), new Vector2(width - 104f, 64f), 25f, color);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.enableAutoSizing = true;
        label.fontSizeMin = 18f;
        label.fontSizeMax = 25f;
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
