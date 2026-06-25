using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Attempts Photon ReconnectAndRejoin after involuntary disconnect in Gameplay. Added at runtime by <see cref="GameManager"/> Awake (non-spectators). Requires the Photon room to have been created with PlayerTtl &gt; 0 (60s in NetworkManager, OneVsOnePhotonFlow, tournament rooms in MultiplayerController).</summary>
[RequireComponent(typeof(PhotonView))]
public class TrucoPunReconnectionManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private float _rejoinWindowSeconds = 55f;
    [SerializeField] private float _retryStepSeconds = 2f;
    [SerializeField] private float _fallbackPhaseSeconds = 15f;
    [Tooltip("How long the player who stayed waits for the opponent to reconnect before winning by walkover.")]
    [SerializeField] private float _opponentReconnectWindowSeconds = 60f;

    Coroutine _routine;
    Coroutine _opponentWatch;
    TrucoReconnectionUi _ui;

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

    // ───────── Opponent dropped: the player who STAYED sees a 60 s wait, then wins by walkover ─────────

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        if (!IsGameplayScene()) return;
        if (SpectatorContext.IsSpectator) return;
        if (GameManager.Instance != null && GameManager.Instance._gameEnded) return;
        // With PlayerTtl > 0 a disconnected player is marked inactive (can still rejoin within the TTL window).
        if (otherPlayer != null && otherPlayer.IsInactive && _opponentWatch == null)
            _opponentWatch = StartCoroutine(WaitForOpponentReconnect());
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);
        // Opponent came back in time — cancel the walkout countdown.
        if (_opponentWatch != null)
        {
            StopCoroutine(_opponentWatch);
            _opponentWatch = null;
            _ui?.Hide();
            AppManager.Instance?.DisplayNotification(TrucoTextosClient.ReconexOk);
        }
    }

    IEnumerator WaitForOpponentReconnect()
    {
        _ui = TrucoReconnectionUi.Ensure(transform);
        float deadline = Time.unscaledTime + _opponentReconnectWindowSeconds;
        while (Time.unscaledTime < deadline)
        {
            if (GameManager.Instance != null && GameManager.Instance._gameEnded) { _ui?.Hide(); _opponentWatch = null; yield break; }
            // If the opponent is active again, stop (OnPlayerEnteredRoom usually handles this first).
            bool opponentBack = false;
            foreach (var p in PhotonNetwork.PlayerListOthers)
                if (p != null && !p.IsInactive) { opponentBack = true; break; }
            if (opponentBack) { _ui?.Hide(); _opponentWatch = null; AppManager.Instance?.DisplayNotification(TrucoTextosClient.ReconexOk); yield break; }

            int rem = Mathf.Max(0, Mathf.CeilToInt(deadline - Time.unscaledTime));
            _ui?.Show(TrucoTextosClient.RivalReconectando, rem);
            yield return null;
        }
        _ui?.Hide();
        _opponentWatch = null;
        // Opponent never returned within the window → the player who stayed wins by walkover.
        if (GameManager.Instance != null)
            GameManager.Instance.WinByOpponentWalkover();
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
    }

    /// <summary>When ReconnectAndRejoin fails, try Connect + JoinRoom with last saved room (1v1 / quick match).</summary>
    IEnumerator TryJoinSavedRoomFallback(float phase2EndTime)
    {
        string room = OneVsOneMatchSession.PhotonRoomName;
        if (string.IsNullOrEmpty(room)) room = TrucoRoomPersistence.LastRoomName();
        if (string.IsNullOrEmpty(room)) yield break;

        if (!PhotonNetwork.IsConnected) PhotonNetwork.ConnectUsingSettings();

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
