using Photon.Pun;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// If host or guest leaves the app while waiting in a 1v1 lobby (no cards dealt),
/// cancel the backend match so the room disappears everywhere and entry is refunded.
/// Android force-kill usually fires <see cref="OnApplicationPause"/> (not Quit) — that path is required.
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
            ScheduleCancelPreGameLobby("EditorExitPlayMode");
    }
#endif

    void OnApplicationQuit() => ScheduleCancelPreGameLobby("ApplicationQuit");

    /// <summary>
    /// Mobile: swipe-from-recents / home often only pauses. Fire cancel immediately so PathLeave + /leave run.
    /// </summary>
    void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus) return;
        ScheduleCancelPreGameLobby("ApplicationPause");
    }

    void OnApplicationFocus(bool hasFocus)
    {
        // Do not cancel on focus loss alone (notification shade / keyboard). Pause covers home/recents kill.
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
#endif
    }

    /// <summary>Legacy name — host or guest pre-game quit.</summary>
    public static void ScheduleCancelHostLobby() => ScheduleCancelPreGameLobby("Legacy");

    public static void ScheduleCancelPreGameLobby(string reason = null)
    {
        if (_cancelScheduled) return;
        // Never cancel after match started / gameplay — forfeit/walkover paths own that.
        if (OneVsOneMatchSession.GameStarted) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (scene == "Gameplay") return;

        string matchId = OneVsOneMatchSession.CurrentMatchId;
        if (string.IsNullOrEmpty(matchId))
            matchId = TrucoActiveHostMatchStore.GetRememberedMatchId();
        bool waiting = OneVsOneMatchLifecycle.IsWaitingInPreGameLobby()
                       || (!string.IsNullOrEmpty(matchId) && !OneVsOneMatchSession.GameStarted);
        if (!waiting || string.IsNullOrEmpty(matchId)) return;

        _cancelScheduled = true;
        TrucoDebugLog.Log(TrucoDebugLog.Category.OneVsOne,
            "PreGameWatchdog cancel reason=" + (reason ?? "?")
            + " host=" + OneVsOneMatchSession.IsHost
            + " match=" + matchId
            + " scene=" + scene);
        _ = CancelAsync(matchId);
    }

    static async System.Threading.Tasks.Task CancelAsync(string matchId)
    {
        try
        {
            if (string.IsNullOrEmpty(matchId)) return;
            bool ok = await OneVsOneMatchLifecycle.CancelLobbyMatchAsync(matchId);
            if (ok)
            {
                if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
                OneVsOneMatchSession.Clear();
                TrucoRoomPersistence.Clear();
                if (OneVsOnePhotonFlow.Instance != null)
                    OneVsOnePhotonFlow.Instance.ResetPurpose();
                TrucoDebugLog.Log(TrucoDebugLog.Category.OneVsOne,
                    "PreGameWatchdog cancel DONE match=" + matchId);
            }
            else
            {
                TrucoDebugLog.Warn(TrucoDebugLog.Category.OneVsOne,
                    "PreGameWatchdog cancel not confirmed — keep match id for next login refund match=" + matchId);
            }
        }
        catch (System.Exception ex)
        {
            TrucoDebugLog.Warn(TrucoDebugLog.Category.OneVsOne,
                "PreGameWatchdog cancel FAILED: " + ex.Message);
        }
        finally
        {
            _cancelScheduled = false;
        }
    }
}
