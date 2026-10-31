using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MH.Multiplayer;

/// <summary>Post-final celebration. Prize amount is display-only; economy stays on the backend.</summary>
public class TournamentChampionOverlay : MonoBehaviour
{
    static TournamentChampionOverlay _instance;
    TextMeshProUGUI _prize;
    TextMeshProUGUI _title;
    TextMeshProUGUI _name;

    public static void Show(int prizeTrucoins, string tournamentName = null, bool paymentConfirmed = false)
    {
        Ensure().Display(prizeTrucoins, tournamentName, paymentConfirmed);
    }
    public static void ShowAuthoritativePayout(int actualAmount, string tournamentName, bool paid, string payoutId, string payoutStatus = "pending")
    {
        var overlay = Ensure();
        overlay.Display(0, tournamentName, false);
        bool confirmed = paid && actualAmount >= 0 && !string.IsNullOrWhiteSpace(payoutId);
        overlay._prize.text = confirmed ? "Pago confirmado por el servidor:\n<size=52><color=#24734D>"
            + actualAmount + " TRUCOINS</color></size>" : payoutStatus == "not_due" ? "Campeón confirmado\nEl servidor confirmó que no corresponde un pago."
            : "Campeón confirmado\nPago pendiente de confirmación del servidor";
    }

    public static void Hide()
    {
        if (_instance != null) _instance.gameObject.SetActive(false);
    }

    static TournamentChampionOverlay Ensure()
    {
        if (_instance != null) return _instance;
        var go = new GameObject("TournamentChampionOverlay");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<TournamentChampionOverlay>();
        _instance.Build();
        return _instance;
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = TrucoCanvasOrder.Champion;
        gameObject.AddComponent<GraphicRaycaster>();
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        var dim = Child("Dim", transform);
        Stretch(dim);
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = new Color(0.05f, 0.03f, 0.02f, 0.82f);

        var panel = Child("Panel", dim);
        var prt = panel.GetComponent<RectTransform>();
        Place(prt, .06f, .055f, .94f, .945f);
        var pImg = panel.gameObject.AddComponent<Image>();
        TrucoMenuTheme.Surface(pImg, TrucoClubUI.Surface);
        var edge = panel.gameObject.AddComponent<Outline>();
        edge.effectColor = new Color(.75f, .57f, .30f, .6f); edge.effectDistance = new Vector2(2, -2);

        var hero = Icon("Trophy", panel, TrucoClubUI.Hero != null ? TrucoClubUI.Hero : TrucoGameplayUiSprites.IconTrophy,
            Vector2.zero, Vector2.one);
        Place(hero, .06f, .51f, .94f, .82f);
        hero.GetComponent<Image>().type = Image.Type.Simple;

        _title = Label("Title", panel, Vector2.zero, Vector2.one, 58f, FontStyles.Bold,
            TrucoMenuTheme.Gold, "¡CAMPEÓN DEL TORNEO!");
        Place(_title.rectTransform, .07f, .84f, .93f, .97f);
        var prizeCard = Child("PrizeCard", panel);
        Place(prizeCard.GetComponent<RectTransform>(), .07f, .235f, .93f, .425f);
        TrucoMenuTheme.Surface(prizeCard.gameObject.AddComponent<Image>(), TrucoMenuTheme.Card);
        _prize = Label("Prize", prizeCard, Vector2.zero, Vector2.one, 36f, FontStyles.Bold,
            TrucoMenuTheme.Ink, "", stretch: true);
        _prize.rectTransform.offsetMin = new Vector2(24, 16); _prize.rectTransform.offsetMax = new Vector2(-24, -16);
        _name = Label("Name", panel, Vector2.zero, Vector2.one, 34f, FontStyles.Bold,
            TrucoMenuTheme.Gold, "");
        Place(_name.rectTransform, .07f, .435f, .93f, .505f);
        var thanks = Label("Thanks", panel, Vector2.zero, Vector2.one, 27f, FontStyles.Normal,
            TrucoClubUI.Muted, "¡Gracias por demostrar\ntu habilidad en el truco!");
        Place(thanks.rectTransform, .10f, .135f, .90f, .22f);

        var btnGo = Child("Volver", panel);
        var brt = btnGo.GetComponent<RectTransform>();
        Place(brt, .08f, .035f, .92f, .115f);
        var bImg = btnGo.gameObject.AddComponent<Image>();
        TrucoMenuTheme.Surface(bImg, TrucoClubUI.Green);
        var btn = btnGo.gameObject.AddComponent<Button>();
        btn.targetGraphic = bImg;
        btn.onClick.AddListener(CloseAndLeave);
        Label("Cap", btnGo, Vector2.zero, Vector2.zero, 32f, FontStyles.Bold, Color.white, "ACEPTAR", stretch: true);
        TrucoMenuTheme.StyleButton(btn);

        gameObject.SetActive(false);
    }

    void Display(int prize, string tournamentName, bool paymentConfirmed)
    {
        if (_title != null) _title.text = "¡CAMPEÓN DEL TORNEO!";
        if (_prize != null)
            // The current receipt confirms payment status but does not return a paid amount.
            // The request's advertised prize must never be presented as the amount received.
            _prize.text = (paymentConfirmed ? "El servidor confirmó el pago.\nPremio previsto:" : "Premio previsto · pago sin confirmar:")
                + "\n<size=52><color=#24734D>" + Mathf.Max(0, prize) + " TRUCOINS</color></size>";
        if (_name != null)
            _name.text = string.IsNullOrEmpty(tournamentName) ? "" : tournamentName;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    void CloseAndLeave()
    {
        gameObject.SetActive(false);
        _ = MultiplayerController.LeaveTournamentByPlayerAsync(() =>
        {
            if (Photon.Pun.PhotonNetwork.IsConnected) Photon.Pun.PhotonNetwork.Disconnect();
            TrucoSceneTransition.Go("MainMenu");
        });
    }

    static Transform Child(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static void Place(RectTransform rt, float x0, float y0, float x1, float y1)
    {
        rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static RectTransform Icon(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size)
    {
        var t = Child(name, parent).GetComponent<RectTransform>();
        t.anchorMin = t.anchorMax = new Vector2(0.5f, 0.5f);
        t.anchoredPosition = pos;
        t.sizeDelta = size;
        var img = t.gameObject.AddComponent<Image>();
        ApplySprite(img, sprite, Color.white);
        img.preserveAspect = true;
        img.raycastTarget = false;
        return t;
    }

    static TextMeshProUGUI Label(string name, Transform parent, Vector2 pos, Vector2 size, float font,
        FontStyles style, Color color, string text, bool stretch = false)
    {
        var t = Child(name, parent).GetComponent<RectTransform>();
        if (stretch)
        {
            t.anchorMin = Vector2.zero;
            t.anchorMax = Vector2.one;
            t.offsetMin = t.offsetMax = Vector2.zero;
        }
        else
        {
            t.anchorMin = t.anchorMax = new Vector2(0.5f, 0.5f);
            t.anchoredPosition = pos;
            t.sizeDelta = size;
        }
        var tmp = t.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = font;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = font * 0.55f;
        tmp.fontSizeMax = font;
        tmp.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
        return tmp;
    }

    static void Stretch(Transform t)
    {
        var rt = t.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void ApplySprite(Image img, Sprite sprite, Color fallback)
    {
        if (sprite != null)
        {
            img.sprite = sprite;
            img.color = Color.white;
            img.type = Image.Type.Sliced;
        }
        else img.color = fallback;
    }
}
