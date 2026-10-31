using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Admin V3.0 Unity panel: live counts, connection/fraud alerts, kill switch (double confirm),
/// force-close tournament, manual champion/prize, audit log. Backend URLs are empty-slot overridable.
/// </summary>
[DisallowMultipleComponent]
public class AdminControlCenterOverlay : MonoBehaviour
{
    GameObject _root;
    TextMeshProUGUI _stats;
    TextMeshProUGUI _alerts;
    TextMeshProUGUI _audit;
    TMP_InputField _maintMsg;
    TMP_InputField _forceCloseId;
    TMP_InputField _winnerUserId;
    TMP_InputField _prizeUserId;
    TMP_InputField _prizeAmount;
    TMP_InputField _prizeReason;
    Toggle _blockGame;
    Toggle _blockTables;
    Toggle _blockTournaments;
    Toggle _blockTx;
    bool _built;
    bool _refreshing;
    bool _auditRefreshing;
    string _auditCursor, _auditNextCursor;
    readonly Stack<string> _auditPrevious = new Stack<string>();
    Button _auditPreviousButton, _auditNextButton;
    float _nextRefresh;

    void Update()
    {
        if (!IsOpen || _refreshing || Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + 15f;
        _ = RefreshAsync(false);
    }

    public bool IsOpen => _root != null && _root.activeSelf;

    public void Open()
    {
        EnsureLayout();
        _root.SetActive(true);
        _root.transform.SetAsLastSibling();
        SyncTogglesFromState();
        _ = RefreshAsync();
    }

    public void Close()
    {
        if (_root != null) _root.SetActive(false);
    }

    void Awake() => EnsureLayout();

    void EnsureLayout()
    {
        if (_built) return;
        _built = true;
        TrucoEventSystem.Ensure();

        _root = new GameObject("AdminControlCenterModal");
        _root.transform.SetParent(transform, false);
        Stretch(_root.AddComponent<RectTransform>());
        _root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

        var panel = Panel("Panel", _root.transform, new Vector2(980, 1280));
        var header = Row(panel.transform, 48);
        MkBtn(header.transform, TrucoTextosClient.AdminVolver, new Vector2(120, 40), new Color(0.35f, 0.35f, 0.35f), Close);
        MkTmp(header.transform, TrucoTextosClient.AdminControlTitle, 22, true);
        MkBtn(header.transform, TrucoTextosClient.AdminRefreshList, new Vector2(140, 40), new Color(0.2f, 0.5f, 0.7f), () => _ = RefreshAsync());
        MkBtn(panel.transform, "Solicitudes de compra / venta de Trucoins", new Vector2(640, 48), TrucoClubUI.Green, TrucoWalletAdminScreen.Show);

        _stats = MkTmp(panel.transform, "…", 18, false, 86);
        _alerts = MkTmp(panel.transform, "", 16, false, 90);

        MkTmp(panel.transform, "Interruptor de emergencia / mantenimiento", 18, false, 28);
        _blockGame = MkToggle(panel.transform, "Bloquear todo el juego");
        _blockTables = MkToggle(panel.transform, "Bloquear mesas 1v1");
        _blockTournaments = MkToggle(panel.transform, "Bloquear torneos");
        _blockTx = MkToggle(panel.transform, "Bloquear transacciones");
        _maintMsg = MkInput(panel.transform, "Mensaje para jugadores");
        var killRow = Row(panel.transform, 48);
        MkBtn(killRow.transform, "ACTIVAR", new Vector2(200, 42), new Color(0.7f, 0.18f, 0.16f), ConfirmEnableKillSwitch);
        MkBtn(killRow.transform, "DESACTIVAR", new Vector2(200, 42), new Color(0.2f, 0.5f, 0.28f), ConfirmDisableKillSwitch);

        MkTmp(panel.transform, "Cierre forzado de torneo (id)", 16, false, 24);
        _forceCloseId = MkInput(panel.transform, "id del torneo");
        MkBtn(panel.transform, "Forzar cierre del torneo", new Vector2(320, 40), new Color(0.55f, 0.25f, 0.2f), ConfirmForceClose);

        MkTmp(panel.transform, "Campeón excepcional (userId)", 16, false, 24);
        _winnerUserId = MkInput(panel.transform, "userId del campeón");
        MkBtn(panel.transform, "Fijar campeón (usa id de cierre)", new Vector2(360, 40), new Color(0.35f, 0.4f, 0.2f), ConfirmSetChampion);

        MkTmp(panel.transform, "Premio excepcional (userId + Trucoins)", 16, false, 24);
        _prizeUserId = MkInput(panel.transform, "userId");
        _prizeAmount = MkInput(panel.transform, "cantidad");
        _prizeReason = MkInput(panel.transform, "motivo (auditoría)");
        MkBtn(panel.transform, "Acreditar premio", new Vector2(240, 40), new Color(0.55f, 0.42f, 0.12f), ConfirmCreditPrize);

        MkTmp(panel.transform, "Auditoría confirmada por el servidor", 16, false, 24);
        _audit = MkTmp(panel.transform, "Actualizá para consultar la auditoría del servidor.", 15, false, 160);
        var auditPages = Row(panel.transform, 44);
        _auditPreviousButton = MkBtn(auditPages.transform, "ANTERIOR", new Vector2(180, 40), TrucoClubUI.Surface, () => _ = MoveAudit(false));
        _auditNextButton = MkBtn(auditPages.transform, "SIGUIENTE", new Vector2(180, 40), TrucoClubUI.Green, () => _ = MoveAudit(true));
        UpdateAuditNavigation();
        MakeControlsScrollable(panel);

        _root.SetActive(false);
    }

    async System.Threading.Tasks.Task RefreshAsync(bool showLoading = true)
    {
        if (_refreshing) return;
        _refreshing = true;
        _nextRefresh = Time.unscaledTime + 15f;
        try
        {
        if (showLoading && AppManager.Instance != null) AppManager.Instance.DisplayLoadingUI(TrucoTextosClient.Conectando);
        int photonPlayers = Photon.Pun.PhotonNetwork.CountOfPlayers;
        int rooms = Photon.Pun.PhotonNetwork.CountOfRooms;
        var matches = await ApiController.FetchLiveActiveMatchesForDashboard();
        var tournaments = await ApiController.GetAllTournamentsAdmin();
        string fraud = await ApiController.FetchFraudAlertsSummaryRaw();
        string reconnect = await ApiController.FetchReconnectAlertsRaw();
        string onlineRaw = await ApiController.FetchDashboardOnlinePlayersRaw();
        if (_stats == null || _alerts == null || _audit == null) return;

        int matchCount = matches != null ? matches.Count : 0;
        int tourneyCount = tournaments != null ? tournaments.Count : 0;
        int liveTourney = 0;
        if (tournaments != null)
        {
            for (int i = 0; i < tournaments.Count; i++)
            {
                var t = tournaments[i];
                if (t == null) continue;
                string st = (t.status ?? "").ToLowerInvariant();
                if (st.Contains("active") || st.Contains("live") || st.Contains("in_progress") || st.Contains("open"))
                    liveTourney++;
            }
        }

        _stats.text =
            "<b>Panel en vivo</b>\n" +
            "Jugadores Photon: " + photonPlayers + "   ·   Salas: " + rooms + "\n" +
            "Partidas 1v1 activas: " + matchCount + "   ·   Torneos listados: " + tourneyCount +
            " (en curso ~" + liveTourney + ")\n" +
            "Conexión Photon: " + (Photon.Pun.PhotonNetwork.IsConnectedAndReady ? "lista" : "desconectada") +
            "   ·   Mantenimiento: " + (TrucoMaintenanceMode.IsActive ? "ACTIVO" : "inactivo") +
            (string.IsNullOrEmpty(onlineRaw) ? "" : "\nOnline API: " + Trunc(onlineRaw, 160));

        _alerts.text =
            "<b>Alertas reconexión / fraude</b>\n" +
            Trunc(reconnect, 220) + "\n" +
            Trunc(fraud, 220);

        await RefreshAudit(_auditCursor);
        SyncTogglesFromState();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Admin dashboard] Refresh failed: " + ex.Message);
            if (_stats != null) _stats.text = "No se pudo actualizar el panel. Datos no confirmados.";
        }
        finally
        {
            _refreshing = false;
            if (showLoading) AppManager.Instance?.HideLoadingUI();
        }
    }

