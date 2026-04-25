using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MH.Multiplayer;

public class TournamentMatchmakingOverlay : MonoBehaviour
{
    #region Setters/Private Variables

    [SerializeField] private RectTransform _contentHolder;
    [Space][SerializeField] private TextMeshProUGUI _playerCounterText;
    [SerializeField] private TextMeshProUGUI _tournamentNameText;
    [SerializeField] private TextMeshProUGUI _bracketInfoText;
    [SerializeField] private TextMeshProUGUI _matchmakingInfoText;
    [Space][SerializeField] private Button _cancelTournamentMatchmakingBtn;

    [Header("Read-Only")]
    [SerializeField] private TournamentRequest _currentTournamentRequest;

    #endregion

    #region Unity Methods

    private void OnEnable()
    {
        MultiplayerController.OnPlayerCountUpdated += UpdatePlayerCount;
    }
    private void OnDisable()
    {
        MultiplayerController.OnPlayerCountUpdated -= UpdatePlayerCount;
    }

    private void Start()
    {
        _cancelTournamentMatchmakingBtn.onClick.RemoveAllListeners();
        _cancelTournamentMatchmakingBtn.onClick.AddListener(CancelTournmentMatchmaking);
    }

    #endregion

    #region Multiplayer Event Handlers

    private void UpdatePlayerCount(int currentPlayerCount)
    {
        if (currentPlayerCount < 2)
        {
            if (currentPlayerCount == 0)
                _playerCounterText.text = "";
            else
                _playerCounterText.text = "Waiting For Other Players";

            return;
        }

        int maxPlayers = _currentTournamentRequest?.MaxPlayerCount ?? 0;
        _playerCounterText.text = $"Players: {currentPlayerCount}/{maxPlayers}";

        if (currentPlayerCount >= maxPlayers && maxPlayers > 0)
        {
            _cancelTournamentMatchmakingBtn.gameObject.SetActive(false);
            _matchmakingInfoText.text = "Starting tournament Battle";
        }
        else
        {
            _cancelTournamentMatchmakingBtn.gameObject.SetActive(true);

            _matchmakingInfoText.text = "Preparing your next tournament battle";
        }
    }

    #endregion

    public void DisplayTournamentMatchmakingUI(TournamentRequest request)
    {
        _currentTournamentRequest = request;
        _contentHolder.gameObject.SetActive(true);

        _tournamentNameText.text = request.TournamentName;

        //int bracketLevels = CalculateBracketLevels(request.MaxPlayerCount);
        string bracketInfo = GetBracketLevelNames(request.MaxPlayerCount);
        _bracketInfoText.text = $"Tournament Format: {bracketInfo}";

        UpdatePlayerCount(0);

        MultiplayerController.InitMultiplayerSession_Tournament(request,
            (error) =>
            {
                _contentHolder.gameObject.SetActive(false);
                AppManager.Instance.DisplayNotification("Matchmaking Error: " + error);
            });
    }
    public void HideTournamentMatchmakingUI()
    {
        _contentHolder.gameObject.SetActive(false);
    }
    public void CancelTournmentMatchmaking()
    {
        MultiplayerController.LeaveTournamentMatchmaking(() =>
        {
            _contentHolder.gameObject.SetActive(false);
        });
    }


    #region Class Utility Methods

    private string GetBracketLevelNames(int maxPlayers)
    {
        switch (maxPlayers)
        {
            case 4:
                return "Semi-Final → Final";
            case 8:
                return "Quarter-Final → Semi-Final → Final";
            case 16:
                return "Round of 16 → Quarter-Final → Semi-Final → Final";
            default:
                return $"{maxPlayers} Player Tournament";
        }
    }


    #endregion
}
