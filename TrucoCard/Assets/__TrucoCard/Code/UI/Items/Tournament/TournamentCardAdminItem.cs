using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TournamentCardAdminItem : MonoBehaviour
{
    #region Setters/Private Variables

    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _rulesText;

    [SerializeField] private Button _manageTournamentBtn;

    [Header("Read-Only")]
    [SerializeField] private Tournament _iTournamentData;

    #endregion

    public void Init(Tournament tournament)
    {
        _iTournamentData = tournament;

        _titleText.SetText(_iTournamentData.name);
        if (_descriptionText != null)
            _descriptionText.SetText(string.IsNullOrEmpty(_iTournamentData.description) ? "" : _iTournamentData.description);

        string flor = _iTournamentData.withFlor ? "Con Flor" : "Sin Flor";
        string type = _iTournamentData.IsPrivateTournament() ? "Privado" : "Público";
        int pts = TrucoMatchRules.NormalizeTargetScore(_iTournamentData.targetScore);
        _rulesText.text = $"{_iTournamentData.maxPlayers} jugadores  ·  {flor}  ·  {pts} pts\n"
                          + $"{type}  ·  Ingreso {_iTournamentData.entryFee}  ·  Premio {_iTournamentData.prizePool}";

        if (_manageTournamentBtn == null) return;
        _manageTournamentBtn.onClick.RemoveAllListeners();
        _manageTournamentBtn.onClick.AddListener(() =>
        {
            ManageTournamentOverlay.Open(_iTournamentData);
        });
    }
}
