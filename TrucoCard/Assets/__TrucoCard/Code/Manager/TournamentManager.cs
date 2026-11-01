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
    private bool _loadingTournamentList;
    private int _listGeneration;
    public bool IsTournamentBrowserVisible => _tournamentsSelectionOverlay != null && _tournamentsSelectionOverlay.IsVisible;

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

    /// <summary>Present the player screen immediately, before any resume/list network request.</summary>
    public bool ShowTournamentBrowser()
    {
        if (!TrucoPlayAccess.TryTournaments()) return false;
        if (_tournamentsSelectionOverlay == null)
        {
            Debug.LogError("TournamentSelectionOverlay is missing from TournamentManager.");
            AppManager.Instance?.DisplayNotification("No se pudo abrir la pantalla de torneos.");
            return false;
        }
        _tournamentsSelectionOverlay.DisplayLoading();
        MainMenuViewCoordinator.HideMenuContentForTournament();
        MainMenuViewCoordinator.EnsureTournamentNavPriority();
        return true;
    }

    public async void DisplayTournamentSelectionUI()
    {
#if UNITY_EDITOR
        if (TrucoEditorTournamentHooks.TryOpen()) return;
#endif
        if (!TrucoPlayAccess.TryTournaments()) return;
        if (_loadingTournamentList) return;
        if (!ShowTournamentBrowser()) return;
        int generation = ++_listGeneration;
        _loadingTournamentList = true;
        try
        {
            bool result = await ApiController.RequestLatestTournaments();
            if (this == null || generation != _listGeneration || !_tournamentsSelectionOverlay.IsVisible) return;
            if (!result)
            {
                _tournamentsSelectionOverlay.DisplayUnavailable();
                return;
            }
            Tournament[] tournaments = ApiController.GetActiveTournaments?.TournamentsArray ?? Array.Empty<Tournament>();
            _tournamentsSelectionOverlay.DisplayTournamentSelectionUI(tournaments);
            MainMenuViewCoordinator.HideMenuContentForTournament();
            MainMenuViewCoordinator.EnsureTournamentNavPriority();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Tournament list could not open: " + exception.GetType().Name);
            if (this != null && generation == _listGeneration && _tournamentsSelectionOverlay.IsVisible)
                _tournamentsSelectionOverlay.DisplayUnavailable();
        }
        finally { if (generation == _listGeneration) _loadingTournamentList = false; }
    }
    public void HideTournamentSelectionUI()
    {
        ++_listGeneration;
        _loadingTournamentList = false;
        _tournamentsSelectionOverlay?.HideTournamentSelectionUI();
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
