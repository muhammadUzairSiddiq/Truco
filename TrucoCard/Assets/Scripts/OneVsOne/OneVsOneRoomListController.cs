using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly List<OneVsOneRoomRowView> _spawned = new List<OneVsOneRoomRowView>();

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

    void OnEnable()
    {
        TrucoReturnFromGameplayCleanup.ConsumeIfNeeded();
        OneVsOnePhotonFlow.OnLobbyRoomCountsChanged += OnPhotonLobbyCountsChanged;
        if (_buttonRefresh != null) _buttonRefresh.onClick.AddListener(() => Refresh(showLoading: true));
        if (_buttonCreate != null) _buttonCreate.onClick.AddListener(OnClickCreate);
        if (_passwordConfirm != null) _passwordConfirm.onClick.AddListener(ConfirmPasswordAndJoin);
        if (_passwordCancel != null) _passwordCancel.onClick.AddListener(() => { ClosePassword(); });
        _nextAutoRefresh = Time.unscaledTime + 1f;
        Refresh(showLoading: true);
    }

    void OnDisable()
    {
        OneVsOnePhotonFlow.OnLobbyRoomCountsChanged -= OnPhotonLobbyCountsChanged;
        if (_buttonRefresh != null) _buttonRefresh.onClick.RemoveAllListeners();
        if (_buttonCreate != null) _buttonCreate.onClick.RemoveListener(OnClickCreate);
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

    public void Open()
    {
        TrucoReturnFromGameplayCleanup.ConsumeIfNeeded();
        TrucoLobbyMatchmakingUi.HideWaitingOverlay();
        if (_root != null)
        {
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            _root.transform.localScale = Vector3.one;
            var topCanvas = _root.GetComponent<Canvas>();
            if (topCanvas != null)
            {
                topCanvas.overrideSorting = true;
                topCanvas.sortingOrder = 32000;
            }
        }
        _nextAutoRefresh = Time.unscaledTime + 1f;
        Refresh(showLoading: true);
    }

    public void Close()
    {
        if (_root != null) _root.SetActive(false);
        TrucoLobbyMatchmakingUi.HideWaitingOverlay();
    }

    public bool IsLobbyVisible => _root != null && _root.activeInHierarchy;

    void OnPhotonLobbyCountsChanged()
    {
        if (_root != null && !_root.activeInHierarchy) return;
        foreach (var v in _spawned)
        {
            if (v != null) v.RefreshFromLivePhoton();
        }
    }

    public async void Refresh(bool showLoading = false)
    {
        _lastRefreshTime = Time.unscaledTime;
        if (_scrollContent == null || _rowPrefab == null) return;
        if (_photonFlow == null) _photonFlow = OneVsOnePhotonFlow.EnsureInstance();
        _photonFlow.EnsureLobbyForRoomList();
        if (showLoading)
            AppManager.Instance.DisplayLoadingUI(TrucoTextosClient.Conectando);
        var list = await ApiController.FetchPlayer1v1MatchList();
        if (showLoading)
            AppManager.Instance.HideLoadingUI();
        foreach (var v in _spawned)
            if (v != null) Destroy(v.gameObject);
        _spawned.Clear();
        if (list == null) return;
        OneVsOneMatchLifecycle.ClearStaleHostMemory(list);
        foreach (var m in list.OrderBy(m => m.name ?? string.Empty))
        {
            if (!m.ShouldShowInLobbyList()) continue;
            var row = Instantiate(_rowPrefab, _scrollContent);
            row.gameObject.SetActive(true);
            bool can = m.CanClickJoinOnRoom();
            string joinTxt = null;
            if (!can)
            {
                if (m.IsStaleFullVersusPhoton()) joinTxt = null;
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

        Truco1v1SceneUiWiring.RemoveGlobalPrivateCodeBarIfAny(_scrollContent);
        if (showLoading)
            TrucoNotificationLog.Info(string.Format(TrucoTextosClient.LogSalasActualizadas, _spawned.Count));
    }

    void OnClickCreate()
    {
        if (_createPanel == null) { AppManager.Instance.DisplayNotification(TrucoTextosClient.FaltaPanelCrear); return; }
        if (_root != null) _root.SetActive(false);
        _createPanel.Open(OnCreateRoomConfirmed, OnCreateRoomCancelled);
    }

    void OnCreateRoomCancelled()
    {
        if (_root != null) _root.SetActive(true);
    }

    async void OnCreateRoomConfirmed(OneVsOneCreateRoomPanel panel)
    {
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
        var created = await ApiController.PlayerCreate1v1Match(body, err => AppManager.Instance.DisplayNotification(err));
        AppManager.Instance.HideLoadingUI();
        if (created == null || string.IsNullOrEmpty(created._id)) { AppManager.Instance.DisplayNotification(TrucoTextosClient.ErrorCrearSala); return; }
        if (!string.IsNullOrEmpty(privatePassword)) OneVsOnePrivateRoomCode.RememberForMatch(created._id, privatePassword);
        string photon = created.ResolvePhotonRoomName();
        if (string.IsNullOrEmpty(photon)) photon = OneVsOneMatchSession.BuildDefaultPhotonRoomName(created._id);
        bool reg = await ApiController.RegisterPhotonRoomName(created._id, photon, err => Debug.LogWarning(err));
        if (!reg) AppManager.Instance.DisplayNotification(TrucoTextosClient.PhotonSyncWarning);
        OneVsOneMatchSession.SetHostContext(created._id, photon, fee, withFlor);
        TrucoActiveHostMatchStore.Remember(created._id);
        panel.Close();
        PrepareMatchmakingUi();
        if (_root != null) _root.SetActive(true);
        Refresh(showLoading: false);
        if (_photonFlow == null) _photonFlow = OneVsOnePhotonFlow.Instance;
        if (_photonFlow != null) _photonFlow.StartHostPhoton(OneVsOneMatchSession.MaxPlayersPhoton);
        TrucoNotificationLog.Success(TrucoTextosClient.LogSalaCreada);
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
            _ = TryJoin(m, c.Trim());
            return;
        }
        _ = TryJoin(m, string.Empty);
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
        await TryJoin(target, p.Trim());
    }

    void ClosePassword() { if (_passwordOverlay != null) _passwordOverlay.SetActive(false); }

    async System.Threading.Tasks.Task TryJoin(Player1v1Match m, string password)
    {
        if (m != null && m.IsPrivate() && !OneVsOnePrivateRoomCode.IsValidFormat(password))
        {
            AppManager.Instance.DisplayNotification(TrucoTextosClient.CodigoInvalido4);
            return;
        }
        if (m != null && m.IsStaleFullVersusPhoton())
        {
            AppManager.Instance.DisplayNotification(TrucoTextosClient.SalaExpiradaAviso);
            return;
        }
        // Re-fetch this exact match from backend so we never join a stale row ("Match is full" on a new room).
        var fresh = await ApiController.GetMatch1v1(m._id);
        if (fresh != null) m = fresh;
        if (m == null || !m.ShouldShowInLobbyList())
        {
            AppManager.Instance.DisplayNotification(TrucoTextosClient.SalaExpiradaAviso);
            Refresh(showLoading: false);
            return;
        }
        int stake = m.GetEntryStake();
        if (!ClientBalanceOk(stake, out _)) { AppManager.Instance.DisplayNotification(TrucoTextosClient.SaldoInsuficiente); return; }
        AppManager.Instance.DisplayLoadingUI(TrucoTextosClient.Conectando);
        string joinApiMessage = null;
        var result = await ApiController.PlayerJoin1v1Match(m._id, password, m, err => { joinApiMessage = err; AppManager.Instance.DisplayNotification(err); });
        AppManager.Instance.HideLoadingUI();
        if (result == null)
        {
            if (string.IsNullOrEmpty(joinApiMessage))
                AppManager.Instance.DisplayNotification(TrucoTextosClient.ErrorUnirse);
            // Wallet may have changed even on failure — refresh balance from server.
            _ = ApiController.GetCurrentUserProfile();
            Refresh(showLoading: false);
            return;
        }
        string photon = result.ResolvePhotonRoomName();
        if (string.IsNullOrEmpty(photon)) photon = m.ResolvePhotonRoomName();
        if (string.IsNullOrEmpty(photon)) { AppManager.Instance.DisplayNotification(TrucoTextosClient.ErrorUnirse); return; }
        int fee = result.entryFee > 0 ? result.entryFee : (m.GetEntryStake() > 0 ? m.GetEntryStake() : m.entryFee);
        string id = !string.IsNullOrEmpty(result._id) ? result._id : m._id;
        bool withFlor = result != null ? result.withFlor : m.withFlor;
        OneVsOneMatchSession.SetGuestContext(id, photon, fee, withFlor);
        PrepareMatchmakingUi();
        if (_root != null) _root.SetActive(true);
        if (_photonFlow == null) _photonFlow = OneVsOnePhotonFlow.Instance;
        if (_photonFlow != null) _photonFlow.StartJoinPhoton();
        TrucoNotificationLog.Success(TrucoTextosClient.LogUnidoSala);
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
