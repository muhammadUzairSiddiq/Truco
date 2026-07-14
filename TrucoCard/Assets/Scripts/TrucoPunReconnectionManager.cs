using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Attempts Photon ReconnectAndRejoin after involuntary disconnect in Gameplay. Added at runtime by <see cref="GameManager"/> Awake (non-spectators). Requires the Photon room to have been created with PlayerTtl &gt; 0 (60s in NetworkManager, OneVsOnePhotonFlow, tournament rooms in MultiplayerController).</summary>
[RequireComponent(typeof(PhotonView))]
public class TrucoPunReconnectionManager : MonoBehaviourPunCallbacks
{
    public static TrucoPunReconnectionManager Instance { get; private set; }

    [SerializeField] private float _rejoinWindowSeconds = 55f;
    [SerializeField] private float _retryStepSeconds = 2f;
    [SerializeField] private float _fallbackPhaseSeconds = 15f;
    [Tooltip("How long the player who stayed waits for the opponent to reconnect before winning by walkover.")]
    [SerializeField] private float _opponentReconnectWindowSeconds = 60f;

    Coroutine _routine;
    Coroutine _opponentWatch;
    TrucoReconnectionUi _ui;
    int _opponentReconnectSecondsRemaining;

    public static bool IsWaitingForOpponentReconnect =>
        Instance != null && Instance._opponentWatch != null;