    async System.Threading.Tasks.Task<bool> RefreshAudit(string cursor)
    {
        if (_auditRefreshing || _audit == null) return false;
        _auditRefreshing = true; _audit.text = "Consultando la auditoría del servidor…"; UpdateAuditNavigation();
        try
        {
            var page = await TrucoAdminAuditService.List(cursor, 5);
            if (this == null || _audit == null) return false;
            _auditCursor = cursor; _auditNextCursor = page.nextCursor;
            var text = new System.Text.StringBuilder();
            foreach (var entry in page.items)
            {
                text.Append(entry.at).Append(" · ").Append(entry.actorId).Append(" · ").Append(entry.action);
                if (!string.IsNullOrEmpty(entry.targetId)) text.Append(" · ").Append(entry.targetType).Append(": ").Append(entry.targetId);
                if (!string.IsNullOrEmpty(entry.reason)) text.Append("\n").Append(entry.reason);
                if (!string.IsNullOrEmpty(entry.detail)) text.Append("\n").Append(entry.detail);
                text.Append('\n');
            }
            _audit.text = text.Length == 0 ? "No hay eventos de auditoría en esta página." : text.ToString();
            _audit.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(160, _audit.GetPreferredValues(_audit.text, 900, 0).y + 12);
            return true;
        }
        catch (Exception exception)
        {
            if (this != null && _audit != null) _audit.text = "No se pudo confirmar la auditoría del servidor. " + exception.Message;
            return false;
        }
        finally { _auditRefreshing = false; if (this != null) UpdateAuditNavigation(); }
    }
    async System.Threading.Tasks.Task MoveAudit(bool next)
    {
        if (_auditRefreshing || next && string.IsNullOrEmpty(_auditNextCursor) || !next && _auditPrevious.Count == 0) return;
        string previous = _auditCursor, destination = next ? _auditNextCursor : _auditPrevious.Peek();
        if (!await RefreshAudit(destination)) return;
        if (next) _auditPrevious.Push(previous); else _auditPrevious.Pop();
        UpdateAuditNavigation();
    }
    void UpdateAuditNavigation()
    {
        if (_auditPreviousButton != null) _auditPreviousButton.interactable = !_auditRefreshing && _auditPrevious.Count > 0;
        if (_auditNextButton != null) _auditNextButton.interactable = !_auditRefreshing && !string.IsNullOrEmpty(_auditNextCursor);
    }
    static void MakeControlsScrollable(GameObject panel)
    {
        var originalLayout = panel.GetComponent<VerticalLayoutGroup>(); originalLayout.enabled = false;
        var controls = new List<Transform>(); foreach (Transform child in panel.transform) controls.Add(child);
        var scrollRoot = TrucoClubUI.Rect("ControlsScroll", panel.transform, Vector2.zero, Vector2.one);
        var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        var viewport = TrucoClubUI.Rect("Viewport", scrollRoot, Vector2.zero, Vector2.one); viewport.gameObject.AddComponent<RectMask2D>();
        var content = TrucoClubUI.Rect("Controls", viewport, new Vector2(0, 1), new Vector2(1, 1)); content.pivot = new Vector2(.5f, 1);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = originalLayout.padding; layout.spacing = originalLayout.spacing; layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        foreach (var child in controls) child.SetParent(content, false);
        scroll.content = content; scroll.viewport = viewport; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
    }

