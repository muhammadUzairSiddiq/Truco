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
        canvas.sortingOrder = 58000;
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
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(980f, 1640f);
        var pImg = panel.gameObject.AddComponent<Image>();
        ApplySprite(pImg, TrucoGameplayUiSprites.PanelBrown, Color.white);

        var laurelL = Icon("LaurelL", panel, TrucoGameplayUiSprites.Laurel, new Vector2(-240f, 240f), new Vector2(180f, 280f));
        var laurelR = Icon("LaurelR", panel, TrucoGameplayUiSprites.Laurel, new Vector2(240f, 240f), new Vector2(180f, 280f));
        laurelR.localScale = new Vector3(-1f, 1f, 1f);

        Icon("Trophy", panel, TrucoGameplayUiSprites.IconTrophy, new Vector2(0f, 260f), new Vector2(340f, 360f));

        _title = Label("Title", panel, new Vector2(0f, 600f), new Vector2(840f, 190f), 68f, FontStyles.Bold,
            new Color(1f, 0.86f, 0.38f, 1f), "¡CAMPEÓN DEL TORNEO!");
        _prize = Label("Prize", panel, new Vector2(0f, -130f), new Vector2(820f, 300f), 40f, FontStyles.Bold,
            new Color(1f, 0.95f, 0.82f, 1f), "");
        _name = Label("Name", panel, new Vector2(0f, -370f), new Vector2(840f, 80f), 30f, FontStyles.Normal,
            new Color(0.92f, 0.85f, 0.7f, 1f), "");
        Label("Thanks", panel, new Vector2(0f, -490f), new Vector2(800f, 100f), 28f, FontStyles.Normal,
            new Color(0.92f, 0.85f, 0.7f), "¡Gracias por jugar y demostrar\ntu habilidad en el truco!");

        var btnGo = Child("Volver", panel);
        var brt = btnGo.GetComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.anchoredPosition = new Vector2(0f, 48f);
        brt.sizeDelta = new Vector2(640f, 110f);
        var bImg = btnGo.gameObject.AddComponent<Image>();
        ApplySprite(bImg, TrucoGameplayUiSprites.ButtonGreenLong, Color.white);
        var btn = btnGo.gameObject.AddComponent<Button>();
        btn.targetGraphic = bImg;
        btn.onClick.AddListener(CloseAndLeave);
        Label("Cap", btnGo, Vector2.zero, Vector2.zero, 32f, FontStyles.Bold, Color.white, "ACEPTAR", stretch: true);

        gameObject.SetActive(false);
        _ = laurelL;
    }

    void Display(int prize, string tournamentName, bool paymentConfirmed)
    {
        if (_title != null) _title.text = "¡CAMPEÓN DEL TORNEO!";
        if (_prize != null)
            _prize.text = (paymentConfirmed ? "Campeón, recibiste el premio:" : "Premio pendiente de confirmación:")
                + "\n<size=52><color=#F5D76E>" + Mathf.Max(0, prize) + " TRUCOINS</color></size>";
        if (_name != null)
            _name.text = string.IsNullOrEmpty(tournamentName) ? "" : tournamentName;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    void CloseAndLeave()
    {
        gameObject.SetActive(false);
        MultiplayerController.LeaveTournamentMatchmaking(() =>
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
