using TMPro;
using UnityEngine;
using MH.Multiplayer;

public class TournamentWaitingRoomOverlay : MonoBehaviour
{
    #region Setters/Private Variables

    [SerializeField] private GameObject _contentHolder;
    [SerializeField] private TextMeshProUGUI _tournamentNameText;
    [SerializeField] private TextMeshProUGUI _currentBracketInfoText;

    #endregion

    public void DisplayTournamentWaitingRoomUI()
    {
        _contentHolder.SetActive(true);
        
        // Set tournament info when displaying the UI
        SetTournamentInfo();
    }
    public void HideTournamentWaitingRoomUI()
    {
        _contentHolder.SetActive(false);
    }

    #region Class Utility

    public void SetTournamentInfo()
    {
        if (!MultiplayerController.IsTournamentActive)
        {
            _tournamentNameText.text = "No Active Tournament";
            _currentBracketInfoText.text = "Waiting for tournament...";
            return;
        }

        TournamentRequest currentTournament = MultiplayerController.CurrentTournamentRequest;
        int currentBracketLevel = MultiplayerController.CurrentBracketLevel.roundNumber;
        _currentBracketInfoText.text = MultiplayerController.CurrentBracketLevel.bracketTitle;

        if (!string.IsNullOrEmpty(currentTournament.TournamentName))
        {
            _tournamentNameText.text = currentTournament.TournamentName;
        }
        else
        {
            _tournamentNameText.text = $"Tournament {currentTournament.TournamentId}";
        }
    }

    #endregion
}
