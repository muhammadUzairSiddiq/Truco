using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Admin-only: crea torneos vía API; los jugadores no crean torneos desde el cliente.</summary>
public class CreateTournamentOverlay : MonoBehaviour
{
    [Header("Input Fields")]
    [SerializeField] private TMP_InputField _tournamentNameInput;
    [SerializeField] private TMP_InputField _tournamentDescriptionInput;
    [SerializeField] private TMP_InputField _tournamentStartDateInput;
    [SerializeField] private TMP_InputField _tournamentEndDateInput;
    [SerializeField] private TMP_InputField _tournamentEntryFeeInput;

    [Header("DropDown")]
    [SerializeField] private TMP_Dropdown _maxPlayersInput;

    [Header("Buttons")]
    [SerializeField] private Button _createTournamentBtn;
    [SerializeField] private Button _closeOverlayBtn;



    private void Start()
    {
        _createTournamentBtn.onClick.RemoveAllListeners();
        _createTournamentBtn.onClick.AddListener(CreateTournamentBtn_fn);
        _closeOverlayBtn.onClick.RemoveAllListeners();
        _closeOverlayBtn.onClick.AddListener(CloseOverlayBtn_fn);
    }

    private async void CreateTournamentBtn_fn()
    {

        AppManager.Instance.DisplayLoadingUI("Creating Tournament...");

        string name = _tournamentNameInput.text;
        string description = _tournamentDescriptionInput.text;
        string startDate = _tournamentStartDateInput.text;
        string endDate = _tournamentEndDateInput.text;

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(description) || string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate) || string.IsNullOrEmpty(_tournamentEntryFeeInput.text))
        {
            AppManager.Instance.DisplayNotification("All fields are required.");
            return;
        }

        int entryFeeVal = int.Parse(_tournamentEntryFeeInput.text);
        int maxPlayersVal = _maxPlayersInput.value == 1 ? 4 : 8;

        if (entryFeeVal < 1)
        {
            AppManager.Instance.DisplayNotification("Invalid entry fee.");
            return;
        }

        CreateTournamentRequestAdmin newTournament = new CreateTournamentRequestAdmin
        {
            name = name,
            description = description,
            startDate = startDate,
            endDate = endDate,
            entryFee = entryFeeVal,
            maxPlayers = maxPlayersVal
        };

        await ApiController.CreateTournamentByAdmin(newTournament, () =>
        {
            CloseOverlayBtn_fn();

            AppManager.Instance.DisplayNotification("Tournament Created Successfully");
        }, 
        (ERR) =>
        {
            AppManager.Instance.DisplayNotification("Error Creating Tournament: " + ERR);

        });


    }
    private void CloseOverlayBtn_fn()
    {
        _tournamentNameInput.text = "";
        _tournamentDescriptionInput.text = "";
        _tournamentStartDateInput.text = "";
        _tournamentEndDateInput.text = "";
        _tournamentEntryFeeInput.text = "";
        _maxPlayersInput.value = 0;

        gameObject.SetActive(false);
    }
}
