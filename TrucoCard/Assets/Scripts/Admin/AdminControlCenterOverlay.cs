using System;
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
        if (FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        _root = new GameObject("AdminControlCenterModal");
        _root.transform.SetParent(transform, false);
        Stretch(_root.AddComponent<RectTransform>());
        _root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

        var panel = Panel("Panel", _root.transform, new Vector2(980, 1280));
        var header = Row(panel.transform, 48);
        MkBtn(header.transform, TrucoTextosClient.AdminVolver, new Vector2(120, 40), new Color(0.35f, 0.35f, 0.35f), Close);
        MkTmp(header.transform, TrucoTextosClient.AdminControlTitle, 22, true);
        MkBtn(header.transform, TrucoTextosClient.AdminRefreshList, new Vector2(140, 40), new Color(0.2f, 0.5f, 0.7f), () => _ = RefreshAsync());

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

        MkTmp(panel.transform, "Registro de admin", 16, false, 24);
        _audit = MkTmp(panel.transform, "", 15, false, 160);

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
            "   ·   Mantenimiento: " + (TrucoMaintenanceMode.IsActive ? "ACTIVO" : "off") +
            (string.IsNullOrEmpty(onlineRaw) ? "" : "\nOnline API: " + Trunc(onlineRaw, 160));

        _alerts.text =
            "<b>Alertas reconexión / fraude</b>\n" +
            Trunc(reconnect, 220) + "\n" +
            Trunc(fraud, 220);

        var log = TrucoAdminAuditLog.Load();
        var sb = new System.Text.StringBuilder();
        int n = Mathf.Min(12, log.Count);
        for (int i = 0; i < n; i++)
        {
            var e = log[i];
            sb.Append(e.at).Append("  ").Append(e.admin).Append("  ").Append(e.action);
            if (!string.IsNullOrEmpty(e.detail)) sb.Append(" — ").Append(e.detail);
            sb.Append('\n');
        }
        _audit.text = sb.Length == 0 ? "(sin registros aún)" : sb.ToString();
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
        if (pushed) TrucoMaintenanceMode.ApplyLocal(state);
        TrucoAdminAuditLog.Record(on ? "KILL_SWITCH_ON" : "KILL_SWITCH_OFF",
            "game=" + state.blockGame + " tables=" + state.blockTables + " tourneys=" + state.blockTournaments
            + " tx=" + state.blockTransactions + " api=" + pushed);
        AppManager.Instance?.DisplayNotification(!pushed
            ? "El servidor no confirmó el cambio. Mantenimiento sin cambios."
            : on ? "Mantenimiento activado y confirmado por el servidor." : "Mantenimiento desactivado.");
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
        AppManager.Instance?.DisplayNotification(ok ? "Torneo cerrado." : "No se pudo cerrar (ruta de admin vacía o error).");
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
        AppManager.Instance?.DisplayNotification(ok ? "Campeón registrado." : "No se pudo fijar el campeón.");
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
        AppManager.Instance?.DisplayNotification(ok ? "Premio acreditado." : "No se pudo acreditar (ruta /admin/users/{id}/coins).");
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

    static void MkBtn(Transform parent, string label, Vector2 size, Color bg, UnityEngine.Events.UnityAction click)
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
