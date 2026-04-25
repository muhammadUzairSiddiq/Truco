using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Runtime-built Live Dashboard in Admin scene: list matches, optional fraud, spectate, force-close.</summary>
public class AdminLiveDashboardRuntime : MonoBehaviour
{
    [SerializeField] private int _rowHeight = 110;

    RectTransform _content;
    List<GameObject> _rows = new List<GameObject>();
    Text _fraudText;
    Coroutine _connectRoutine;

    void Awake()
    {
        if (Object.FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var cgo = new GameObject("AdminDashboardCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            cgo.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        }
        var root = new GameObject("AdminLiveRoot", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(1f, 0f);
        rootRt.anchorMax = new Vector2(1f, 1f);
        rootRt.pivot = new Vector2(1f, 0.5f);
        rootRt.anchoredPosition = Vector2.zero;
        rootRt.sizeDelta = new Vector2(400, 0f);
        rootRt.offsetMin = new Vector2(-420, 20);
        rootRt.offsetMax = new Vector2(-20, -20);
        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.12f, 0.15f, 0.95f);

        var title = MkText("Title", root.transform, TrucoTextosClient.LiveDashboardTitle, 16, TextAnchor.UpperLeft);
        var titleRt = title.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0, 1);
        titleRt.anchorMax = new Vector2(1, 1);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0, -8);
        titleRt.sizeDelta = new Vector2(0, 36);

        var refresh = MkButton("Refresh", root.transform, "Refresh list", new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -50), new Vector2(150, 32), () => RequestRefresh());
        var fraudBtn = MkButton("Fraud", root.transform, TrucoTextosClient.LiveDashboardFraud, new Vector2(0, 1), new Vector2(0, 1), new Vector2(170, -50), new Vector2(200, 32), OnFraud);
        _fraudText = MkText("FraudText", root.transform, "—", 11, TextAnchor.UpperLeft);
        var fr = _fraudText.GetComponent<RectTransform>();
        fr.anchorMin = new Vector2(0, 1);
        fr.anchorMax = new Vector2(1, 1);
        fr.pivot = new Vector2(0.5f, 1f);
        fr.anchoredPosition = new Vector2(0, -90);
        fr.sizeDelta = new Vector2(-20, 50);