    public static int OpponentReconnectSecondsRemaining =>
        Instance != null ? Instance._opponentReconnectSecondsRemaining : 0;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (_opponentWatch != null) return;
        if (!IsGameplayScene() || SpectatorContext.IsSpectator) return;
        if (GameManager.Instance != null && GameManager.Instance._gameEnded) return;
        if (!PhotonNetwork.InRoom) return;
        if (IsOpponentInactiveOrGone())
            TryStartOpponentWatch();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        base.OnDisconnected(cause);
        if (!IsGameplayScene() || _routine != null) return;
        if (cause == DisconnectCause.DisconnectByClientLogic
            || cause == DisconnectCause.DisconnectByDisconnectMessage
            || cause == DisconnectCause.DisconnectByServerLogic)
        {
            return;
        }
        _routine = StartCoroutine(TryReconnectAndRejoin());
    }

    bool IsGameplayScene() => SceneManager.GetActiveScene().name == "Gameplay";

    static bool IsOpponentInactiveOrGone()
    {
        if (!PhotonNetwork.InRoom) return false;
        var others = PhotonNetwork.PlayerListOthers;
        if (others == null || others.Length == 0) return false;
        foreach (var p in others)
            if (p != null && !p.IsInactive) return false;
        return true;
    }

    // ───────── Opponent dropped: the player who STAYED sees a 60 s wait, then wins by walkover ─────────

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        if (!IsGameplayScene()) return;
        if (SpectatorContext.IsSpectator) return;
        if (GameManager.Instance != null && GameManager.Instance._gameEnded) return;
        if (otherPlayer == null || otherPlayer.IsLocal) return;
        TryStartOpponentWatch();
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();
        if (!IsGameplayScene() || SpectatorContext.IsSpectator) return;
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        _ui?.Hide();
        if (OneVsOneMatchSession.GameStarted && GameManager.Instance != null)
        {
            // Timers resume only after SyncMatchState — early restart freezes stale mano state.
            GameManager.Instance.RequestStateSyncAfterReconnect();
            AppManager.Instance?.DisplayNotification(TrucoTextosClient.ReconexOk);
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);
        if (!IsGameplayScene() || targetPlayer == null || targetPlayer.IsLocal) return;
        if (!targetPlayer.IsInactive && _opponentWatch != null)
        {
            StopCoroutine(_opponentWatch);
            _opponentWatch = null;
            _opponentReconnectSecondsRemaining = 0;
            _ui?.Hide();
            AppManager.Instance?.DisplayNotification(TrucoTextosClient.ReconexOk);
            GameManager.Instance?.RequestStateSyncAfterReconnect();
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);
        if (_opponentWatch != null)
        {
            StopCoroutine(_opponentWatch);
            _opponentWatch = null;
            _opponentReconnectSecondsRemaining = 0;
            _ui?.Hide();
            AppManager.Instance?.DisplayNotification(TrucoTextosClient.ReconexOk);
        }
    }

    void TryStartOpponentWatch()
    {
        if (_opponentWatch != null) return;
        if (!IsGameplayScene() || SpectatorContext.IsSpectator) return;
        if (GameManager.Instance != null && GameManager.Instance._gameEnded) return;
        if (!PhotonNetwork.InRoom) return;
        _opponentWatch = StartCoroutine(WaitForOpponentReconnect());
    }

    IEnumerator WaitForOpponentReconnect()
    {
        _ui = TrucoReconnectionUi.Ensure(transform);
        float deadline = Time.unscaledTime + _opponentReconnectWindowSeconds;
        TrucoDebugLog.Log(TrucoDebugLog.Category.Photon,
            "WaitForOpponentReconnect START window=" + _opponentReconnectWindowSeconds
            + "s match=" + (OneVsOneMatchSession.CurrentMatchId ?? "?"));
        while (Time.unscaledTime < deadline)
        {
            if (GameManager.Instance != null && GameManager.Instance._gameEnded)
            {
                _opponentReconnectSecondsRemaining = 0;
                _ui?.Hide();
                _opponentWatch = null;
                yield break;
            }

            bool opponentBack = false;
            foreach (var p in PhotonNetwork.PlayerListOthers)
                if (p != null && !p.IsInactive) { opponentBack = true; break; }
            if (opponentBack)
            {
                _opponentReconnectSecondsRemaining = 0;
                _ui?.Hide();
                _opponentWatch = null;
                AppManager.Instance?.DisplayNotification(TrucoTextosClient.ReconexOk);
                yield break;
            }

            int rem = Mathf.Max(0, Mathf.CeilToInt(deadline - Time.unscaledTime));
            _opponentReconnectSecondsRemaining = rem;
            _ui?.Show(TrucoTextosClient.RivalReconectando, rem);
            UpdateOpponentAbsentTurnBanner(rem);
            yield return null;
        }

        _opponentReconnectSecondsRemaining = 0;
        _ui?.Hide();
        _opponentWatch = null;
        if (GameManager.Instance != null)
            GameManager.Instance.WinByOpponentWalkover();
    }

    public static void UpdateOpponentAbsentTurnBanner(int secondsRemaining)
    {
        if (UIMANAGER.Instance == null) return;
        int sec = Mathf.Max(0, secondsRemaining);
        bool urgent = sec <= TrucoTextosClient.TurnoTimerUrgenteHastaSegundos;
        UIMANAGER.Instance.UpdateTurnText(
            TrucoTextosClient.FormatoBannerRivalAusenteConSegundos(sec), -1f, urgent, reconnectCountdown: true);
    }

    IEnumerator TryReconnectAndRejoin()
    {
        _ui = TrucoReconnectionUi.Ensure(transform);
        float phase1End = Time.unscaledTime + _rejoinWindowSeconds;
        float stepAccum = 0f;

        while (Time.unscaledTime < phase1End)
        {
            int rem = Mathf.Max(0, Mathf.CeilToInt(phase1End - Time.unscaledTime));
            _ui?.Show(TrucoTextosClient.ReconectandoOverlay, rem);

            if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
            {
                OnReconnectedOk();
                yield break;
            }

            stepAccum += Time.unscaledDeltaTime;
            if (stepAccum >= _retryStepSeconds)
            {
                stepAccum = 0f;
                if (PhotonNetwork.ReconnectAndRejoin())
                {
                    yield return new WaitForSecondsRealtime(0.5f);
                    if (PhotonNetwork.InRoom) { OnReconnectedOk(); yield break; }
                }
                if (!PhotonNetwork.IsConnected) PhotonNetwork.Reconnect();
            }
            yield return null;
        }

        var phase2Deadline = Time.unscaledTime + _fallbackPhaseSeconds;
        yield return TryJoinSavedRoomFallback(phase2Deadline);

        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            OnReconnectedOk();
            yield break;
        }

        _ui?.Hide();
        if (GameManager.Instance != null)
            GameManager.Instance.HandleReconnectionFailedExit();
        else
        {
            OneVsOneMatchSession.Clear();
            SceneManager.LoadScene("MainMenu");
        }
        _routine = null;
    }

    void OnReconnectedOk()
    {
        _ui?.Hide();
        AppManager.Instance?.DisplayNotification(TrucoTextosClient.ReconexOk);
        _routine = null;
        if (OneVsOneMatchSession.GameStarted)
        {
            // Do not RestartTurnTimers here — wait for SyncMatchState (hand may already be over).
            GameManager.Instance?.RequestStateSyncAfterReconnect();
        }
    }

    /// <summary>When ReconnectAndRejoin fails, try Connect + JoinRoom with last saved room (1v1 / quick match).</summary>
    IEnumerator TryJoinSavedRoomFallback(float phase2EndTime)
    {
        string room = OneVsOneMatchSession.PhotonRoomName;
        if (string.IsNullOrEmpty(room)) room = TrucoRoomPersistence.LastRoomName();
        if (string.IsNullOrEmpty(room)) yield break;

        if (!PhotonNetwork.IsConnected)
        {
            TrucoPhotonRegionSettings.ApplyToPhoton();
            PhotonNetwork.ConnectUsingSettings();
        }

        while (Time.unscaledTime < phase2EndTime)
        {
            int rem = Mathf.Max(0, Mathf.CeilToInt(phase2EndTime - Time.unscaledTime));
            _ui?.Show(TrucoTextosClient.ReconectandoOverlay, rem);

            if (PhotonNetwork.InRoom) yield break;
            if (PhotonNetwork.IsConnected && PhotonNetwork.NetworkClientState == ClientState.ConnectedToMaster)
                break;
            yield return null;
        }

        if (PhotonNetwork.InRoom) yield break;
        if (!PhotonNetwork.IsConnected) yield break;

        PhotonNetwork.JoinRoom(room);
        float waitUntil = Time.unscaledTime + 10f;
        while (Time.unscaledTime < waitUntil)
        {
            int rem = Mathf.Max(0, Mathf.CeilToInt(waitUntil - Time.unscaledTime));
            _ui?.Show(TrucoTextosClient.ReconectandoOverlay, rem);
            if (PhotonNetwork.InRoom) yield break;
            yield return null;
        }
    }
}
