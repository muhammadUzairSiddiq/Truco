using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Panel: lista de salas GET /matches, crear con player-create, unirse con join + Photon.
/// Resultados de partida 1v1: el backend es la autoridad; el cliente envía/lee salas y códigos según el API.</summary>
public class OneVsOneRoomListController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject _root;
    [SerializeField] private Transform _scrollContent;
    [SerializeField] private OneVsOneRoomRowView _rowPrefab;
    [SerializeField] private Button _buttonRefresh;
    [SerializeField] private Button _buttonCreate;
    [SerializeField] private Button _buttonBack;

    [Header("Crear")]
    [SerializeField] private OneVsOneCreateRoomPanel _createPanel;

    [Header("Contraseña (privada) — mín. 4 caracteres")]
    [SerializeField] private GameObject _passwordOverlay;
    [SerializeField] private TMPro.TMP_InputField _passwordField;
    [SerializeField] private Button _passwordConfirm;
    [SerializeField] private Button _passwordCancel;
    [Header("Flujos")]
    [SerializeField] private OneVsOnePhotonFlow _photonFlow;
    [SerializeField] private GameObject _matchmakingScreen;

    [Tooltip("Cada cuántos segundos vuelve a consultar el backend (0 = desactiva; usar botón Actualizar).")]
    [SerializeField] private float _autoRefreshSeconds = 0f;
    [SerializeField] private float _minSecondsBetweenRefreshes = 2.5f;

    private float _nextAutoRefresh;
    private float _lastRefreshTime = -100f;
    private Player1v1Match _pendingPrivateJoin;
    private Player1v1Match _myHostedRoom;
    private readonly List<OneVsOneRoomRowView> _spawned = new List<OneVsOneRoomRowView>();
    static bool _createRoomInFlight;
    bool _joinInFlight;
    static bool _purgeInFlight;
    static float _lastPurgeUnscaledTime = -100f;
    const float MinSecondsBetweenAutoPurges = 4f;

    void Awake()
    {
        if (_photonFlow == null)
            _photonFlow = FindObjectOfType<OneVsOnePhotonFlow>(true);
        if (_photonFlow == null)
            _photonFlow = OneVsOnePhotonFlow.EnsureInstance();
    }

    /// <summary>Used by <see cref="TrucoOneVsOneMainMenuFactory"/> so the lobby works with zero inspector wiring.</summary>
    public void ApplyRuntimeWiring(
        GameObject root,
        Transform scrollContent,
        OneVsOneRoomRowView rowPrefab,
        OneVsOneCreateRoomPanel createPanel,
        Button buttonRefresh,
        Button buttonCreate,
        Button buttonBack,
        GameObject passwordOverlay,
        TMP_InputField passwordField,
        Button passwordConfirm,
        Button passwordCancel,
        GameObject matchmakingScreen,
        OneVsOnePhotonFlow photonFlow)
    {
        _root = root;
        _scrollContent = scrollContent;
        _rowPrefab = rowPrefab;
        _createPanel = createPanel;
        _buttonRefresh = buttonRefresh;
        _buttonCreate = buttonCreate;
        _buttonBack = buttonBack;
        _passwordOverlay = passwordOverlay;
        _passwordField = passwordField;
        _passwordConfirm = passwordConfirm;
        _passwordCancel = passwordCancel;
        _matchmakingScreen = matchmakingScreen;
        _photonFlow = photonFlow != null ? photonFlow : OneVsOnePhotonFlow.EnsureInstance();
    }

    static bool _skipNextOnEnableRefresh;

    void OnEnable()
    {
        TrucoReturnFromGameplayCleanup.ConsumeIfNeeded();
        OneVsOnePhotonFlow.OnLobbyRoomCountsChanged += OnPhotonLobbyCountsChanged;
        if (_buttonRefresh != null) _buttonRefresh.onClick.AddListener(() => Refresh(showLoading: true, forcePurge: true));
        if (_buttonCreate != null) _buttonCreate.onClick.AddListener(OnClickCreate);
        if (_buttonBack != null)
        {
            _buttonBack.onClick.RemoveAllListeners();
            _buttonBack.onClick.AddListener(GoBackFromRoomList);
        }
        if (_passwordConfirm != null) _passwordConfirm.onClick.AddListener(ConfirmPasswordAndJoin);
        if (_passwordCancel != null) _passwordCancel.onClick.AddListener(() => { ClosePassword(); });
        _nextAutoRefresh = Time.unscaledTime + 1f;
        if (_skipNextOnEnableRefresh)
        {
            _skipNextOnEnableRefresh = false;
            return;
        }
        Refresh(showLoading: true, forcePurge: true);
    }

    void OnDisable()
    {
        OneVsOnePhotonFlow.OnLobbyRoomCountsChanged -= OnPhotonLobbyCountsChanged;
        if (_buttonRefresh != null) _buttonRefresh.onClick.RemoveAllListeners();
        if (_buttonCreate != null) _buttonCreate.onClick.RemoveListener(OnClickCreate);
        if (_buttonBack != null) _buttonBack.onClick.RemoveAllListeners();
        if (_passwordConfirm != null) _passwordConfirm.onClick.RemoveListener(ConfirmPasswordAndJoin);
    }

    void Update()
    {
        if (_autoRefreshSeconds <= 0f) return;
        if (Time.unscaledTime < _nextAutoRefresh) return;
        if (_root != null && !_root.activeInHierarchy) return;
        _nextAutoRefresh = Time.unscaledTime + _autoRefreshSeconds;
        if (Time.unscaledTime - _lastRefreshTime < _minSecondsBetweenRefreshes) return;
        Refresh(showLoading: false);
    }

    public void Open(bool refreshOnOpen = true)
    {
        TrucoReturnFromGameplayCleanup.ConsumeIfNeeded();
        OneVsOneMatchLifecycle.SanitizeSessionForRoomBrowser();
        TrucoLobbyMatchmakingUi.HideWaitingOverlay();
        if (_root != null)
        {
            _root.SetActive(true);
            _root.transform.localScale = Vector3.one;
            var topCanvas = _root.GetComponent<Canvas>();
            if (topCanvas != null)
            {
                topCanvas.overrideSorting = true;
                topCanvas.sortingOrder = 31000;
            }
        }
        MainMenuViewCoordinator.EnsureBottomNavVisible();
        _nextAutoRefresh = Time.unscaledTime + 1f;
        if (refreshOnOpen)
            Refresh(showLoading: true, forcePurge: true);
    }

    /// <summary>When enabled in TrucoClientSettings, POST /leave on every stale lobby row for this user before listing rooms.</summary>
    public static async System.Threading.Tasks.Task<ApiController.LobbyPurgeResult> TryPurgeMyLobbyMatchesAsync(
        bool force = false,
        string keepMatchId = null)
    {
        if (_purgeInFlight) return default;
        if (!force && !TrucoClientSettings.AutoLeaveAllMyLobbyMatchesOnRoomListOpen) return default;
        if (!force && Time.unscaledTime - _lastPurgeUnscaledTime < MinSecondsBetweenAutoPurges) return default;
        _purgeInFlight = true;
        try
        {
            if (!await ApiController.EnsureSessionUserLoadedAsync())
            {
                TrucoDebugLog.Warn(TrucoDebugLog.Category.Lobby, "Purge skipped: no logged-in user yet.");
                return default;
            }

            if (force && await ApiController.CheckUserAdmin())
            {
                AppManager.Instance?.DisplayLoadingUI(TrucoTextosClient.Conectando);
                int adminClosed = await OneVsOneMatchLifecycle.AdminPurgeAllLobbyMatchesAsync(
                    err => TrucoDebugLog.Warn(TrucoDebugLog.Category.Lobby, "Admin force-close: " + err));
                TrucoDebugLog.Log(TrucoDebugLog.Category.Lobby, "Admin force-closed " + adminClosed + " matches.");
            }

            AppManager.Instance?.DisplayLoadingUI(TrucoTextosClient.Conectando);
            string keep = keepMatchId;
            if (string.IsNullOrEmpty(keep) && OneVsOneMatchLifecycle.IsHostWaitingForGuest())
                keep = OneVsOneMatchSession.CurrentMatchId;
            if (string.IsNullOrEmpty(keep) && !OneVsOneMatchSession.GameStarted)
                keep = TrucoActiveHostMatchStore.GetRememberedMatchId();
            if (string.IsNullOrEmpty(keep)) keep = null;
            var result = await OneVsOneMatchLifecycle.PurgeAllMyActiveLobbyMatchesDetailedAsync(keep);
            _lastPurgeUnscaledTime = Time.unscaledTime;
            return result;
        }
        finally
        {
            _purgeInFlight = false;
            AppManager.Instance?.HideLoadingUI();
        }
    }

    /// <summary>Lobby auto-cleanup is silent for players — details go to debug logs only.</summary>
    static void NotifyPurgeResult(ApiController.LobbyPurgeResult result, int joinableFromOthers = -1, bool force = false)
    {
        TrucoDebugLog.Log(TrucoDebugLog.Category.Lobby,
            "PurgeResult silent UI succeeded=" + result.succeeded
            + " attempted=" + result.attempted
            + " failed=" + result.failed
            + " mineStillVisible=" + result.mineStillVisible
            + " joinableOthers=" + joinableFromOthers
            + " force=" + force);
        // Do not DisplayNotification — purge/server counts look like developer errors to players.
    }

    public static void ResetLobbyPurgeDebounce() => _lastPurgeUnscaledTime = -100f;

    public void Close()
    {
        if (_root != null) _root.SetActive(false);
        TrucoLobbyMatchmakingUi.HideWaitingOverlay();
    }

    void GoBackFromRoomList()
    {
        TrucoLobbyLeaveConfirm.RunIfNeeded(() =>
        {
            Close();
            MainMenuViewCoordinator.ReturnToMenuFromTournament();
            MainMenuViewCoordinator.EnsureBottomNavVisible();
        });
    }

    public bool IsLobbyVisible => _root != null && _root.activeInHierarchy;

    float _nextPhotonSoftRefresh;
    const float PhotonSoftRefreshCooldown = 2.5f;

    void OnPhotonLobbyCountsChanged()
    {
        if (_root != null && !_root.activeInHierarchy) return;
        bool anyEmpty = false;
        foreach (var v in _spawned)
        {
            if (v != null) v.RefreshFromLivePhoton();
        }
        // Host left Photon → count 0: soft-refresh API list once (event-driven, not a poll loop).
        for (int i = 0; i < _spawned.Count; i++)
        {
            var row = _spawned[i];
            if (row == null) continue;
            var m = row.BoundMatch;
            if (m == null) continue;
            string photon = m.ResolvePhotonRoomName();
            if (string.IsNullOrEmpty(photon)) continue;
            if (OneVsOnePhotonFlow.TryGetLiveLobbyPlayerCountForMatch(photon, out int c) && c <= 0)
            {
                anyEmpty = true;
                break;
            }
        }
        if (!anyEmpty) return;
        if (Time.unscaledTime < _nextPhotonSoftRefresh) return;
        _nextPhotonSoftRefresh = Time.unscaledTime + PhotonSoftRefreshCooldown;
        TrucoDebugLog.Log(TrucoDebugLog.Category.Lobby,
            "Photon lobby empty room detected → soft Refresh (no force purge)");
        Refresh(showLoading: false, forcePurge: false);
    }

    public async void Refresh(bool showLoading = false, bool forcePurge = false, string keepMatchId = null)
    {
        var purgeResult = await TryPurgeMyLobbyMatchesAsync(force: forcePurge, keepMatchId: keepMatchId);
        _lastRefreshTime = Time.unscaledTime;
        if (_scrollContent == null || _rowPrefab == null) return;
        if (_photonFlow == null) _photonFlow = OneVsOnePhotonFlow.EnsureInstance();
        _photonFlow.EnsureLobbyForRoomList();
        if (showLoading)
            AppManager.Instance.DisplayLoadingUI(TrucoTextosClient.Conectando);
        List<Player1v1Match> list = null;
        try
        {
            list = await ApiController.FetchPlayer1v1MatchList();
        }
        finally
        {
            if (showLoading)
                AppManager.Instance.HideLoadingUI();
        }
        foreach (var v in _spawned)
            if (v != null) Destroy(v.gameObject);
        _spawned.Clear();
        if (list == null) return;
        await CancelAllExtraHostedRoomsAsync(list);
        await ScrubAbandonedHostRoomsAsync(list);
        list = await ApiController.FetchPlayer1v1MatchList();
        if (list == null) return;
        OneVsOneMatchLifecycle.ReconcilePersistedLobbyState(list);
        var seenIds = new HashSet<string>();
        foreach (var m in list.OrderBy(m => m.name ?? string.Empty))
        {
            if (m == null || string.IsNullOrEmpty(m._id) || !seenIds.Add(m._id)) continue;
            if (!m.ShouldShowInLobbyList() && !m.ShouldShowResumeInLobbyList()) continue;
            SpawnRoomRow(m);
        }

        Truco1v1SceneUiWiring.RemoveGlobalPrivateCodeBarIfAny(_scrollContent);
        TryOfferRejoinSavedMatch(list);
        UpdateCreateButtonState(list);
        NotifyPurgeResult(purgeResult, _spawned.Count, forcePurge);
        if (showLoading)
            TrucoNotificationLog.Info(string.Format(TrucoTextosClient.LogSalasActualizadas, _spawned.Count));
    }

    void SpawnRoomRow(Player1v1Match m)
    {
        var row = Instantiate(_rowPrefab, _scrollContent);
        row.gameObject.SetActive(true);
        bool resume = m.ShouldShowResumeInLobbyList();
        bool can = resume || m.CanClickJoinOnRoom();
        string joinTxt = null;
        if (resume)
            joinTxt = TrucoTextosClient.ContinuarPartida;
        else if (!can)
        {
            if (m.IsStaleFullVersusPhoton() || m.IsHostAbandonedVersusPhoton()) joinTxt = null;
            else if (m.IsCurrentUserHostOfRoom()) joinTxt = TrucoTextosClient.TuSalaEsperando;
            else if (m.GetTrucoPlayerCount() >= 2) joinTxt = TrucoTextosClient.SalaLlena;
        }
        string hostCode = null;
        if (m.IsPrivate() && m.IsCurrentUserHostOfRoom())
        {
            if (!string.IsNullOrEmpty(m.joinCode)) hostCode = m.joinCode.Trim();
            if (string.IsNullOrEmpty(hostCode)) hostCode = OneVsOnePrivateRoomCode.TryGetRememberedForMatch(m._id);
        }
        row.Bind(m, OnClickJoin, can, joinTxt, hostCode);
        _spawned.Add(row);
    }

    static string MapJoinApiError(string message) => TrucoUserFacingErrors.ForApiOrPhoton(message);

    void UpdateCreateButtonState(List<Player1v1Match> list)
    {
        _myHostedRoom = FindMyHostedRoom(list);
        if (_buttonCreate == null) return;
        EnsureCreateButtonRaycasts();
        var label = _buttonCreate.GetComponentInChildren<TMP_Text>(true);
        bool hosting = IsCurrentlyHosting();
        // Always keep Delete usable while hosting — never lock behind matchmaking "busy".
        bool busy = !hosting && (_createRoomInFlight || OneVsOnePhotonFlow.IsMatchmakingBusyGlobally);
        _buttonCreate.interactable = !busy;
        if (label != null)
        {
            label.raycastTarget = false;
            label.text = hosting ? TrucoTextosClient.EliminarSala : TrucoTextosClient.CrearSala;
        }
        TrucoDebugLog.Log(TrucoDebugLog.Category.Lobby,
            "CreateBtn hosting=" + hosting + " busy=" + busy
            + " myHosted=" + (_myHostedRoom != null ? _myHostedRoom._id : "null")
            + " session=" + (OneVsOneMatchSession.CurrentMatchId ?? "null")
            + " remembered=" + (TrucoActiveHostMatchStore.GetRememberedMatchId() ?? "null"));
    }

    void EnsureCreateButtonRaycasts()
    {
        if (_buttonCreate == null) return;
        var tmp = _buttonCreate.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < tmp.Length; i++)
            if (tmp[i] != null) tmp[i].raycastTarget = false;
    }

    bool IsCurrentlyHosting()
    {
        if (_myHostedRoom != null) return true;
        if (OneVsOneMatchLifecycle.IsHostWaitingForGuest()) return true;
        if (!string.IsNullOrEmpty(TrucoActiveHostMatchStore.GetRememberedMatchId())
            && !OneVsOneMatchSession.GameStarted)
            return true;
        return false;
    }

    static Player1v1Match FindMyHostedRoom(List<Player1v1Match> list)
    {
        if (list != null)
        {
            foreach (var m in list)
                if (m != null && m.IsCurrentUserHostOfRoom() && m.IsLobbyLikeStatus())
                    return m;
            string sessionId = OneVsOneMatchSession.CurrentMatchId;
            if (string.IsNullOrEmpty(sessionId))
                sessionId = TrucoActiveHostMatchStore.GetRememberedMatchId();
            if (!string.IsNullOrEmpty(sessionId))
            {
                foreach (var m in list)
                    if (m != null && m._id == sessionId && m.IsLobbyLikeStatus())
                        return m;
            }
        }
        return null;
    }

    async void TryOfferRejoinSavedMatch(List<Player1v1Match> list)
    {
        if (PhotonNetwork.InRoom) return;
        string room = TrucoRoomPersistence.LastRoomName();
        string matchId = TrucoRoomPersistence.LastMatchId();
        if (string.IsNullOrEmpty(room) || string.IsNullOrEmpty(matchId)) return;
        if (!string.IsNullOrEmpty(OneVsOneMatchSession.CurrentMatchId)) return;
        Player1v1Match match = null;
        if (list != null)
            foreach (var m in list)
                if (m != null && m._id == matchId) { match = m; break; }
        if (match == null || !match.IsLobbyLikeStatus() || !match.IsCurrentUserParticipant())
        {
            TrucoRoomPersistence.Clear();
            return;
        }
        // Never auto-resume gameplay from persistence — lobby rejoin only, and only before cards.
        if (OneVsOneMatchSession.GameStarted)
        {
            TrucoRoomPersistence.Clear();
            return;
        }
        TrucoRoomPersistence.RestoreSessionFromSaved(match);
        if (_photonFlow == null) _photonFlow = OneVsOnePhotonFlow.EnsureInstance();
        AppManager.Instance?.DisplayNotification(TrucoLocalization.T(TrucoLocalization.Key.RejoinPartida));
        PrepareMatchmakingUi();
        if (match.IsCurrentUserHostOfRoom())
            _photonFlow.StartHostPhoton(OneVsOneMatchSession.MaxPlayersPhoton);
        else
            _photonFlow.StartJoinPhoton();
    }

    void OnClickCreate()
    {
        EnsureCreateButtonRaycasts();
        // Hosting check FIRST so Delete always works even if create-in-flight flag stuck.
        if (IsCurrentlyHosting())
        {
            TrucoDebugLog.Log(TrucoDebugLog.Category.Lobby, "OnClickCreate → Delete hosted room");
            _ = DeleteCurrentHostedRoomAsync();
            return;
        }
        if (_createRoomInFlight)
            return;
        if (_createPanel == null) { AppManager.Instance.DisplayNotification(TrucoTextosClient.FaltaPanelCrear); return; }
        MainMenuViewCoordinator.EnsureBottomNavVisible();
        if (_root != null) _root.SetActive(false);
        _createPanel.Open(OnCreateRoomConfirmed, OnCreateRoomCancelled);
    }

    async System.Threading.Tasks.Task DeleteCurrentHostedRoomAsync()
    {
        string matchId = null;
        int expectedRefund = 0;
        try
        {
            if (_myHostedRoom != null)
            {
                await DeleteHostedRoomAsync(_myHostedRoom);
                return;
            }
            matchId = OneVsOneMatchSession.CurrentMatchId;
            if (string.IsNullOrEmpty(matchId))
                matchId = TrucoActiveHostMatchStore.GetRememberedMatchId();
            if (string.IsNullOrEmpty(matchId))
            {
                ClearLocalHostingState();
                UpdateCreateButtonState(null);
                return;
            }
            expectedRefund = OneVsOneMatchSession.EntryFee;
            if (expectedRefund <= 0 && TrucoRoomPersistence.LastMatchId() == matchId)
                expectedRefund = TrucoRoomPersistence.LastEntryFee();

            AppManager.Instance.DisplayLoadingUI(TrucoTextosClient.Conectando);
            // Refund BEFORE Photon leave — leaving first can webhook-close without refund.
            bool refunded = await OneVsOneMatchLifecycle.CancelLobbyMatchAsync(matchId, expectedRefund);
            if (refunded)
            {
                if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
                ClearLocalHostingState();
                if (_photonFlow != null) _photonFlow.ResetPurpose();
                AppManager.Instance.HideLoadingUI();
                MainMenuViewCoordinator.EnsureBottomNavVisible();
                AppManager.Instance.DisplayNotification(TrucoTextosClient.SalaEliminada);
                _skipNextOnEnableRefresh = true;
                Refresh(showLoading: false, forcePurge: false, keepMatchId: null);
            }
            else
            {
                if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
                AppManager.Instance.HideLoadingUI();
                MainMenuViewCoordinator.EnsureBottomNavVisible();
                AppManager.Instance.DisplayNotification(TrucoTextosClient.ErrorEliminarSala);
                TrucoDebugLog.Warn(TrucoDebugLog.Category.Lobby,
                    "DeleteCurrentHostedRoom refund not confirmed match=" + matchId
                    + " stake=" + expectedRefund + " — keeping host session for retry");
                // Keep match id in session/store so retry + purge won't /end without refund.
                TrucoActiveHostMatchStore.Remember(matchId);
                _skipNextOnEnableRefresh = true;
                Refresh(showLoading: false, forcePurge: false, keepMatchId: matchId);
            }
        }
        catch (System.Exception ex)
        {
            AppManager.Instance?.HideLoadingUI();
            TrucoDebugLog.Warn(TrucoDebugLog.Category.Lobby, "DeleteCurrentHostedRoom failed: " + ex.Message);
            AppManager.Instance?.DisplayNotification(TrucoTextosClient.ErrorEliminarSala);
            if (!string.IsNullOrEmpty(matchId))
            {
                TrucoActiveHostMatchStore.Remember(matchId);
                _skipNextOnEnableRefresh = true;
                Refresh(showLoading: false, forcePurge: false, keepMatchId: matchId);
            }
        }
    }

    void ClearLocalHostingState()
    {
        _myHostedRoom = null;
        OneVsOneMatchSession.Clear();
        TrucoMatchProgress.ClearAllMatchMemory();
        TrucoActiveHostMatchStore.Clear();
        TrucoRoomPersistence.Clear();
    }

    async System.Threading.Tasks.Task DeleteHostedRoomAsync(Player1v1Match room)
    {
        if (room == null) return;
        string matchId = room._id;
        int expectedRefund = room.GetEntryStake();
        try
        {
            AppManager.Instance.DisplayLoadingUI(TrucoTextosClient.Conectando);
            bool refunded = await OneVsOneMatchLifecycle.CancelLobbyMatchAsync(matchId, expectedRefund);
            if (refunded)
            {
                if (PhotonNetwork.InRoom &&
                    (OneVsOneMatchSession.CurrentMatchId == matchId ||
                     PhotonNetwork.CurrentRoom?.Name == room.ResolvePhotonRoomName()))
                    PhotonNetwork.LeaveRoom(false);
                ClearLocalHostingState();
                if (_photonFlow != null) _photonFlow.ResetPurpose();
                AppManager.Instance.HideLoadingUI();
                MainMenuViewCoordinator.EnsureBottomNavVisible();
                AppManager.Instance.DisplayNotification(TrucoTextosClient.SalaEliminada);
                _skipNextOnEnableRefresh = true;
                Refresh(showLoading: false, forcePurge: false, keepMatchId: null);
            }
            else
            {
                if (PhotonNetwork.InRoom &&
                    (OneVsOneMatchSession.CurrentMatchId == matchId ||
                     PhotonNetwork.CurrentRoom?.Name == room.ResolvePhotonRoomName()))
                    PhotonNetwork.LeaveRoom(false);
                AppManager.Instance.HideLoadingUI();
                MainMenuViewCoordinator.EnsureBottomNavVisible();
                AppManager.Instance.DisplayNotification(TrucoTextosClient.ErrorEliminarSala);
                TrucoDebugLog.Warn(TrucoDebugLog.Category.Lobby,
                    "DeleteHostedRoom refund not confirmed match=" + matchId + " stake=" + expectedRefund);
                _myHostedRoom = room;
                TrucoActiveHostMatchStore.Remember(matchId);
                _skipNextOnEnableRefresh = true;
                Refresh(showLoading: false, forcePurge: false, keepMatchId: matchId);
            }
        }
        catch (System.Exception ex)
        {
            AppManager.Instance?.HideLoadingUI();
            TrucoDebugLog.Warn(TrucoDebugLog.Category.Lobby, "DeleteHostedRoom failed: " + ex.Message);
            AppManager.Instance?.DisplayNotification(TrucoTextosClient.ErrorEliminarSala);
            if (!string.IsNullOrEmpty(matchId))
            {
                _myHostedRoom = room;
                TrucoActiveHostMatchStore.Remember(matchId);
                _skipNextOnEnableRefresh = true;
                Refresh(showLoading: false, forcePurge: false, keepMatchId: matchId);
            }
        }
    }

    void OnCreateRoomCancelled()
    {
        _skipNextOnEnableRefresh = true;
        Open();
    }

    async void OnCreateRoomConfirmed(OneVsOneCreateRoomPanel panel)
    {
        if (_createRoomInFlight || IsCurrentlyHosting())
        {
            AppManager.Instance?.DisplayNotification(TrucoTextosClient.YaTienesSala);
            panel?.SetConfirmInteractable(true);
            return;
        }
        _createRoomInFlight = true;
        panel?.SetConfirmInteractable(false);
        if (_buttonCreate != null) _buttonCreate.interactable = false;
        try
        {
            await OneVsOneMatchLifecycle.PurgeAllMyActiveLobbyMatchesAsync();
            var list = await ApiController.FetchPlayer1v1MatchList();
            if (FindMyHostedRoom(list) != null)
            {
                AppManager.Instance.DisplayNotification(TrucoTextosClient.YaTienesSala);
                panel?.Close();
                _skipNextOnEnableRefresh = true;
                Open();
                return;
            }
        int fee = panel.GetEntryFee();
        if (!ClientBalanceOk(fee, out _)) { AppManager.Instance.DisplayNotification(TrucoTextosClient.SaldoInsuficiente); return; }
        int stake = Mathf.Max(1, fee);
        string privatePassword = null;
        if (!panel.IsPublic())
        {
            privatePassword = panel.GetPassword().Trim();
            if (!OneVsOnePrivateRoomCode.IsValidFormat(privatePassword))
            {
                AppManager.Instance.DisplayNotification(TrucoTextosClient.CodigoInvalido4);
                return;
            }
        }
        bool withFlor = panel.WithFlor();
        var body = new PlayerCreateMatchRequest
        {
            name = panel.GetRoomName(),
            type = panel.IsPublic() ? "public" : "private",
            cost = stake,
            prize = Player1v1MatchExtensions.ComputeOneVsOnePrize(stake),
            maxPlayers = OneVsOneMatchSession.MaxPlayersPhoton,
            password = panel.IsPublic() ? string.Empty : privatePassword,
            withFlor = withFlor
        };
        AppManager.Instance.DisplayLoadingUI(TrucoTextosClient.Conectando);
        string createErr = null;
        var created = await ApiController.PlayerCreate1v1Match(body, err => createErr = err);
        AppManager.Instance.HideLoadingUI();
        if (created == null || string.IsNullOrEmpty(created._id))
        {
            AppManager.Instance.DisplayNotification(
                !string.IsNullOrEmpty(createErr)
                    ? TrucoUserFacingErrors.ForApiOrPhoton(createErr)
                    : TrucoTextosClient.ErrorCrearSala);
            return;
        }
        int storedStake = created.GetEntryStake();
        if (storedStake != stake)
        {
            TrucoDebugLog.Warn(TrucoDebugLog.Category.Api,
                "player-create stake mismatch sent=" + stake + " stored=" + storedStake
                + " cost=" + created.cost + " entryFee=" + created.entryFee + " prize=" + created.prize);
        }
        TrucoRulesScenarioLog.Backend("player-create OK",
            "match=" + created._id + " sentStake=" + stake + " storedStake=" + storedStake
            + " cost=" + created.cost + " prize=" + created.prize);
        TrucoMatchProgress.ResetForNewMatch();
        TrucoRoomPersistence.Clear();
        if (!string.IsNullOrEmpty(privatePassword)) OneVsOnePrivateRoomCode.RememberForMatch(created._id, privatePassword);
        string photon = created.ResolvePhotonRoomNameOrDefault(created._id);
        bool reg = await ApiController.RegisterPhotonRoomName(created._id, photon,
            err => TrucoDebugLog.Warn(TrucoDebugLog.Category.Api, "RegisterPhotonRoom: " + err));
        if (!reg) AppManager.Instance.DisplayNotification(TrucoTextosClient.PhotonSyncWarning);
        OneVsOneMatchSession.SetHostContext(created._id, photon, storedStake > 0 ? storedStake : fee, withFlor);
        TrucoActiveHostMatchStore.Remember(created._id);
        TrucoRoomPersistence.SavePendingLobby(photon, created._id, fee, withFlor, isHost: true);
        panel.Close();
        PrepareMatchmakingUi();
        _skipNextOnEnableRefresh = true;
        _photonFlow = OneVsOnePhotonFlow.EnsureInstance();
        _photonFlow.StartHostPhoton(OneVsOneMatchSession.MaxPlayersPhoton,
            err => AppManager.Instance?.DisplayNotification(TrucoUserFacingErrors.ForApiOrPhoton(err)));
        Open(refreshOnOpen: false);
        // Soft refresh (no force purge) so the new room appears in the list.
        Refresh(showLoading: false, forcePurge: false);
        UpdateCreateButtonState(null);
        TrucoLobbyMatchmakingUi.HideWaitingOverlay();
        TrucoNotificationLog.Success(TrucoTextosClient.LogSalaCreada);
        }
        finally
        {
            _createRoomInFlight = false;
            panel?.SetConfirmInteractable(true);
            UpdateCreateButtonState(null);
        }
    }

    void OnClickJoin(Player1v1Match m, OneVsOneRoomRowView row)
    {
        if (m == null) return;
        if (m.IsPrivate())
        {
            string c = row != null ? row.GetJoinerCodeText() : string.Empty;
            if (!OneVsOnePrivateRoomCode.IsValidFormat(c))
            {
                AppManager.Instance.DisplayNotification(TrucoTextosClient.CodigoInvalido4);
                return;
            }
            _ = TryJoin(m, c.Trim(), row);
            return;
        }
        _ = TryJoin(m, string.Empty, row);
    }

    async void ConfirmPasswordAndJoin()
    {
        if (_pendingPrivateJoin == null) { ClosePassword(); return; }
        string p = _passwordField != null ? _passwordField.text : string.Empty;
        if (!OneVsOnePrivateRoomCode.IsValidFormat(p))
        {
            AppManager.Instance.DisplayNotification(TrucoTextosClient.CodigoInvalido4);
            return;
        }
        var target = _pendingPrivateJoin;
        _pendingPrivateJoin = null;
        ClosePassword();
        await TryJoin(target, p.Trim(), null);
    }

    void ClosePassword() { if (_passwordOverlay != null) _passwordOverlay.SetActive(false); }

    async System.Threading.Tasks.Task TryJoin(Player1v1Match m, string password, OneVsOneRoomRowView row)
    {
        if (m == null || string.IsNullOrEmpty(m._id)) return;
        if (OneVsOneMatchLifecycle.IsHostWaitingForGuest())
        {
            TrucoDebugLog.Warn(TrucoDebugLog.Category.Lobby,
                "TryJoin blocked: already hosting match=" + OneVsOneMatchSession.CurrentMatchId);
            AppManager.Instance?.DisplayNotification(TrucoTextosClient.YaTienesSala);
            return;
        }
        if (_joinInFlight || OneVsOnePhotonFlow.IsMatchmakingBusyGlobally)
        {
            TrucoDebugLog.Warn(TrucoDebugLog.Category.Lobby,
                "TryJoin blocked: inFlight=" + _joinInFlight + " photonBusy=" +
                OneVsOnePhotonFlow.IsMatchmakingBusyGlobally
                + " purpose=" + (OneVsOnePhotonFlow.Instance != null
                    ? OneVsOnePhotonFlow.Instance.CurrentPurpose.ToString()
                    : "?")
                + " inRoom=" + Photon.Pun.PhotonNetwork.InRoom
                + " connecting=" + (OneVsOnePhotonFlow.Instance != null && OneVsOnePhotonFlow.Instance.IsConnecting));
            AppManager.Instance?.DisplayNotification(TrucoTextosClient.Conectando);
            return;
        }
        _joinInFlight = true;
        row?.SetJoinInteractable(false);
        string listPhoton = m.ResolvePhotonRoomNameOrDefault(m._id);
        TrucoDebugLog.Always(TrucoDebugLog.Category.Lobby,
            "TryJoin start match=" + m._id
            + " name=" + (m.name ?? "?")
            + " private=" + m.IsPrivate()
            + " apiPlayers=" + m.GetApiReportedPlayerCount()
            + " uiPlayers=" + m.GetTrucoPlayerCount()
            + " photon=" + listPhoton
            + " region=" + (Photon.Pun.PhotonNetwork.CloudRegion ?? "?")
            + " inLobby=" + Photon.Pun.PhotonNetwork.InLobby
            + " inRoom=" + Photon.Pun.PhotonNetwork.InRoom);
        try
        {
        if (m != null && m.IsPrivate() && !OneVsOnePrivateRoomCode.IsValidFormat(password))
        {
            TrucoDebugLog.Warn(TrucoDebugLog.Category.Lobby, "TryJoin abort: invalid private code format");
            AppManager.Instance.DisplayNotification(TrucoTextosClient.CodigoInvalido4);
            return;
        }
        if (m != null && m.IsStaleFullVersusPhoton())
        {
            TrucoDebugLog.Warn(TrucoDebugLog.Category.Lobby, "TryJoin abort: stale full vs Photon");
            AppManager.Instance.DisplayNotification(TrucoTextosClient.SalaExpiradaAviso);
            await RemoveAbandonedLobbyRowAsync(m._id, TrucoTextosClient.SalaExpiradaAviso);
            return;
        }
        if (m != null && m.IsHostAbandonedVersusPhoton())
        {
            TrucoDebugLog.Warn(TrucoDebugLog.Category.Lobby,
                "TryJoin abort: host abandoned Photon room gone match=" + m._id);
            await RemoveAbandonedLobbyRowAsync(m._id, TrucoTextosClient.AnfitrionSalioSala);
            return;
        }
        var fresh = await ApiController.GetMatch1v1(m._id);
        if (fresh != null)
        {
            TrucoDebugLog.Always(TrucoDebugLog.Category.Api,
                "TryJoin GET match ok id=" + fresh._id
                + " status=" + (fresh.status ?? "?")
                + " players=" + fresh.GetApiReportedPlayerCount()
                + " photon=" + fresh.ResolvePhotonRoomNameOrDefault(fresh._id));
            m = fresh;
        }
        else
            TrucoDebugLog.Warn(TrucoDebugLog.Category.Api, "TryJoin GET match returned null — using list row");

        // Already registered on backend — skip POST /join (prevents "Match is full" on double tap).
        if (m.IsAlreadyRegisteredGuest())
        {
            TrucoDebugLog.Always(TrucoDebugLog.Category.Lobby, "TryJoin resume guest (skip POST /join) match=" + m._id);
            await LaunchGuestPhotonAsync(m);
            return;
        }

        // Do NOT use ShouldShowInLobbyList here — Photon lobby counts can make a joinable
        // 1/2 room look "hidden" right before POST /join and falsely abort with a vague error.
        if (m == null || !m.IsLobbyLikeStatus())
        {
            TrucoDebugLog.Warn(TrucoDebugLog.Category.Lobby,
                "TryJoin abort: not lobby-like status=" + (m?.status ?? "null"));
            AppManager.Instance.DisplayNotification(TrucoTextosClient.SalaExpiradaAviso);
            Refresh(showLoading: false);
            return;
        }
        if (m.GetApiReportedPlayerCount() >= 2)
        {
            TrucoDebugLog.Warn(TrucoDebugLog.Category.Lobby, "TryJoin abort: API already full");
            AppManager.Instance.DisplayNotification(TrucoTextosClient.SalaLlena);
            Refresh(showLoading: false);
            return;
        }
        int stake = m.GetEntryStake();
        if (!ClientBalanceOk(stake, out int bal))
        {
            TrucoDebugLog.Warn(TrucoDebugLog.Category.Lobby,
                "TryJoin abort: insufficient balance stake=" + stake + " bal=" + bal);
            AppManager.Instance.DisplayNotification(TrucoTextosClient.SaldoInsuficiente);
            return;
        }
        AppManager.Instance.DisplayLoadingUI(TrucoTextosClient.Conectando);
        Player1v1Match result = null;
        string joinApiMessage = null;
        try
        {
            TrucoDebugLog.Always(TrucoDebugLog.Category.Api, "POST /join match=" + m._id);
            result = await ApiController.PlayerJoin1v1Match(m._id, password, m, err => joinApiMessage = err);
        }
        finally
        {
            AppManager.Instance.HideLoadingUI();
        }
        if (result == null)
        {
            TrucoDebugLog.Error(TrucoDebugLog.Category.Api,
                "POST /join failed match=" + m._id + " err=" + (joinApiMessage ?? "(none)"));
            var recovery = await ApiController.GetMatch1v1(m._id);
            if (recovery != null && recovery.IsAlreadyRegisteredGuest())
            {
                TrucoDebugLog.Always(TrucoDebugLog.Category.Lobby, "TryJoin recovery: already registered guest");
                await LaunchGuestPhotonAsync(recovery);
                return;
            }
            string shown = MapJoinApiError(joinApiMessage);
            TrucoNotificationLog.Warning("JOIN FAIL: " + shown);
            AppManager.Instance.DisplayNotification(shown);
            _ = ApiController.GetCurrentUserProfile();
            Refresh(showLoading: false);
            return;
        }
        TrucoDebugLog.Always(TrucoDebugLog.Category.Api,
            "POST /join OK match=" + result._id + " photon=" + result.ResolvePhotonRoomNameOrDefault(result._id));
        string id = !string.IsNullOrEmpty(result._id) ? result._id : m._id;
        var verified = await ApiController.GetMatch1v1(id);
        if (verified != null) result = verified;
        await LaunchGuestPhotonAsync(result, id, m);
        }
        finally
        {
            _joinInFlight = false;
            if (row != null && m != null)
                row.SetJoinInteractable(m.CanClickJoinOnRoom());
        }
    }

    async System.Threading.Tasks.Task LaunchGuestPhotonAsync(Player1v1Match result, string matchIdFallback = null, Player1v1Match feeHint = null)
    {
        if (result == null) return;
        string id = !string.IsNullOrEmpty(result._id) ? result._id : matchIdFallback;
        string photon = result.ResolvePhotonRoomNameOrDefault(id);
        if (string.IsNullOrEmpty(photon))
        {
            TrucoDebugLog.Error(TrucoDebugLog.Category.OneVsOne, "LaunchGuestPhoton: empty photon name match=" + id);
            AppManager.Instance.DisplayNotification(TrucoTextosClient.ErrorUnirse);
            return;
        }
        TrucoDebugLog.Always(TrucoDebugLog.Category.OneVsOne,
            "LaunchGuestPhoton room=" + photon + " match=" + id
            + " connected=" + Photon.Pun.PhotonNetwork.IsConnected
            + " ready=" + Photon.Pun.PhotonNetwork.IsConnectedAndReady
            + " server=" + Photon.Pun.PhotonNetwork.Server
            + " region=" + (Photon.Pun.PhotonNetwork.CloudRegion ?? "?")
            + " appVersion=" + (Photon.Pun.PhotonNetwork.PhotonServerSettings?.AppSettings?.AppVersion ?? "?"));
        var hint = feeHint ?? result;
        int fee = result.GetEntryStake() > 0 ? result.GetEntryStake() : hint.GetEntryStake();
        bool withFlor = result.withFlor;
        // Open() sanitizes stale sessions — do that BEFORE SetGuestContext.
        // Previously Open ran after SetGuestContext and wiped PhotonRoomName
        // (guest not InRoom yet → IsGuestWaitingInLobby false → Clear), so
        // StartJoinPhoton failed immediately with "Could not join the room."
        PrepareMatchmakingUi();
        _skipNextOnEnableRefresh = true;
        Open(refreshOnOpen: false);
        OneVsOneMatchSession.SetGuestContext(id, photon, fee, withFlor);
        TrucoRoomPersistence.SavePendingLobby(photon, id, fee, withFlor, isHost: false);
        if (_photonFlow == null) _photonFlow = OneVsOnePhotonFlow.EnsureInstance();
        if (_photonFlow != null)
        {
            _photonFlow.StartJoinPhoton(err =>
            {
                TrucoDebugLog.Error(TrucoDebugLog.Category.Photon, "StartJoinPhoton onError: " + err);
                TrucoNotificationLog.Warning("PHOTON: " + err);
                AppManager.Instance?.DisplayNotification(TrucoUserFacingErrors.ForApiOrPhoton(err));
            });
        }
        else
        {
            TrucoDebugLog.Error(TrucoDebugLog.Category.Photon, "LaunchGuestPhoton: no PhotonFlow instance");
            AppManager.Instance.DisplayNotification(TrucoTextosClient.ErrorUnirse);
            return;
        }
        TrucoNotificationLog.Success(TrucoTextosClient.LogUnidoSala);
        await System.Threading.Tasks.Task.CompletedTask;
    }

    /// <summary>
    /// Guest path: host left Photon but API row still shows 1/2 — cancel row and tell player.
    /// Does not replace host Delete / pause-cancel; only cleans zombies others still see.
    /// </summary>
    async System.Threading.Tasks.Task RemoveAbandonedLobbyRowAsync(string matchId, string userMessage)
    {
        if (!string.IsNullOrEmpty(userMessage))
            AppManager.Instance?.DisplayNotification(userMessage);
        if (!string.IsNullOrEmpty(matchId))
        {
            try
            {
                TrucoDebugLog.Log(TrucoDebugLog.Category.Lobby,
                    "RemoveAbandonedLobbyRow match=" + matchId);
                await OneVsOneMatchLifecycle.CancelLobbyMatchAsync(matchId);
            }
            catch (System.Exception ex)
            {
                TrucoDebugLog.Warn(TrucoDebugLog.Category.Lobby,
                    "RemoveAbandonedLobbyRow cancel failed: " + ex.Message);
            }
        }
        _skipNextOnEnableRefresh = true;
        Refresh(showLoading: false, forcePurge: false);
    }

    /// <summary>While listing rooms, drop API zombies where Photon already shows host gone (0 players).</summary>
    async System.Threading.Tasks.Task ScrubAbandonedHostRoomsAsync(List<Player1v1Match> list)
    {
        if (list == null) return;
        if (_photonFlow == null) _photonFlow = OneVsOnePhotonFlow.EnsureInstance();
        _photonFlow?.EnsureLobbyForRoomList();
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || string.IsNullOrEmpty(m._id)) continue;
            if (!m.IsHostAbandonedVersusPhoton() && !m.IsStaleFullVersusPhoton()) continue;
            // Never auto-cancel our own live host wait from this scrub (Delete / pause own that).
            if (m.IsCurrentUserHostOfRoom() && PhotonNetwork.InRoom) continue;
            TrucoDebugLog.Log(TrucoDebugLog.Category.Lobby,
                "ScrubAbandonedHostRooms cancel match=" + m._id
                + " abandoned=" + m.IsHostAbandonedVersusPhoton()
                + " staleFull=" + m.IsStaleFullVersusPhoton());
            try { await OneVsOneMatchLifecycle.CancelLobbyMatchAsync(m._id); }
            catch (System.Exception ex)
            {
                TrucoDebugLog.Warn(TrucoDebugLog.Category.Lobby, "Scrub cancel failed: " + ex.Message);
            }
        }
    }

    static async System.Threading.Tasks.Task CancelAllExtraHostedRoomsAsync(List<Player1v1Match> list)
    {
        if (list == null || PhotonNetwork.InRoom) return;
        if (OneVsOnePhotonFlow.IsMatchmakingBusyGlobally) return;
        if (!await ApiController.EnsureSessionUserLoadedAsync()) return;
        string userId = ApiController.GetSessionUser?.Data?._id;
        if (string.IsNullOrEmpty(userId)) return;

        string keep = OneVsOneMatchLifecycle.IsHostWaitingForGuest()
            ? OneVsOneMatchSession.CurrentMatchId
            : null;
        var hosted = OneVsOneLobbyFlowRules.GetAllHostedMatchIds(list, userId);
        for (int i = 0; i < hosted.Count; i++)
        {
            if (!string.IsNullOrEmpty(keep) && hosted[i] == keep) continue;
            TrucoDebugLog.Log(TrucoDebugLog.Category.Lobby, "Cancel extra hosted room=" + hosted[i]);
            await OneVsOneMatchLifecycle.CancelLobbyMatchAsync(hosted[i]);
        }
    }

    void PrepareMatchmakingUi()
    {
        // Room list is the waiting UI — hide legacy "Buscando partida" / Volver overlays.
        TrucoLobbyMatchmakingUi.HideWaitingOverlay();
    }

    static bool ClientBalanceOk(int required, out int balance)
    {
        balance = 0;
        var u = ApiController.GetSessionUser?.Data;
        if (u?.wallet == null) return required <= 0;
        balance = u.wallet.balance;
        return balance >= required;
    }
}
