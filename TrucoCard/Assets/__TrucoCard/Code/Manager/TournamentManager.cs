using MH.Multiplayer;
using System;
using System.Threading.Tasks;
using UnityEngine;

public class TournamentManager : SingeltonBase<TournamentManager>
{
    #region Setters/Private Variables

    [Header("References")]
    [SerializeField] private TournamentSelectionOverlay _tournamentsSelectionOverlay;
    [SerializeField] private TournamentMatchmakingOverlay _tournamentMatchmakingOverlay;
    [SerializeField] private TournamentWaitingRoomOverlay _tournamentWaitingRoomOverlay;
    [SerializeField] private TournamentPasswordInputOverlay _tournamentPasswordInputOverlay;

    #endregion

    #region Unity Methods

    private void OnEnable()
    {
        MultiplayerController.OnTournamentBrackedMatch += HandleTournamentMatchCreated;
        MultiplayerController.OnTournamentMatchFinalized += HandleTournamentMatchFinalized;
    }
    private void OnDisable()
    {
        MultiplayerController.OnTournamentBrackedMatch -= HandleTournamentMatchCreated;
        MultiplayerController.OnTournamentMatchFinalized -= HandleTournamentMatchFinalized;
    }

    #endregion

    #region Tournament Selection Overlay

    public async void DisplayTournamentSelectionUI()
    {
        AppManager.Instance.DisplayLoadingUI("Fetching Tournaments");
        
        bool result = await ApiController.RequestLatestTournaments();

        if (result)
        {
            await Task.Delay(3000);

            AppManager.Instance.HideLoadingUI();


            if (ApiController.GetActiveTournaments == null)
            {
                AppManager.Instance.DisplayNotification("No active tournaments available at the moment. Please check back later.");
            }
            else
            {
                Tournament[] tournaments = ApiController.GetActiveTournaments.TournamentsArray;

                if (tournaments.Length < 1)
                {
                    AppManager.Instance.DisplayNotification("No active tournaments available at the moment. Please check back later.");
                }
                else
                {
                    _tournamentsSelectionOverlay.DisplayTournamentSelectionUI(tournaments);
                    MainMenuViewCoordinator.EnsureTournamentNavPriority();
                }
            }
        }
        else
        {
            AppManager.Instance.DisplayNotification("Failed to fetch tournaments. Please try again later.");
        }

    }
    public void HideTournamentSelectionUI()
    {
        _tournamentsSelectionOverlay.HideTournamentSelectionUI();
    }

    #endregion

    #region Tournament Password Validation Overlay

    public void DisplayTournamentPasswordInputOverlay(string tournamentId, Action onSuccessAction = null)
    {
        _tournamentPasswordInputOverlay.InitValidation(tournamentId, onSuccessAction);
    }

    #endregion

    #region Tournament Matchmaking Overlay

    public void DisplayTournamentMatchmakingUI(TournamentRequest request)
    {
        _tournamentMatchmakingOverlay.DisplayTournamentMatchmakingUI(request);
    }
    public void HideTournamentMatchmakingUI()
    {
        _tournamentMatchmakingOverlay.CancelTournmentMatchmaking();
    }

    #endregion

    #region Tournament Waiting Room Overlay

    public void DisplayTournamentWaitingRoomOverlay()
    {
        _tournamentWaitingRoomOverlay.DisplayTournamentWaitingRoomUI();
    }
    public void HideTournamentWaitingRoomOverlay()
    {
        _tournamentWaitingRoomOverlay.HideTournamentWaitingRoomUI();
    }

    #endregion

    private void HandleTournamentMatchCreated()
    {
        _tournamentMatchmakingOverlay.HideTournamentMatchmakingUI();
        _tournamentsSelectionOverlay.HideTournamentSelectionUI();
        _tournamentWaitingRoomOverlay.DisplayTournamentWaitingRoomUI();
    }
    private void HandleTournamentMatchFinalized(string tournamentId)
    {


    }

}
