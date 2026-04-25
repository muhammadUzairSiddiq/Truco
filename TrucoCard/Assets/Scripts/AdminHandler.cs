using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Threading.Tasks;

public class AdminHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CreateTournamentOverlay _createTournamentOverlay;
    [SerializeField] private AllTournamentsOverlay _allTournamentsOverlay;

    [Header("Buttons")]
    [SerializeField] private Button _fetchAllTournaments;
    [SerializeField] private Button _addNewTournament;

    [Header("Read-Only")]
    [SerializeField] private List<Tournament> _tournaments = new List<Tournament>();

    private bool _isAdminVerified = false;


    private void Start()
    {
        VerifyAdminAccess();

        RegisterButtonEvents();
    }


    #region Register Button Events

    private void RegisterButtonEvents()
    {
        _fetchAllTournaments.onClick.RemoveAllListeners();
        _addNewTournament.onClick.RemoveAllListeners();

        _fetchAllTournaments.onClick.AddListener(FetchAllTournamentsBtn_fn);
        _addNewTournament.onClick.AddListener(AddNewTournamentBtn_fn);
    }

    private async void FetchAllTournamentsBtn_fn()
    {
        if (!_isAdminVerified)
        {
            AppManager.Instance.DisplayNotification("Admin Access Required to Access All Tournaments");
            return;
        }

        AppManager.Instance.DisplayLoadingUI("Fetching All Tournaments");

        _tournaments = await ApiController.GetAllTournamentsAdmin();

        _allTournamentsOverlay.Init(_tournaments.ToArray());
    }

    private void AddNewTournamentBtn_fn()
    {
        if (!_isAdminVerified)
        {
            AppManager.Instance.DisplayNotification("Admin Access Required to Add Tournament");
            return;
        }

        _createTournamentOverlay.gameObject.SetActive(true);
    }

    #endregion


    private async void VerifyAdminAccess()
    {
        AppManager.Instance.DisplayLoadingUI("Verifying Admin Access");

        _isAdminVerified = await ApiController.CheckUserAdmin();

        if (!_isAdminVerified)
        {
            AppManager.Instance.DisplayNotification("Non-Admin Access Detected, Admin functions will be disabled.");
            DisableAdminUI();
        }
        else
        {
            AppManager.Instance.DisplayNotification("Admin Access Verified");
        }
    }

    private void DisableAdminUI()
    {
         _fetchAllTournaments.interactable = false;
    }
}
