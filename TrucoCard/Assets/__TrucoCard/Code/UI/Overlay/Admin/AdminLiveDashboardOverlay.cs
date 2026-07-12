using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// AdminScene modal: list live 1v1 matches from
/// <see cref="ApiController.FetchLiveActiveMatchesForDashboard"/> (dashboard → /matches → admin/matches),
/// show exact Photon room name, spectate via JoinRoom (same as legacy admin flow).
/// </summary>
[DisallowMultipleComponent]
public class AdminLiveDashboardOverlay : MonoBehaviour
{
    [SerializeField] private int _rowHeight = 108;
    [SerializeField] private Color _panelBg = new Color(0.1f, 0.1f, 0.1f, 0.95f);
    [SerializeField] private Color _dim = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] private Color _buttonBlue = new Color(0.2f, 0.5f, 0.7f, 1f);
    [SerializeField] private Color _buttonGrey = new Color(0.35f, 0.35f, 0.35f, 1f);

    GameObject _root;
    RectTransform _scrollContent;
    readonly List<GameObject> _rows = new List<GameObject>();
    Coroutine _connectRoutine;

    public bool IsOpen => _root != null && _root.activeSelf;

    public void Open()
    {
        EnsureLayout();
        _root.SetActive(true);
        _root.transform.SetAsLastSibling();
        RequestRefresh();
    }

    public void Close()
    {
        if (_root != null) _root.SetActive(false);
    }

    void Awake() => EnsureLayout();

    void EnsureLayout()
    {
        if (_root != null) return;
        if (FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        _root = new GameObject("AdminLiveDashboardModal");
        _root.transform.SetParent(transform, false);
        var rootRt = _root.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;
        _root.AddComponent<Image>().color = _dim;
        _root.GetComponent<Image>().raycastTarget = true;

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        panel.transform.SetParent(_root.transform, false);
        var pRt = panel.GetComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0.5f, 0.5f);
        pRt.anchorMax = new Vector2(0.5f, 0.5f);
        pRt.pivot = new Vector2(0.5f, 0.5f);
        pRt.sizeDelta = new Vector2(920, 1000f);
        var pImg = panel.GetComponent<Image>();
        pImg.color = _panelBg;
        pImg.raycastTarget = true;
        var vlgP = panel.GetComponent<VerticalLayoutGroup>();
        vlgP.padding = new RectOffset(20, 20, 16, 16);
        vlgP.spacing = 10;
        vlgP.childAlignment = TextAnchor.UpperCenter;
        vlgP.childControlWidth = true;
        vlgP.childControlHeight = true;
        vlgP.childForceExpandWidth = true;
        vlgP.childForceExpandHeight = false;
        var leP = panel.GetComponent<LayoutElement>();
        leP.minWidth = 500;
        leP.preferredWidth = 920;
        leP.minHeight = 400;
        leP.preferredHeight = 1000;

        var header = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        header.transform.SetParent(panel.transform, false);
        header.GetComponent<LayoutElement>().minHeight = 48;
        header.GetComponent<LayoutElement>().preferredHeight = 48;
        var hlg = header.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = 12;
        hlg.childForceExpandWidth = false;

        var backBtn = MkPanelButton(header.transform, TrucoTextosClient.AdminVolver, new Vector2(120, 40), _buttonGrey, () => Close());
        backBtn.GetComponent<LayoutElement>().minWidth = 120;

        var title = MkTmp(header.transform, TrucoTextosClient.LiveDashboardTitle, 22, TextAlignmentOptions.MidlineLeft, true);
        title.GetComponent<LayoutElement>().flexibleWidth = 1f;

        var refreshBtn = MkPanelButton(header.transform, TrucoTextosClient.AdminRefreshList, new Vector2(130, 40), _buttonBlue, () => RequestRefresh());
        refreshBtn.GetComponent<LayoutElement>().minWidth = 130;

        var scroll = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask), typeof(LayoutElement));
        scroll.transform.SetParent(panel.transform, false);
        var sl = scroll.GetComponent<LayoutElement>();
        sl.minHeight = 500;
        sl.flexibleHeight = 1f;
        sl.minWidth = 800;
        scroll.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f);
        scroll.GetComponent<Mask>().showMaskGraphic = false;
        var srect = scroll.GetComponent<RectTransform>();
        srect.anchorMin = new Vector2(0, 0);
        srect.anchorMax = new Vector2(1, 1);
        var scomp = scroll.GetComponent<ScrollRect>();
        scomp.horizontal = false;
        scomp.vertical = true;
        scomp.movementType = ScrollRect.MovementType.Clamped;

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scroll.transform, false);
        var vrt = viewport.GetComponent<RectTransform>();
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.sizeDelta = Vector2.zero;
        vrt.anchoredPosition = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.08f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        scomp.viewport = vrt;

        _scrollContent = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter))
            .GetComponent<RectTransform>();
        _scrollContent.SetParent(viewport.transform, false);
        _scrollContent.anchorMin = new Vector2(0, 1);
        _scrollContent.anchorMax = new Vector2(1, 1);
        _scrollContent.pivot = new Vector2(0.5f, 1f);
        _scrollContent.anchoredPosition = Vector2.zero;
        _scrollContent.sizeDelta = new Vector2(0, 0);
        _scrollContent.GetComponent<VerticalLayoutGroup>().spacing = 8;
        _scrollContent.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
        _scrollContent.GetComponent<VerticalLayoutGroup>().childControlWidth = true;
        _scrollContent.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = true;
        _scrollContent.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scomp.content = _scrollContent;

        _root.SetActive(false);
    }

    public async void RequestRefresh()
    {
        if (_scrollContent == null) return;
        if (AppManager.Instance != null) AppManager.Instance.DisplayLoadingUI(TrucoTextosClient.Conectando);
        var list = await ApiController.FetchLiveActiveMatchesForDashboard();
        if (AppManager.Instance != null) AppManager.Instance.HideLoadingUI();
        foreach (var g in _rows)
            if (g != null) Destroy(g);
        _rows.Clear();
        if (list == null) return;
        foreach (var m in list)
        {
            if (m == null || string.IsNullOrEmpty(m._id)) continue;
            _rows.Add(MkRow(m));
        }
    }

    GameObject MkRow(Player1v1Match m)
    {
        var row = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(_scrollContent, false);
        row.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f, 0.9f);
        var le = row.GetComponent<LayoutElement>();
        le.minHeight = _rowHeight;
        le.preferredHeight = _rowHeight;
        var h = row.GetComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleLeft;
        h.padding = new RectOffset(10, 10, 6, 6);
        h.spacing = 8;
        h.childControlWidth = false;
        h.childForceExpandWidth = false;

        string displayName = !string.IsNullOrEmpty(m.name) ? m.name : m._id;
        string photonExact = m.ResolvePhotonRoomName();
        if (string.IsNullOrEmpty(photonExact)) photonExact = "— (sin registrar aún)";

        int stake = m.GetEntryStake();
        int pc = m.GetTrucoPlayerCount();
        string meta = string.Format(TrucoTextosClient.LiveRowMetaTemplate, m.status ?? "—", m.GetTypeLabel(), stake, pc);

        string block = $"<b>{displayName}</b>\n" +
                       $"<size=18>{TrucoTextosClient.AdminPhotonRoomLabel} <color=#7fdfff>{photonExact}</color></size>\n" +
                       $"<size=16>{meta}</size>";

        var info = new GameObject("Info", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        info.transform.SetParent(row.transform, false);
        var tmp = info.GetComponent<TextMeshProUGUI>();
        tmp.richText = true;
        tmp.fontSize = 20;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = block;
        var infLe = info.GetComponent<LayoutElement>();
        infLe.minWidth = 400;
        infLe.flexibleWidth = 1f;
        var infRt = info.GetComponent<RectTransform>();
        infRt.sizeDelta = new Vector2(400, _rowHeight - 4);

        var bSp = MkPanelButton(row.transform, TrucoTextosClient.Spectate, new Vector2(100, 36), _buttonBlue, () => StartSpectate(m));
        bSp.GetComponent<LayoutElement>().minWidth = 100;

        var bFc = MkPanelButton(row.transform, TrucoTextosClient.ForceCloseMatch, new Vector2(100, 36), new Color(0.55f, 0.25f, 0.2f, 1f), () => ForceClose(m));
        bFc.GetComponent<LayoutElement>().minWidth = 100;

        return row;
    }

    void StartSpectate(Player1v1Match m)
    {
        if (_connectRoutine != null) StopCoroutine(_connectRoutine);
        _connectRoutine = StartCoroutine(CoJoinSpectate(m));
    }

    IEnumerator CoJoinSpectate(Player1v1Match m)
    {
        string room = m.ResolvePhotonRoomName();
        if (string.IsNullOrEmpty(room)) room = OneVsOneMatchSession.BuildDefaultPhotonRoomName(m._id);
        SpectatorContext.BeginSpectateSession(room, m._id);
        Close();
        if (string.IsNullOrEmpty(room)) yield break;
        if (AppManager.Instance != null) AppManager.Instance.DisplayNotification(TrucoTextosClient.SpectateEntrando);
        var props = new Hashtable { [PhotonPlayerHelper.SpectatorKey] = true };
        var u = ApiController.GetSessionUser?.Data;
        if (u != null) { props["userId"] = u._id; props["username"] = "sp_" + u.username; }
        PhotonNetwork.NickName = u != null ? "Spectator(" + u.username + ")" : "Spectator";
        PhotonNetwork.AutomaticallySyncScene = true;
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
        float wait = 0f;
        while (PhotonNetwork.InRoom && wait < 3f) { wait += Time.deltaTime; yield return null; }
        if (!PhotonNetwork.IsConnected)
        {
            TrucoPhotonRegionSettings.ApplyToPhoton();
            PhotonNetwork.ConnectUsingSettings();
        }
        float t = 0f;
        while (!PhotonNetwork.IsConnectedAndReady && t < 15f) { t += Time.deltaTime; yield return null; }
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            if (AppManager.Instance != null) AppManager.Instance.DisplayNotification("No se pudo conectar a Photon");
            SpectatorContext.Clear();
            _connectRoutine = null;
            yield break;
        }
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        bool ok = PhotonNetwork.JoinRoom(room);
        if (!ok)
        {
            if (AppManager.Instance != null) AppManager.Instance.DisplayNotification("No se pudo unir a la sala (nombre o lleno)");
            SpectatorContext.Clear();
            _connectRoutine = null;
            yield break;
        }
        t = 0f;
        while (t < 12f && !PhotonNetwork.InRoom) { t += Time.deltaTime; yield return null; }
        if (PhotonNetwork.InRoom && SpectatorContext.IsSpectator
            && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Gameplay")
        {
            Photon.Pun.PhotonNetwork.LoadLevel("Gameplay");
        }
        _connectRoutine = null;
    }

    async void ForceClose(Player1v1Match m)
    {
        if (m == null || string.IsNullOrEmpty(m._id)) return;
        if (AppManager.Instance != null) AppManager.Instance.DisplayLoadingUI("Cerrando partida…");
        bool o = await ApiController.AdminForceCloseMatch1v1(m._id, err => { if (AppManager.Instance != null) AppManager.Instance.DisplayNotification(err); });
        if (AppManager.Instance != null) AppManager.Instance.HideLoadingUI();
        if (o) RequestRefresh();
    }

    static TextMeshProUGUI MkTmp(Transform parent, string text, float size, TextAlignmentOptions align, bool expand)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
        if (expand) go.AddComponent<LayoutElement>().flexibleWidth = 1f;
        return t;
    }

    GameObject MkPanelButton(Transform parent, string label, Vector2 size, Color bg, UnityEngine.Events.UnityAction onClick)
    {
        var bgo = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        bgo.transform.SetParent(parent, false);
        bgo.GetComponent<LayoutElement>().minWidth = size.x;
        bgo.GetComponent<LayoutElement>().minHeight = size.y;
        bgo.GetComponent<LayoutElement>().preferredWidth = size.x;
        bgo.GetComponent<LayoutElement>().preferredHeight = size.y;
        bgo.GetComponent<Image>().color = bg;
        var child = new GameObject("L");
        child.transform.SetParent(bgo.transform, false);
        var tmp = child.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
        tmp.fontSize = 18;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        var lrt = child.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        bgo.GetComponent<Button>().onClick.AddListener(onClick);
        return bgo;
    }
}
