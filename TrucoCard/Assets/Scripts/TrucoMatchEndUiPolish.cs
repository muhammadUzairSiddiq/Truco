using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Styles win/lose overlays at runtime using the shared Truco panel theme.</summary>
public static class TrucoMatchEndUiPolish
{
    const string BalanceLabelName = "TrucoEndBalance";

    public static void Apply(GameObject panelRoot, bool won, int entryFee)
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(true);

        var scrim = panelRoot.GetComponent<Image>();
        if (scrim != null) scrim.color = TrucoUiTheme.OverlayScrim;

        var childImages = panelRoot.GetComponentsInChildren<Image>(true);
        RectTransform card = null;
        foreach (var img in childImages)
        {
            if (img == null || img.gameObject == panelRoot) continue;
            card = img.rectTransform;
            StyleCard(card);
            break;
        }
        if (card == null)
            card = FindOrCreateCard(panelRoot.transform);

        foreach (var tmp in panelRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.name == BalanceLabelName) continue;
            tmp.color = won ? Color.white : new Color(0.95f, 0.92f, 0.92f, 1f);
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableAutoSizing = true;
        }

        var title = FindOrCreateText(card, "TrucoEndTitle", 36f);
        title.text = won ? TrucoTextosClient.GanastePartida : TrucoTextosClient.PerdistePartida;
        title.color = won ? TrucoUiTheme.EntryPrizeAccent : new Color(0.95f, 0.55f, 0.5f, 1f);

        int prize = Player1v1MatchExtensions.ComputeOneVsOnePrize(entryFee);
        if (won && prize > 0)
        {
            var prizeLine = FindOrCreateText(card, "TrucoEndPrize", 28f);
            prizeLine.text = string.Format(TrucoTextosClient.GanastePremio, prize);
            prizeLine.color = TrucoUiTheme.EntryPrizeAccent;
        }

        var balanceLine = FindOrCreateText(card, BalanceLabelName, 24f);
        balanceLine.text = FormatBalanceLine();
        balanceLine.color = Color.white;

        StyleChildButtons(card);
        if (card != null) TrucoUiMotion.PopIn(card, 0.38f, 0.8f);
    }

    public static void RefreshBalance(GameObject panelRoot)
    {
        if (panelRoot == null) return;
        var t = panelRoot.GetComponentInChildren<TextMeshProUGUI>(true);
        foreach (var label in panelRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (label != null && label.name == BalanceLabelName)
            {
                label.text = FormatBalanceLine();
                return;
            }
        }
    }

    static string FormatBalanceLine()
    {
        int bal = ApiController.GetSessionUser?.Data?.wallet?.balance ?? 0;
        return TrucoLocalization.IsEnglish
            ? $"Balance: {bal} Trucoins"
            : $"Saldo: {bal} Trucoins";
    }

    static RectTransform FindOrCreateCard(Transform parent)
    {
        var existing = parent.Find("TrucoEndCard");
        if (existing != null) return existing as RectTransform;

        var go = new GameObject("TrucoEndCard", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(720f, 420f);
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }

    static void StyleCard(RectTransform card)
    {
        var img = card.GetComponent<Image>();
        var panel = TrucoUiAssetLoader.Panel;
        if (panel != null)
        {
            img.sprite = panel;
            img.type = Image.Type.Sliced;
            img.color = new Color(1f, 1f, 1f, 0.98f);
        }
        else
        {
            img.color = new Color(0.12f, 0.32f, 0.2f, 0.96f);
        }
    }

    static TextMeshProUGUI FindOrCreateText(RectTransform parent, string name, float fontSize)
    {
        var child = parent.Find(name);
        TextMeshProUGUI tmp;
        if (child != null)
            tmp = child.GetComponent<TextMeshProUGUI>();
        else
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            tmp = go.GetComponent<TextMeshProUGUI>();
        }

        var rt = tmp.rectTransform;
        float yMin = name switch
        {
            "TrucoEndTitle" => 0.62f,
            "TrucoEndPrize" => 0.38f,
            _ => 0.18f
        };
        float yMax = yMin + 0.22f;
        rt.anchorMin = new Vector2(0.08f, yMin);
        rt.anchorMax = new Vector2(0.92f, yMax);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.fontSize = fontSize;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 18f;
        tmp.fontSizeMax = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    static void StyleChildButtons(RectTransform card)
    {
        var green = TrucoUiAssetLoader.GreenButton;
        foreach (var btn in card.GetComponentsInChildren<Button>(true))
        {
            var img = btn.GetComponent<Image>();
            if (img != null && green != null)
            {
                img.sprite = green;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            var label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.color = Color.white;
                label.fontStyle = FontStyles.Bold;
            }
        }
    }
}