    void SyncTogglesFromState()
    {
        var s = TrucoMaintenanceMode.Current;
        if (_blockGame != null) _blockGame.isOn = s.blockGame;
        if (_blockTables != null) _blockTables.isOn = s.blockTables;
        if (_blockTournaments != null) _blockTournaments.isOn = s.blockTournaments;
        if (_blockTx != null) _blockTx.isOn = s.blockTransactions;
        if (_maintMsg != null && string.IsNullOrEmpty(_maintMsg.text))
            _maintMsg.text = s.message;
    }

    TrucoMaintenanceMode.State CaptureState(bool active)
    {
        return new TrucoMaintenanceMode.State
        {
            active = active,
            blockGame = _blockGame != null && _blockGame.isOn,
            blockTables = _blockTables != null && _blockTables.isOn,
            blockTournaments = _blockTournaments != null && _blockTournaments.isOn,
            blockTransactions = _blockTx != null && _blockTx.isOn,
            message = _maintMsg != null && !string.IsNullOrWhiteSpace(_maintMsg.text)
                ? _maintMsg.text.Trim()
                : "El juego está en mantenimiento. Volvé más tarde.",
            updatedAt = DateTime.UtcNow.ToString("o"),
            updatedBy = ApiController.GetSessionUser?.Data?.username ?? "admin"
        };
    }

    void ConfirmEnableKillSwitch()
    {
        TrucoConfirmDialog.Show(
            "Interruptor de emergencia",
            "Esto bloqueará el juego según las casillas. Los jugadores verán el mensaje de mantenimiento.",
            "NO", "SÍ",
            () =>
            {
                TrucoConfirmDialog.Show(
                    "Confirmar otra vez",
                    "¿Activar el kill switch? Acción auditada.",
                    "NO", "ACTIVAR",
                    () => _ = ApplyKillSwitch(true));
            });
    }

