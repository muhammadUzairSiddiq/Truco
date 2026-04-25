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
        _descriptionText.SetText(_iTournamentData.description);


        _rulesText.text = $"Max Player: {_iTournamentData.maxPlayers}\nEntry Fee: {_iTournamentData.entryFee}\n Start Date: {_iTournamentData.startDate}\n End Date: {_iTournamentData.endDate}";

        _manageTournamentBtn.onClick.RemoveAllListeners();
        _manageTournamentBtn.onClick.AddListener(() =>
        {
            ManageTournamentBtn_fn();
        });

    }

    private void ManageTournamentBtn_fn()
    {

    }
}
