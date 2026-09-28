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
                _playerCounterText.text = "Esperando jugadores…";

            return;
        }

        int maxPlayers = _currentTournamentRequest?.MaxPlayerCount ?? 0;
        _playerCounterText.text = $"Jugadores: {currentPlayerCount}/{maxPlayers}";

        if (currentPlayerCount >= maxPlayers && maxPlayers > 0)
        {
            _cancelTournamentMatchmakingBtn.gameObject.SetActive(false);
            _matchmakingInfoText.text = "Iniciando cuadro del torneo…";
        }
        else
        {
            _cancelTournamentMatchmakingBtn.gameObject.SetActive(true);

            _matchmakingInfoText.text = "Preparando tu próxima partida…";
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
        string flor = request.WithFlor ? "Con Flor" : "Sin Flor";
        _bracketInfoText.text = $"{bracketInfo}\n{flor} · {request.TargetScore} puntos · Premio: {request.Prize} TRUCOINS";

        UpdatePlayerCount(0);

        MultiplayerController.InitMultiplayerSession_Tournament(request,
            (error) =>
            {
                _contentHolder.gameObject.SetActive(false);
                AppManager.Instance.DisplayNotification("Error de emparejamiento: " + error);
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
                return "Semifinales → Final";
            case 8:
                return "Cuartos → Semifinales → Final";
            case 16:
                return "Octavos → Cuartos → Semifinales → Final";
            default:
                return $"Torneo de {maxPlayers} jugadores";
        }
    }


    #endregion
}
