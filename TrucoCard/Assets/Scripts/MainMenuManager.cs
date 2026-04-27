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
        MainMenuViewCoordinator.Initialize();
    }

    private void Start()
    {
        TrucoReturnFromGameplayCleanup.ConsumeIfNeeded();
        MainMenuViewCoordinator.TryCompleteNavigationIfNeeded();
        WireLogoutButton();
        AppManager.Instance.DisplayLoadingUI("Please Wait...");


        MultiplayerController.LeaveTournamentMatchmaking(async () =>
        {
            await ApiController.GetCurrentUserProfile();

            AppManager.Instance.HideLoadingUI();
            UsernameMainMenuBinder.ApplyToScene();
            MainMenuViewCoordinator.TryCompleteNavigationIfNeeded();
            RegisterButtonEvents();

        });

    }

    public void StopMatchMaking()
    {
        PhotonNetwork.LeaveRoom(false);
        PhotonNetwork.Disconnect();
        matchMakingPanel.SetActive(false);
    }

    const string LoginSceneName = "LoginScreen";

    /// <summary>Logout, clear local API session, and open the login scene (used by the profile Logout button).</summary>
    public void LogoutToLogin()
    {
        if (AppManager.Instance != null)
        {
            AppManager.Instance.HideLoadingUI();
            AppManager.Instance.HideNotification();
        }
        if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
        ApiController.ClearClientSessionState();
        MainMenuViewCoordinator.DestroyBottomNavIfPresent();
        SceneManager.LoadScene(LoginSceneName, LoadSceneMode.Single);
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