        var scroll = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
        scroll.transform.SetParent(root.transform, false);
        var srt = scroll.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0, 0);
        srt.anchorMax = new Vector2(1, 1);
        srt.offsetMin = new Vector2(8, 12);
        srt.offsetMax = new Vector2(-8, -150);
        scroll.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f);
        var scrollComp = scroll.GetComponent<ScrollRect>();
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scroll.transform, false);
        var vrt = viewport.GetComponent<RectTransform>();
        vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one; vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.08f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        _content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
        _content.SetParent(viewport.transform, false);
        _content.anchorMin = new Vector2(0, 1);
        _content.anchorMax = new Vector2(1, 1);
        _content.pivot = new Vector2(0.5f, 1f);
        _content.offsetMin = new Vector2(0, 0);
        _content.offsetMax = new Vector2(0, 0);
        _content.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
        _content.GetComponent<VerticalLayoutGroup>().spacing = 4;
        _content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollComp.content = _content;
        scrollComp.viewport = vrt;
        scrollComp.vertical = true;
    }

    void Start() => RequestRefresh();

    void OnFraud() => FetchFraud();

    async void FetchFraud()
    {
        string raw = await ApiController.FetchFraudAlertsSummaryRaw();
        if (_fraudText != null) _fraudText.text = raw != null && raw.Length > 400 ? raw.Substring(0, 400) + "…" : raw ?? "—";
    }

    async void RequestRefresh()
    {
        if (AppManager.Instance != null) AppManager.Instance.DisplayLoadingUI("Loading matches…");
        var list = await ApiController.FetchLiveActiveMatchesForDashboard();
        if (AppManager.Instance != null) AppManager.Instance.HideLoadingUI();
        foreach (var g in _rows) if (g) Destroy(g);
        _rows.Clear();
        if (list == null) return;
        foreach (var m in list)
        {
            _rows.Add(MkRow(m));
        }
    }

    GameObject MkRow(Player1v1Match m)
    {
        var row = new GameObject("Row", typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(_content, false);
        var le = row.GetComponent<LayoutElement>();
        le.minHeight = _rowHeight;
        le.preferredHeight = _rowHeight;
        var h = row.GetComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleLeft;
        h.spacing = 4;
        h.padding = new RectOffset(4, 4, 2, 2);

        string n = (m.name != null && m.name.Length > 0) ? m.name : m._id;
        var info = MkText("info", row.transform, $"{n}\nFee:{m.entryFee} Pub:{(m.isPublic ? "Y" : "N")} St:{m.status}\nP:{m.ResolvePhotonRoomName()}", 11, TextAnchor.MiddleLeft);
        var irt = info.GetComponent<RectTransform>();
        irt.sizeDelta = new Vector2(180, 100);
        var flex = info.gameObject.AddComponent<LayoutElement>();
        flex.flexibleWidth = 1f;
        flex.minWidth = 100f;

        var b1 = MkButton("Sp", row.transform, TrucoTextosClient.Spectate, null, null, new Vector2(0, 0), new Vector2(90, 28), () => StartSpectate(m));
        var b2 = MkButton("Fc", row.transform, "Close", null, null, new Vector2(0, 0), new Vector2(70, 28), () => ForceClose(m));
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
        if (string.IsNullOrEmpty(room)) yield break;
        if (AppManager.Instance != null) AppManager.Instance.DisplayNotification("Joining as spectator…");
        var props = new Hashtable { [PhotonPlayerHelper.SpectatorKey] = true };
        var u = ApiController.GetSessionUser?.Data;
        if (u != null) { props["userId"] = u._id; props["username"] = "sp_" + u.username; }
        PhotonNetwork.NickName = u != null ? "Spectator(" + u.username + ")" : "Spectator";
        PhotonNetwork.AutomaticallySyncScene = true;
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
        float wait = 0f;
        while (PhotonNetwork.InRoom && wait < 3f) { wait += Time.deltaTime; yield return null; }
        if (!PhotonNetwork.IsConnected) PhotonNetwork.ConnectUsingSettings();
        float t = 0f;
        while (!PhotonNetwork.IsConnectedAndReady && t < 15f) { t += Time.deltaTime; yield return null; }
        if (!PhotonNetwork.IsConnectedAndReady) { if (AppManager.Instance != null) AppManager.Instance.DisplayNotification("Could not connect to Photon"); SpectatorContext.Clear(); _connectRoutine = null; yield break; }
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        bool ok = PhotonNetwork.JoinRoom(room);
        if (!ok) { if (AppManager.Instance != null) AppManager.Instance.DisplayNotification("Join room failed (name / capacity)"); SpectatorContext.Clear(); }
        _connectRoutine = null;
    }

    async void ForceClose(Player1v1Match m)
    {
        if (m == null || string.IsNullOrEmpty(m._id)) return;
        if (AppManager.Instance != null) AppManager.Instance.DisplayLoadingUI("Closing match…");
        bool ok = await ApiController.AdminForceCloseMatch1v1(m._id, err => { if (AppManager.Instance != null) AppManager.Instance.DisplayNotification(err); });
        if (AppManager.Instance != null) AppManager.Instance.HideLoadingUI();
        if (ok) RequestRefresh();
    }

    static Text MkText(string name, Transform parent, string str, int size, TextAnchor a)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var tx = go.GetComponent<Text>();
        tx.text = str;
        tx.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        tx.fontSize = size;
        tx.alignment = a;
        tx.color = Color.white;
        tx.horizontalOverflow = HorizontalWrapMode.Wrap;
        return tx;
    }

    static GameObject MkButton(string name, Transform parent, string label, Vector2? aMin, Vector2? aMax, Vector2? pos, Vector2? size, UnityEngine.Events.UnityAction onClick)
    {
        var bgo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        bgo.transform.SetParent(parent, false);
        var brt = bgo.GetComponent<RectTransform>();
        if (aMin != null) { brt.anchorMin = aMin.Value; brt.anchorMax = aMax.Value; if (pos != null) brt.anchoredPosition = pos.Value; if (size != null) brt.sizeDelta = size.Value; }
        else
        {
            var le = bgo.AddComponent<LayoutElement>();
            le.minWidth = size?.x ?? 80;
            le.minHeight = size?.y ?? 28;
        }
        bgo.GetComponent<Image>().color = new Color(0.2f, 0.5f, 0.7f);
        var lab = new GameObject("L");
        lab.transform.SetParent(bgo.transform, false);
        var t = lab.AddComponent<Text>();
        t.text = label;
        t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize = 12;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        var lrt = lab.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        bgo.GetComponent<Button>().onClick.AddListener(onClick);
        return bgo;
    }
}
