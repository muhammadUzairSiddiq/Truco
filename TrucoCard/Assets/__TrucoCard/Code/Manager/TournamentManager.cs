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
        if (!TrucoPlayAccess.TryTournaments()) return;
        AppManager.Instance.DisplayLoadingUI("Buscando torneos…");
        
        bool result = await ApiController.RequestLatestTournaments();

        if (result)
        {
            await Task.Delay(3000);

            AppManager.Instance.HideLoadingUI();


            if (ApiController.GetActiveTournaments == null)
            {
                AppManager.Instance.DisplayNotification("No hay torneos activos por ahora. Volvé más tarde.");
            }
            else
            {
                Tournament[] tournaments = ApiController.GetActiveTournaments.TournamentsArray;

                if (tournaments.Length < 1)
                {
                    AppManager.Instance.DisplayNotification("No hay torneos activos por ahora. Volvé más tarde.");
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
            AppManager.Instance.HideLoadingUI();
            AppManager.Instance.DisplayNotification("No se pudieron cargar los torneos. Probá de nuevo.");
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
        // This is a presentation-only operation. Calling Cancel here re-enters
        // MultiplayerController.LeaveTournamentMatchmaking(), which calls this
        // method again and causes a fatal StackOverflowException on MainMenu load.
        _tournamentMatchmakingOverlay.HideTournamentMatchmakingUI();
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