    void ConfirmDisableKillSwitch()
    {
        TrucoConfirmDialog.Show(
            "Quitar mantenimiento",
            "¿Desactivar el bloqueo y volver a abrir mesas, torneos y transacciones?",
            "NO", "SÍ",
            () => _ = ApplyKillSwitch(false));
    }

    async System.Threading.Tasks.Task ApplyKillSwitch(bool on)
    {
        var state = CaptureState(on);
        bool pushed = await TrucoMaintenanceMode.PushToServerAsync(state);
        // PushToServerAsync applies only the canonical confirmed server state.
        if (pushed)
        {
            SyncTogglesFromState();
            if (_maintMsg != null) _maintMsg.text = TrucoMaintenanceMode.Message;
        }
        var actual = pushed ? TrucoMaintenanceMode.Current : state;
        TrucoAdminAuditLog.Record(pushed ? actual.active ? "KILL_SWITCH_ON" : "KILL_SWITCH_OFF" : "KILL_SWITCH_REJECTED",
            "game=" + actual.blockGame + " tables=" + actual.blockTables + " tourneys=" + actual.blockTournaments
            + " tx=" + actual.blockTransactions + " api=" + pushed);
        AppManager.Instance?.DisplayNotification(!pushed
            ? "El servidor no confirmó el cambio. Mantenimiento sin cambios."
            : actual.active ? "Mantenimiento activo confirmado por el servidor." : "Mantenimiento desactivado y confirmado por el servidor.");
        await RefreshAsync();
    }

    void ConfirmForceClose()
    {
        string id = _forceCloseId != null ? _forceCloseId.text.Trim() : "";
        if (string.IsNullOrEmpty(id))
        {
            AppManager.Instance?.DisplayNotification("Ingresá el id del torneo.");
            return;
        }
        TrucoConfirmDialog.Show(
            "Forzar cierre",
            "¿Cerrar el torneo " + id + "? Acción excepcional y auditada.",
            "NO", "CERRAR",
            () => _ = ForceCloseTournament(id));
    }

    async System.Threading.Tasks.Task ForceCloseTournament(string id)
    {
        if (AppManager.Instance != null) AppManager.Instance.DisplayLoadingUI("Cerrando torneo…");
        bool ok = await ApiController.AdminForceCloseTournament(id);
        if (AppManager.Instance != null) AppManager.Instance.HideLoadingUI();
        TrucoAdminAuditLog.Record("FORCE_CLOSE_TOURNAMENT", id + " ok=" + ok);
        AppManager.Instance?.DisplayNotification(ok ? "Cierre y reintegros confirmados." : "Cierre sin confirmar. Revisá la auditoría o reintentá la misma acción.");
        await RefreshAsync();
    }

    void ConfirmSetChampion()
    {
        string tid = _forceCloseId != null ? _forceCloseId.text.Trim() : "";
        string uid = _winnerUserId != null ? _winnerUserId.text.Trim() : "";
        if (string.IsNullOrEmpty(tid) || string.IsNullOrEmpty(uid))
        {
            AppManager.Instance?.DisplayNotification("Hace falta id de torneo y userId del campeón.");
            return;
        }
        TrucoConfirmDialog.Show(
            "Campeón excepcional",
            "¿Fijar campeón " + uid + " en torneo " + tid + "?",
            "NO", "SÍ",
            () => _ = SetChampion(tid, uid));
    }

    async System.Threading.Tasks.Task SetChampion(string tid, string uid)
    {
        bool ok = await ApiController.FinalizeTournament(tid, uid);
        TrucoAdminAuditLog.Record("MANUAL_CHAMPION", tid + " → " + uid + " ok=" + ok);
        AppManager.Instance?.DisplayNotification(ok ? "Campeón confirmado por el servidor." : "Campeón sin confirmar. Revisá la auditoría o reintentá la misma acción.");
        await RefreshAsync();
    }

    void ConfirmCreditPrize()
    {
        string uid = _prizeUserId != null ? _prizeUserId.text.Trim() : "";
        if (string.IsNullOrEmpty(uid) || _prizeAmount == null || !int.TryParse(_prizeAmount.text.Trim(), out int amount) || amount <= 0)
        {
            AppManager.Instance?.DisplayNotification("UserId y cantidad válida de Trucoins.");
            return;
        }
        string reason = _prizeReason != null ? _prizeReason.text.Trim() : "premio excepcional";
        TrucoConfirmDialog.Show(
            "Premio excepcional",
            "¿Acreditar " + amount + " Trucoins a " + uid + "?",
            "NO", "ACREDITAR",
            () => _ = Credit(uid, amount, reason));
    }

