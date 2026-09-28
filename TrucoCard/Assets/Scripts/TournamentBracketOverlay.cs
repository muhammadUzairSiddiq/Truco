using System.Collections.Generic;
using MH.Multiplayer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

/// <summary>
/// Live 4/8 player bracket. Display only — winners are recorded from already-decided matches.
/// </summary>
public class TournamentBracketOverlay : MonoBehaviour
{
    static TournamentBracketOverlay _instance;
    Transform _roundsHost;
    TextMeshProUGUI _header;
    TextMeshProUGUI _status;
    Button _leaveBtn;
    bool _spectator;

    public static void Show(bool spectator)
    {
        Ensure().Display(spectator);
    }

    public static void Hide()
    {
        if (_instance != null) _instance.gameObject.SetActive(false);
    }

    public static void RefreshIfOpen()
    {
        if (_instance != null && _instance.gameObject.activeInHierarchy)
            _instance.Rebuild();
    }

    static TournamentBracketOverlay Ensure()
    {
        if (_instance != null) return _instance;
        var go = new GameObject("TournamentBracketOverlay");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<TournamentBracketOverlay>();
        _instance.Build();
        return _instance;
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 56000;
        gameObject.AddComponent<GraphicRaycaster>();
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        var root = new GameObject("Root", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(transform, false);
        var rr = root.GetComponent<RectTransform>();
        rr.anchorMin = Vector2.zero;
        rr.anchorMax = Vector2.one;
        rr.offsetMin = rr.offsetMax = Vector2.zero;
        var bg = root.GetComponent<Image>();
        bg.color = new Color(0.12f, 0.07f, 0.04f, 0.97f);
        if (TrucoGameplayUiSprites.PanelBrown != null)
        {
            bg.sprite = TrucoGameplayUiSprites.PanelBrown;
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;
        }

        var header = new GameObject("Header", typeof(RectTransform), typeof(Image));
        header.transform.SetParent(root.transform, false);
        var hrt = header.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.sizeDelta = new Vector2(0f, 150f);
        var himg = header.GetComponent<Image>();
        if (TrucoGameplayUiSprites.PanelBrown != null)
        {
            himg.sprite = TrucoGameplayUiSprites.PanelBrown;
            himg.color = Color.white;
            himg.type = Image.Type.Sliced;
        }
        else himg.color = new Color(0.28f, 0.18f, 0.1f, 1f);

        var back = MakeIconButton(header.transform, TrucoGameplayUiSprites.Back, new Vector2(70f, -75f), () => Leave());
        _ = back;
        _header = MakeText(header.transform, "Title", new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f),
            new Vector2(0f, 8f), new Vector2(760f, 70f), 34f, "CUADRO DEL TORNEO");
        _status = MakeText(header.transform, "Status", new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.22f),
            new Vector2(0f, 0f), new Vector2(900f, 48f), 22f, "");

        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollGo.transform.SetParent(root.transform, false);
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = Vector2.zero;
        srt.anchorMax = Vector2.one;
        srt.offsetMin = new Vector2(24f, 140f);
        srt.offsetMax = new Vector2(-24f, -166f);
        scrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.12f);
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollGo.transform, false);
        var vrt = viewport.GetComponent<RectTransform>();
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.offsetMin = vrt.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = Color.white;
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var crt = content.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = new Vector2(0f, 0f);
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12, 12, 12, 24);
        vlg.spacing = 18f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        // The knockout tree positions its own columns and connection lines.
        vlg.enabled = false;
        content.GetComponent<ContentSizeFitter>().enabled = false;
        crt.anchorMin = crt.anchorMax = new Vector2(0f, 1f);
        crt.pivot = new Vector2(0f, 1f);
        _roundsHost = content.transform;

        var sr = scrollGo.GetComponent<ScrollRect>();
        sr.viewport = vrt;
        sr.content = crt;
        sr.horizontal = true;
        sr.movementType = ScrollRect.MovementType.Clamped;

        var leaveGo = new GameObject("Leave", typeof(RectTransform), typeof(Image), typeof(Button));
        leaveGo.transform.SetParent(root.transform, false);
        var lrt = leaveGo.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0.5f, 0f);
        lrt.anchorMax = new Vector2(0.5f, 0f);
        lrt.pivot = new Vector2(0.5f, 0f);
        lrt.anchoredPosition = new Vector2(-240f, 28f);
        lrt.sizeDelta = new Vector2(440f, 96f);
        var limg = leaveGo.GetComponent<Image>();
        if (TrucoGameplayUiSprites.ButtonGold != null)
        {
            limg.sprite = TrucoGameplayUiSprites.ButtonGold;
            limg.color = Color.white;
        }
        else limg.color = new Color(0.45f, 0.32f, 0.16f, 1f);
        _leaveBtn = leaveGo.GetComponent<Button>();
        _leaveBtn.targetGraphic = limg;
        _leaveBtn.onClick.AddListener(Leave);
        MakeText(leaveGo.transform, "Cap", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 30f, "VOLVER", true);
        var refresh = Instantiate(leaveGo, root.transform);
        refresh.name = "Refresh";
        refresh.GetComponent<RectTransform>().anchoredPosition = new Vector2(240f, 28f);
        refresh.GetComponent<Image>().sprite = TrucoGameplayUiSprites.ButtonGreenLong;
        refresh.GetComponentInChildren<TextMeshProUGUI>().text = "ACTUALIZAR";
        var refreshButton = refresh.GetComponent<Button>();
        refreshButton.onClick = new Button.ButtonClickedEvent();
        refreshButton.onClick.AddListener(Rebuild);

        gameObject.SetActive(false);
    }

    void Display(bool spectator)
    {
        _spectator = spectator;
        Rebuild();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    void Rebuild()
    {
        if (_roundsHost == null) return;
        for (int i = _roundsHost.childCount - 1; i >= 0; i--)
            Destroy(_roundsHost.GetChild(i).gameObject);

        var req = MultiplayerController.CurrentTournamentRequest;
        var bracket = MultiplayerController.BracketState;
        string name = req != null ? req.TournamentName : "Torneo";
        string flor = req != null && req.WithFlor ? "Con Flor" : "Sin Flor";
        int pts = req != null ? req.TargetScore : 30;
        if (_header != null) _header.text = "TORNEO " + (bracket != null ? bracket.maxPlayers : 4) + " JUGADORES";
        if (_status != null)
        {
            string wait = LocalWaitingCopy(bracket);
            _status.text = name + "  ·  " + flor + "  ·  " + pts + " puntos"
                           + (string.IsNullOrEmpty(wait) ? "" : "\n" + wait);
        }

        if (bracket == null || bracket.rounds == null)
        {
            AddNote("El cuadro se arma cuando el torneo está completo.");
            return;
        }

        string myId = ApiController.GetSessionUser?.Data?._id;
        TournamentBracketGraphic.Build((RectTransform)_roundsHost, bracket, myId);
    }

    string LocalWaitingCopy(TournamentBracketState bracket)
    {
        if (_spectator) return "Eliminado — podés seguir el cuadro. Si salís, deja de mostrarse.";
        if (!PhotonNetwork.InRoom) return "Esperando rival del próximo partido…";
        if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.PlayerCount < 2)
            return "Esperando que el otro ganador termine su partido…";
        if (bracket != null && MultiplayerController.ActiveMatch != null && MultiplayerController.ActiveMatch.round >= 0)
        {
            int r = MultiplayerController.ActiveMatch.round;
            int idx = MultiplayerController.ActiveMatch.matchIndex;
            if (r >= 0 && r < bracket.rounds.Count && idx >= 0 && idx < bracket.rounds[r].Count)
            {
                var m = bracket.rounds[r][idx];
                if (m.IsFinished) return "Ganaste. Esperando al próximo rival…";
            }
        }
        return "";
    }

    void AddRoundTitle(string title)
    {
        var go = new GameObject("Round", typeof(RectTransform), typeof(LayoutElement), typeof(TextMeshProUGUI));
        go.transform.SetParent(_roundsHost, false);
        go.GetComponent<LayoutElement>().preferredHeight = 48f;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = title;
        tmp.fontSize = 26f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(0.98f, 0.84f, 0.42f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;
        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
    }

    void AddMatchCard(TournamentBracketState.BracketMatch match, string myId)
    {
        var go = new GameObject("Match", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(VerticalLayoutGroup));
        go.transform.SetParent(_roundsHost, false);
        go.GetComponent<LayoutElement>().preferredHeight = 150f;
        var img = go.GetComponent<Image>();
        if (TrucoGameplayUiSprites.PanelGreen != null)
        {
            img.sprite = TrucoGameplayUiSprites.PanelGreen;
            img.color = Color.white;
            img.type = Image.Type.Sliced;
        }
        else img.color = new Color(0.12f, 0.28f, 0.16f, 0.95f);
        var v = go.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(28, 28, 18, 18);
        v.spacing = 6f;
        v.childAlignment = TextAnchor.MiddleCenter;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandHeight = false;

        AddSlot(go.transform, match.first, match.winnerId, myId);
        var vs = new GameObject("vs", typeof(RectTransform), typeof(TextMeshProUGUI));
        vs.transform.SetParent(go.transform, false);
        var vst = vs.GetComponent<TextMeshProUGUI>();
        vst.text = "vs";
        vst.fontSize = 18f;
        vst.alignment = TextAlignmentOptions.Center;
        vst.color = new Color(1f, 0.85f, 0.45f, 0.85f);
        AddSlot(go.transform, match.second, match.winnerId, myId);
    }

    void AddSlot(Transform parent, TournamentBracketState.Slot slot, string winnerId, string myId)
    {
        var go = new GameObject("Slot", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        string name = slot != null && !string.IsNullOrEmpty(slot.playerName) ? slot.playerName : "—";
        if (slot == null || string.IsNullOrEmpty(slot.playerId))
            name = "ESPERANDO RIVAL";
        tmp.text = name;
        tmp.fontSize = 26f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 18f;
        tmp.fontSizeMax = 26f;
        bool eliminated = slot != null && slot.eliminated;
        bool winner = slot != null && !string.IsNullOrEmpty(winnerId) && slot.playerId == winnerId;
        bool mine = slot != null && slot.playerId == myId;
        if (winner) tmp.color = new Color(1f, 0.86f, 0.38f, 1f);
        else if (eliminated) tmp.color = new Color(0.75f, 0.68f, 0.58f, 0.55f);
        else if (mine) tmp.color = new Color(0.85f, 1f, 0.85f, 1f);
        else tmp.color = new Color(0.96f, 0.93f, 0.86f, 1f);
        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
    }

    void AddNote(string msg)
    {
        var go = new GameObject("Note", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(_roundsHost, false);
        go.GetComponent<LayoutElement>().preferredHeight = 80f;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(940f, 120f);
        ((RectTransform)_roundsHost).sizeDelta = rect.sizeDelta;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = msg;
        tmp.fontSize = 24f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.92f, 0.86f, 0.74f, 1f);
    }

    void Leave()
    {
        gameObject.SetActive(false);
        MultiplayerController.LeaveTournamentMatchmaking(() =>
        {
            if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
            TrucoSceneTransition.Go("MainMenu");
        });
    }

    static RectTransform MakeIconButton(Transform parent, Sprite sprite, Vector2 pos, UnityEngine.Events.UnityAction click)
    {
        var go = new GameObject("Back", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(92f, 92f);
        var img = go.GetComponent<Image>();
        if (sprite != null) { img.sprite = sprite; img.color = Color.white; }
        else img.color = new Color(0, 0, 0, 0.25f);
        img.preserveAspect = true;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(click);
        return rt;
    }

    static TextMeshProUGUI MakeText(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size,
        float font, string text, bool stretch = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        if (stretch)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        else
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = font;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(1f, 0.9f, 0.55f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
        return tmp;
    }
}
