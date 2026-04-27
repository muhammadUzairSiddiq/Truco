using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Panel: lista de salas GET /matches, crear con player-create, unirse con join + Photon.</summary>
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

    [Header("Contraseña (privada)")]
    [SerializeField] private GameObject _passwordOverlay;
    [SerializeField] private TMPro.TMP_InputField _passwordField;
    [SerializeField] private Button _passwordConfirm;
    [SerializeField] private Button _passwordCancel;

    [Header("Flujos")]
    [SerializeField] private OneVsOnePhotonFlow _photonFlow;
    [SerializeField] private GameObject _matchmakingScreen;

    [Tooltip("Cada cuántos segundos vuelve a consultar el backend (0 = desactiva).")]
    [SerializeField] private float _autoRefreshSeconds = 8f;
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
        if (_buttonRefresh != null) _buttonRefresh.onClick.AddListener(Refresh);
        if (_buttonCreate != null) _buttonCreate.onClick.AddListener(OnClickCreate);
        if (_buttonBack != null) _buttonBack.onClick.AddListener(Close);
        if (_passwordConfirm != null) _passwordConfirm.onClick.AddListener(ConfirmPasswordAndJoin);
        if (_passwordCancel != null) _passwordCancel.onClick.AddListener(() => { ClosePassword(); });
        _nextAutoRefresh = Time.unscaledTime + 1f;
        Refresh();
    }

    void OnDisable()
    {
        if (_buttonRefresh != null) _buttonRefresh.onClick.RemoveListener(Refresh);
        if (_buttonCreate != null) _buttonCreate.onClick.RemoveListener(OnClickCreate);
        if (_buttonBack != null) _buttonBack.onClick.RemoveListener(Close);
        if (_passwordConfirm != null) _passwordConfirm.onClick.RemoveListener(ConfirmPasswordAndJoin);
    }

    void Update()
    {
        if (_autoRefreshSeconds <= 0f) return;
        if (Time.unscaledTime < _nextAutoRefresh) return;
        if (_root != null && !_root.activeInHierarchy) return;
        _nextAutoRefresh = Time.unscaledTime + _autoRefreshSeconds;
        if (Time.unscaledTime - _lastRefreshTime < _minSecondsBetweenRefreshes) return;
        Refresh();
    }

    public void Open()
    {
        if (_root != null)
        {
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            var topCanvas = _root.GetComponent<Canvas>();
            if (topCanvas != null)
            {
                topCanvas.overrideSorting = true;
                topCanvas.sortingOrder = 32000;
            }
        }
        _nextAutoRefresh = Time.unscaledTime + 1f;
        Refresh();
    }

    public void Close()
    {
        if (_root != null) _root.SetActive(false);
    }

    public async void Refresh()
    {
        _lastRefreshTime = Time.unscaledTime;
        if (_scrollContent == null || _rowPrefab == null) return;
        AppManager.Instance.DisplayLoadingUI(TrucoTextosClient.Conectando);
        var list = await ApiController.FetchPlayer1v1MatchList();
        AppManager.Instance.HideLoadingUI();
        foreach (var v in _spawned)
            if (v != null) Destroy(v.gameObject);
        _spawned.Clear();
        if (list == null) return;
        foreach (var m in list.OrderBy(m => m.name ?? string.Empty))
        {
            if (!m.IsLobbyLikeStatus() && m.status != null) continue;
            var row = Instantiate(_rowPrefab, _scrollContent);
            row.gameObject.SetActive(true);
            bool can = m.CanClickJoinOnRoom();
            string joinTxt = null;
            if (!can)
            {
                if (m.GetTrucoPlayerCount() >= 2) joinTxt = TrucoTextosClient.SalaLlena;
                else if (m.IsCurrentUserHostOfRoom()) joinTxt = TrucoTextosClient.TuSalaEsperando;
            }
            row.Bind(m, OnClickJoin, can, joinTxt);
            _spawned.Add(row);
        }
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
        var body = new PlayerCreateMatchRequest
        {
            name = panel.GetRoomName(),
            type = panel.IsPublic() ? "public" : "private",
            cost = stake,
            prize = stake,
            maxPlayers = OneVsOneMatchSession.MaxPlayersPhoton,
            password = panel.IsPublic() ? string.Empty : panel.GetPassword()
        };
        AppManager.Instance.DisplayLoadingUI(TrucoTextosClient.Conectando);
        var created = await ApiController.PlayerCreate1v1Match(body, err => AppManager.Instance.DisplayNotification(err));
        AppManager.Instance.HideLoadingUI();
        if (created == null || string.IsNullOrEmpty(created._id)) { AppManager.Instance.DisplayNotification(TrucoTextosClient.ErrorCrearSala); return; }
        string photon = created.ResolvePhotonRoomName();
        if (string.IsNullOrEmpty(photon)) photon = OneVsOneMatchSession.BuildDefaultPhotonRoomName(created._id);
        bool reg = await ApiController.RegisterPhotonRoomName(created._id, photon, err => Debug.LogWarning(err));
        if (!reg) AppManager.Instance.DisplayNotification("No se pudo sincronizar el nombre de la sala con el servidor, pero se puede jugar. El admin puede no ver el nombre todavía.");
        OneVsOneMatchSession.SetHostContext(created._id, photon, fee);
        panel.Close();
        if (_matchmakingScreen != null) _matchmakingScreen.SetActive(false);
        if (_root != null) _root.SetActive(true);
        Refresh();
        if (_photonFlow == null) _photonFlow = OneVsOnePhotonFlow.Instance;
        if (_photonFlow != null) _photonFlow.StartHostPhoton(OneVsOneMatchSession.MaxPlayersPhoton);
    }

    void OnClickJoin(Player1v1Match m)
    {
        if (m == null) return;
        if (m.IsPrivate())
        {
            _pendingPrivateJoin = m;
            if (_passwordOverlay != null) _passwordOverlay.SetActive(true);
            if (_passwordField != null) _passwordField.text = string.Empty;
            return;
        }
        _ = TryJoin(m, string.Empty);
    }

    async void ConfirmPasswordAndJoin()
    {
        if (_pendingPrivateJoin == null) { ClosePassword(); return; }
        string p = _passwordField != null ? _passwordField.text : string.Empty;
        ClosePassword();
        await TryJoin(_pendingPrivateJoin, p);
        _pendingPrivateJoin = null;
    }

    void ClosePassword() { if (_passwordOverlay != null) _passwordOverlay.SetActive(false); }

    async System.Threading.Tasks.Task TryJoin(Player1v1Match m, string password)
    {
        int stake = m.GetEntryStake();
        if (!ClientBalanceOk(stake, out _)) { AppManager.Instance.DisplayNotification(TrucoTextosClient.SaldoInsuficiente); return; }
        AppManager.Instance.DisplayLoadingUI(TrucoTextosClient.Conectando);
        var result = await ApiController.PlayerJoin1v1Match(m._id, password, m, err => AppManager.Instance.DisplayNotification(err));
        AppManager.Instance.HideLoadingUI();
        if (result == null) { AppManager.Instance.DisplayNotification(TrucoTextosClient.ErrorUnirse); return; }
        string photon = result.ResolvePhotonRoomName();
        if (string.IsNullOrEmpty(photon)) photon = m.ResolvePhotonRoomName();
        if (string.IsNullOrEmpty(photon)) { AppManager.Instance.DisplayNotification(TrucoTextosClient.ErrorUnirse); return; }
        int fee = result.entryFee > 0 ? result.entryFee : (m.GetEntryStake() > 0 ? m.GetEntryStake() : m.entryFee);
        string id = !string.IsNullOrEmpty(result._id) ? result._id : m._id;
        OneVsOneMatchSession.SetGuestContext(id, photon, fee);
        if (_matchmakingScreen != null) _matchmakingScreen.SetActive(false);
        if (_root != null) _root.SetActive(true);
        if (_photonFlow == null) _photonFlow = OneVsOnePhotonFlow.Instance;
        if (_photonFlow != null) _photonFlow.StartJoinPhoton();
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