    async System.Threading.Tasks.Task Credit(string uid, int amount, string reason)
    {
        bool ok = await ApiController.AdminCreditUserCoins(uid, amount, reason);
        TrucoAdminAuditLog.Record("MANUAL_PRIZE", uid + " +" + amount + " " + reason + " ok=" + ok);
        AppManager.Instance?.DisplayNotification(ok ? "Crédito confirmado por el servidor." : "Crédito sin confirmar. Revisá la auditoría o reintentá con los mismos datos.");
        await RefreshAsync();
    }

    static string Trunc(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "—";
        s = s.Replace("\r", " ").Replace("\n", " ");
        return s.Length <= max ? s : s.Substring(0, max) + "…";
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static GameObject Panel(string name, Transform parent, Vector2 size)
    {
        var panel = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(parent, false);
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        panel.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 0.97f);
        var v = panel.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(18, 18, 14, 14);
        v.spacing = 6;
        v.childControlWidth = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        return panel;
    }

    static GameObject Row(Transform parent, float h)
    {
        var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().minHeight = h;
        go.GetComponent<LayoutElement>().preferredHeight = h;
        var hlg = go.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        return go;
    }

    static TextMeshProUGUI MkTmp(Transform parent, string text, float size, bool expand, float minH = 36)
    {
        var go = new GameObject("T", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().minHeight = minH;
        go.GetComponent<LayoutElement>().preferredHeight = minH;
        if (expand) go.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = Color.white;
        t.enableWordWrapping = true;
        t.overflowMode = TextOverflowModes.Truncate;
        if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
        return t;
    }

    static Button MkBtn(Transform parent, string label, Vector2 size, Color bg, UnityEngine.Events.UnityAction click)
    {
        var go = new GameObject("B", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var le = go.GetComponent<LayoutElement>();
        le.minWidth = size.x; le.minHeight = size.y;
        le.preferredWidth = size.x; le.preferredHeight = size.y;
        go.GetComponent<Image>().color = bg;
        var child = new GameObject("L");
        child.transform.SetParent(go.transform, false);
        var tmp = child.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 16;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
        var lrt = child.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        go.GetComponent<Button>().onClick.AddListener(click);
        return go.GetComponent<Button>();
    }

    static Toggle MkToggle(Transform parent, string label)
    {
        var row = Row(parent, 36);
        var go = new GameObject("Tg", typeof(RectTransform), typeof(Toggle), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(row.transform, false);
        go.GetComponent<LayoutElement>().minWidth = 36;
        go.GetComponent<LayoutElement>().minHeight = 36;
        go.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.28f);
        var tg = go.GetComponent<Toggle>();
        tg.targetGraphic = go.GetComponent<Image>();
        var check = new GameObject("C", typeof(RectTransform), typeof(Image));
        check.transform.SetParent(go.transform, false);
        var crt = check.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(22, 22);
        check.GetComponent<Image>().color = new Color(0.3f, 0.75f, 0.4f);
        tg.graphic = check.GetComponent<Image>();
        MkTmp(row.transform, label, 16, true, 32);
        return tg;
    }

    static TMP_InputField MkInput(Transform parent, string placeholder)
    {
        var go = new GameObject("In", typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().minHeight = 42;
        go.GetComponent<LayoutElement>().preferredHeight = 42;
        go.GetComponent<Image>().color = new Color(0.16f, 0.16f, 0.18f);
        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(8, 4); trt.offsetMax = new Vector2(-8, -4);
        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 18;
        tmp.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
        var phGo = new GameObject("Ph", typeof(RectTransform), typeof(TextMeshProUGUI));
        phGo.transform.SetParent(go.transform, false);
        var prt = phGo.GetComponent<RectTransform>();
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
        prt.offsetMin = new Vector2(8, 4); prt.offsetMax = new Vector2(-8, -4);
        var ph = phGo.GetComponent<TextMeshProUGUI>();
        ph.text = placeholder;
        ph.fontSize = 18;
        ph.color = new Color(1, 1, 1, 0.35f);
        if (TMP_Settings.defaultFontAsset != null) ph.font = TMP_Settings.defaultFontAsset;
        var input = go.GetComponent<TMP_InputField>();
        input.textViewport = go.GetComponent<RectTransform>();
        input.textComponent = tmp;
        input.placeholder = ph;
        return input;
    }
}
