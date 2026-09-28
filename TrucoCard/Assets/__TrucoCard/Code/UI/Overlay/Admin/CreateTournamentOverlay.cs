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
    [SerializeField] private TMP_InputField _prizeInput;
    [SerializeField] private TMP_InputField _passwordInput;

    [Header("DropDown")]
    [SerializeField] private TMP_Dropdown _maxPlayersInput;
    [SerializeField] private TMP_Dropdown _pointsInput;
    [SerializeField] private TMP_Dropdown _typeInput;
    [SerializeField] private Toggle _withFlorToggle;

    [Header("Buttons")]
    [SerializeField] private Button _createTournamentBtn;
    [SerializeField] private Button _closeOverlayBtn;

    private void Start()
    {
        EnsureExtraFields();
        _createTournamentBtn.onClick.RemoveAllListeners();
        _createTournamentBtn.onClick.AddListener(CreateTournamentBtn_fn);
        _closeOverlayBtn.onClick.RemoveAllListeners();
        _closeOverlayBtn.onClick.AddListener(CloseOverlayBtn_fn);
    }

    void EnsureExtraFields()
    {
        var host = _tournamentEntryFeeInput != null ? _tournamentEntryFeeInput.transform.parent : transform;
        if (_prizeInput == null)
            _prizeInput = CloneInput(_tournamentEntryFeeInput, host, "Prize", "Premio TRUCOINS");
        if (_passwordInput == null)
            _passwordInput = CloneInput(_tournamentEntryFeeInput, host, "Password", "Contraseña (privado)");
        if (_pointsInput == null)
            _pointsInput = CloneDropdown(_maxPlayersInput, host, "Points", new[] { "15 puntos", "30 puntos" });
        if (_typeInput == null)
            _typeInput = CloneDropdown(_maxPlayersInput, host, "Type", new[] { "Público", "Privado" });
        if (_withFlorToggle == null)
        {
            var go = new GameObject("WithFlor", typeof(RectTransform), typeof(Toggle), typeof(Image));
            go.transform.SetParent(host, false);
            _withFlorToggle = go.GetComponent<Toggle>();
            _withFlorToggle.isOn = true;
            var label = new GameObject("L", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(go.transform, false);
            var tmp = label.GetComponent<TextMeshProUGUI>();
            tmp.text = "Con Flor";
            tmp.fontSize = 22f;
            tmp.color = Color.white;
            _withFlorToggle.onValueChanged.AddListener(on => { tmp.text = on ? "Con Flor" : "Sin Flor"; });
        }
        if (_maxPlayersInput != null && (_maxPlayersInput.options == null || _maxPlayersInput.options.Count < 2))
        {
            _maxPlayersInput.ClearOptions();
            _maxPlayersInput.AddOptions(new System.Collections.Generic.List<string> { "8 jugadores", "4 jugadores" });
        }
    }

    static TMP_InputField CloneInput(TMP_InputField template, Transform host, string name, string placeholder)
    {
        if (template == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            go.transform.SetParent(host, false);
            return go.GetComponent<TMP_InputField>();
        }
        var clone = Instantiate(template.gameObject, host);
        clone.name = name;
        var input = clone.GetComponent<TMP_InputField>();
        input.text = "";
        if (input.placeholder is TMP_Text ph) ph.text = placeholder;
        return input;
    }

    static TMP_Dropdown CloneDropdown(TMP_Dropdown template, Transform host, string name, string[] options)
    {
        TMP_Dropdown dd;
        if (template != null)
        {
            var clone = Instantiate(template.gameObject, host);
            clone.name = name;
            dd = clone.GetComponent<TMP_Dropdown>();
        }
        else
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TMP_Dropdown));
            go.transform.SetParent(host, false);
            dd = go.GetComponent<TMP_Dropdown>();
        }
        dd.ClearOptions();
        dd.AddOptions(new System.Collections.Generic.List<string>(options));
        dd.value = 0;
        return dd;
    }

    private async void CreateTournamentBtn_fn()
    {
        AppManager.Instance.DisplayLoadingUI("Creando torneo…");

        string name = _tournamentNameInput.text;
        string description = _tournamentDescriptionInput != null ? _tournamentDescriptionInput.text : name;
        string startDate = _tournamentStartDateInput != null ? _tournamentStartDateInput.text : "";
        string endDate = _tournamentEndDateInput != null ? _tournamentEndDateInput.text : "";

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(_tournamentEntryFeeInput.text))
        {
            AppManager.Instance.HideLoadingUI();
            AppManager.Instance.DisplayNotification("Completá nombre e ingreso.");
            return;
        }

        if (!int.TryParse(_tournamentEntryFeeInput.text, out int entryFeeVal))
        {
            AppManager.Instance.HideLoadingUI();
            AppManager.Instance.DisplayNotification("Ingreso inválido: usá un número entero.");
            return;
        }
        int maxPlayersVal = _maxPlayersInput != null && _maxPlayersInput.value == 1 ? 4 : 8;
        int prizeVal = 0;
        if (_prizeInput != null) int.TryParse(_prizeInput.text, out prizeVal);
        int pointsVal = _pointsInput != null && _pointsInput.value == 0 ? 15 : 30;
        bool withFlor = _withFlorToggle == null || _withFlorToggle.isOn;
        bool isPrivate = _typeInput != null && _typeInput.value == 1;
        string password = _passwordInput != null ? _passwordInput.text : "";

        if (prizeVal <= 0 || (isPrivate && string.IsNullOrWhiteSpace(password)))
        {
            AppManager.Instance.HideLoadingUI();
            AppManager.Instance.DisplayNotification("Ingresá un premio válido y una contraseña para torneos privados.");
            return;
        }

        if (entryFeeVal < 1)
        {
            AppManager.Instance.HideLoadingUI();
            AppManager.Instance.DisplayNotification("Ingreso inválido.");
            return;
        }

        CreateTournamentRequestAdmin newTournament = new CreateTournamentRequestAdmin
        {
            name = name,
            description = string.IsNullOrEmpty(description) ? name : description,
            startDate = startDate,
            endDate = endDate,
            entryFee = entryFeeVal,
            maxPlayers = maxPlayersVal,
            prize = prizeVal,
            withFlor = withFlor,
            targetScore = pointsVal,
            type = isPrivate ? "private" : "public",
            password = isPrivate ? password : ""
        };

        await ApiController.CreateTournamentByAdmin(newTournament, () =>
        {
            AppManager.Instance.HideLoadingUI();
            CloseOverlayBtn_fn();
            AppManager.Instance.DisplayNotification("Torneo creado.");
        },
        (ERR) =>
        {
            AppManager.Instance.HideLoadingUI();
            AppManager.Instance.DisplayNotification("Error al crear el torneo: " + ERR);
        });
    }

    private void CloseOverlayBtn_fn()
    {
        if (_tournamentNameInput != null) _tournamentNameInput.text = "";
        if (_tournamentDescriptionInput != null) _tournamentDescriptionInput.text = "";
        if (_tournamentStartDateInput != null) _tournamentStartDateInput.text = "";
        if (_tournamentEndDateInput != null) _tournamentEndDateInput.text = "";
        if (_tournamentEntryFeeInput != null) _tournamentEntryFeeInput.text = "";
        if (_prizeInput != null) _prizeInput.text = "";
        if (_passwordInput != null) _passwordInput.text = "";
        if (_maxPlayersInput != null) _maxPlayersInput.value = 0;
        if (_pointsInput != null) _pointsInput.value = 1;
        if (_typeInput != null) _typeInput.value = 0;
        if (_withFlorToggle != null) _withFlorToggle.isOn = true;
        gameObject.SetActive(false);
    }
}
