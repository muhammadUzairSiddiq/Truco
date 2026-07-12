using Photon.Pun;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// If host or guest leaves the app / editor while waiting in a 1v1 lobby (no cards dealt),
/// cancel the backend match so the room disappears everywhere and entry is refunded.
/// </summary>
public class TrucoHostPreGameWatchdog : MonoBehaviour
{
    static TrucoHostPreGameWatchdog _instance;
    static bool _cancelScheduled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject(nameof(TrucoHostPreGameWatchdog));
        _instance = go.AddComponent<TrucoHostPreGameWatchdog>();
        DontDestroyOnLoad(go);
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
    }

#if UNITY_EDITOR
    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
            ScheduleCancelPreGameLobby();
    }
#endif

    void OnApplicationQuit() => ScheduleCancelPreGameLobby();

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
#endif
    }

    /// <summary>Legacy name — host or guest pre-game quit.</summary>
    public static void ScheduleCancelHostLobby() => ScheduleCancelPreGameLobby();

    public static void ScheduleCancelPreGameLobby()
    {
        if (_cancelScheduled) return;
        if (!OneVsOneMatchLifecycle.IsWaitingInPreGameLobby()) return;
        _cancelScheduled = true;
        TrucoDebugLog.Log(TrucoDebugLog.Category.OneVsOne,
            "PreGameWatchdog cancel scheduled host=" + OneVsOneMatchSession.IsHost
            + " match=" + (OneVsOneMatchSession.CurrentMatchId ?? "?"));
        _ = CancelAsync();
    }

    static async System.Threading.Tasks.Task CancelAsync()
    {
        try
        {
            string matchId = OneVsOneMatchSession.CurrentMatchId;
            if (string.IsNullOrEmpty(matchId)) return;
            if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
            await OneVsOneMatchLifecycle.CancelLobbyMatchAsync(matchId);
            OneVsOneMatchSession.Clear();
            TrucoActiveHostMatchStore.Clear();
            TrucoRoomPersistence.Clear();
            if (OneVsOnePhotonFlow.Instance != null)
                OneVsOnePhotonFlow.Instance.ResetPurpose();
        }
        finally
        {
            _cancelScheduled = false;
        }
    }
}
