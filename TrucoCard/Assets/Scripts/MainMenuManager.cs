using MH.Multiplayer;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-200)]
public class MainMenuManager : MonoBehaviour
{
    public GameObject matchMakingPanel;
    [SerializeField] private Button _tournamentsBtn;

    void Awake()
    {
        TrucoDebugLog.Always(TrucoDebugLog.Category.MainMenu,
            "MainMenu Awake scene=" + SceneManager.GetActiveScene().name
            + " logFile=" + TrucoDebugLog.LogFilePath);
        TrucoReturnFromGameplayCleanup.ConsumeIfNeeded();
        MainMenuViewCoordinator.Initialize();
    }

    private void Start()
    {
        TrucoDebugLog.Always(TrucoDebugLog.Category.MainMenu, "MainMenu Start");
        TrucoReturnFromGameplayCleanup.ConsumeIfNeeded();
        MainMenuViewCoordinator.TryCompleteNavigationIfNeeded();
        WireLogoutButton();
        AppManager.Instance.DisplayLoadingUI(TrucoTextosClient.PleaseWait);


        MultiplayerController.LeaveTournamentMatchmaking(async () =>
        {
            await ApiController.GetCurrentUserProfile();
            TrucoDebugLog.Always(TrucoDebugLog.Category.MainMenu,
                "MainMenu profile loaded user=" + (ApiController.GetSessionUser?.Data?.username ?? "?"));
            // Abandoned unused rooms: refund on next login even if the previous process was killed.
            string leftover = TrucoActiveHostMatchStore.GetRememberedMatchId();
            int leftoverFee = TrucoActiveHostMatchStore.GetRememberedEntryFee();
            if (!string.IsNullOrEmpty(leftover) && !OneVsOneMatchSession.GameStarted)
                await OneVsOneMatchLifecycle.CancelLobbyMatchAsync(leftover,
                    leftoverFee > 0 ? leftoverFee : OneVsOneMatchSession.EntryFee);
            await OneVsOneMatchLifecycle.PurgeAllMyActiveLobbyMatchesAsync();
            await ApiController.GetCurrentUserProfile();

            AppManager.Instance.HideLoadingUI();
            UsernameMainMenuBinder.ApplyToScene();
            TrucoWalletHudRefresh.Apply();
            MainMenuViewCoordinator.TryCompleteNavigationIfNeeded();
            MainMenuViewCoordinator.EnsureBottomNavVisible();
            RegisterButtonEvents();

        });

    }

    public void StopMatchMaking()
    {
        if (OneVsOneMatchLifecycle.IsWaitingInPreGameLobby())
        {
            TrucoLobbyLeaveConfirm.RunIfNeeded(() =>
            {
                PhotonNetwork.Disconnect();
                if (matchMakingPanel != null) matchMakingPanel.SetActive(false);
            });
            return;
        }

        string matchId = OneVsOneMatchSession.CurrentMatchId;
        PhotonNetwork.LeaveRoom(false);
        PhotonNetwork.Disconnect();
        if (!string.IsNullOrEmpty(matchId))
            _ = OneVsOneMatchLifecycle.CancelLobbyMatchAsync(matchId);
        OneVsOneMatchSession.Clear();
        if (OneVsOnePhotonFlow.Instance != null) OneVsOnePhotonFlow.Instance.ResetPurpose();
        TrucoLobbyMatchmakingUi.HideWaitingOverlay();
        if (matchMakingPanel != null) matchMakingPanel.SetActive(false);
    }

    const string LoginSceneName = "LoginScreen";

    /// <summary>Logout, clear local API session, and open the login scene (used by the profile Logout button).</summary>
    public async void LogoutToLogin()
    {
        if (AppManager.Instance != null)
        {
            AppManager.Instance.HideLoadingUI();
            AppManager.Instance.HideNotification();
        }
        // The auth token is required by /leave. Refund the unused table before clearing
        // the session or disconnecting Photon, otherwise logout can orphan the stake.
        if (OneVsOneMatchLifecycle.IsWaitingInPreGameLobby())
        {
            string matchId = OneVsOneMatchSession.CurrentMatchId;
            int expectedRefund = OneVsOneMatchSession.EntryFee;
            bool refunded = await OneVsOneMatchLifecycle.CancelLobbyMatchAsync(matchId, expectedRefund);
            if (!refunded)
            {
                AppManager.Instance?.DisplayNotification(TrucoTextosClient.ErrorEliminarSala);
                return;
            }
        }
        //
        if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
        TrucoActiveHostMatchStore.Clear();
        ApiController.ClearClientSessionState();
        MainMenuViewCoordinator.DestroyBottomNavIfPresent();
        TrucoSceneTransition.Go(LoginSceneName);
    }

    [System.Obsolete("Use LogoutToLogin — build index 0 is Init, not the login form.")]
    public void GoToLogin()
    {
        LogoutToLogin();
    }

    private void RegisterButtonEvents()
    {
        _tournamentsBtn.onClick.RemoveAllListeners();
        _tournamentsBtn.onClick.AddListener(() =>
        {
            MainMenuViewCoordinator.TriggerTournamentFromMain();
        });
        WireLogoutButton();
    }

    void WireLogoutButton()
    {
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (b == null || b.gameObject.scene != gameObject.scene) continue;
            if (b.gameObject.name != "logout Button") continue;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(LogoutToLogin);
            break;
        }
    }
    
}
