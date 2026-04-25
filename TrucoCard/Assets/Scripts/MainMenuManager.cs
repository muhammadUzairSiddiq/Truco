using MH.Multiplayer;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    public GameObject matchMakingPanel;
    [SerializeField] private Button _tournamentsBtn;

    private void Start()
    {
        AppManager.Instance.DisplayLoadingUI("Please Wait...");


        MultiplayerController.LeaveTournamentMatchmaking(async () =>
        {
            await ApiController.GetCurrentUserProfile();

            AppManager.Instance.HideLoadingUI();
            RegisterButtonEvents();

        });

    }

    public void StopMatchMaking()
    {
        PhotonNetwork.LeaveRoom(false);
        PhotonNetwork.Disconnect();
        matchMakingPanel.SetActive(false);
    }

    public void GoToLogin()
    {
        SceneManager.LoadScene(0);
    }

    private void RegisterButtonEvents()
    {
        _tournamentsBtn.onClick.RemoveAllListeners();
        _tournamentsBtn.onClick.AddListener(() =>
        {
            TournamentManager.Instance.DisplayTournamentSelectionUI();
        });
    }
    
}
