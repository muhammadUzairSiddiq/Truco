using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TournamentSelectionOverlay : MonoBehaviour
{
    #region Setters/Private Variables

    [SerializeField] private GameObject _contentHolder;
    [SerializeField] private RectTransform _tournamentsCardHolder;
    [SerializeField] private TournamentCardItem _tournamentCardPF;
    [SerializeField] private Button _closeTournamentSelectionUiBtn;

    private List<TournamentCardItem> _instantiatedCards = new List<TournamentCardItem>();

    #endregion

    #region Unity Methods

    private void Start()
    {
        _closeTournamentSelectionUiBtn.onClick.RemoveAllListeners();
        _closeTournamentSelectionUiBtn.onClick.AddListener(HideTournamentSelectionUI);
    }

    #endregion

    public void DisplayTournamentSelectionUI(Tournament[] activeTournaments)
    {
        foreach (var t in activeTournaments)
        {
            TournamentCardItem tournamentCard = Instantiate(_tournamentCardPF, _tournamentsCardHolder);
            tournamentCard.Init(t);

            _instantiatedCards.Add(tournamentCard);
        }

        _contentHolder.gameObject.SetActive(true);

        LayoutRebuilder.ForceRebuildLayoutImmediate(_tournamentsCardHolder);
        Canvas.ForceUpdateCanvases();
    }
    public void HideTournamentSelectionUI()
    {
        _contentHolder.gameObject.SetActive(false);

        foreach (var t in _instantiatedCards)
        {
            if (t != null)
                Destroy(t.gameObject);
        }

        _instantiatedCards.Clear();
    }

}
