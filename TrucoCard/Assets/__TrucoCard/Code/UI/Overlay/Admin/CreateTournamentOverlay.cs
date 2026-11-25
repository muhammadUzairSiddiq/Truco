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
        ArrangeForm();
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
        }
        if (_maxPlayersInput != null)
        {
            _maxPlayersInput.ClearOptions();
            _maxPlayersInput.AddOptions(new System.Collections.Generic.List<string> { "4 jugadores", "8 jugadores" });
            _maxPlayersInput.value = 0;
        }
    }

    void ArrangeForm()
    {
        // Legacy scene fields and dynamically cloned fields otherwise share anchored positions.
        // Put every control into a labelled, scrollable form on both phone and desktop screens.
        var oldChildren = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in transform) oldChildren.Add(child.gameObject);
        var root = (RectTransform)transform;
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero; root.localScale = Vector3.one;
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true; canvas.sortingOrder = TrucoCanvasOrder.Screen;
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
        var form = TrucoClubUI.Panel("TournamentForm", transform, TrucoClubUI.Ink, Vector2.zero, Vector2.one).rectTransform;
        var safe = TrucoClubUI.Rect("SafeArea", form, Vector2.zero, Vector2.one);
        safe.gameObject.AddComponent<TrucoSafeArea>();
        TrucoClubUI.Text(safe, "CREAR TORNEO", 42, TrucoClubUI.Gold, new Vector2(.04f, .9f), new Vector2(.96f, .98f), true);
        var scrollArea = TrucoClubUI.Rect("Fields", safe, new Vector2(.05f, .12f), new Vector2(.95f, .9f));
        var scroll = scrollArea.gameObject.AddComponent<ScrollRect>();
        scrollArea.gameObject.AddComponent<RectMask2D>();
        scroll.viewport = scrollArea; scroll.horizontal = false;
        var content = TrucoClubUI.Rect("Content", scrollArea, new Vector2(0, 1), Vector2.one);
        content.pivot = new Vector2(.5f, 1);
        content.sizeDelta = new Vector2(0, 1420);
        scroll.content = content;
        int row = 0;
        AddFormField(content, _tournamentNameInput, "Nombre del torneo", row++);
        AddFormField(content, _tournamentDescriptionInput, "Descripción", row++);
        AddFormField(content, _maxPlayersInput, "Cantidad de jugadores", row++);
        AddFormField(content, _pointsInput, "Puntos para ganar", row++);
        AddFormField(content, _withFlorToggle, "Modo Flor", row++);
        AddFormField(content, _tournamentEntryFeeInput, "Entrada en Trucoins", row++);
        AddFormField(content, _prizeInput, "Premio en Trucoins", row++);
        AddFormField(content, _typeInput, "Visibilidad", row++);
        AddFormField(content, _passwordInput, "Contraseña para torneo privado", row++);
        AddFormField(content, _tournamentStartDateInput, "Inicio (fecha ISO)", row++);
        AddFormField(content, _tournamentEndDateInput, "Fin (fecha ISO)", row++);
        if (_tournamentEntryFeeInput != null) _tournamentEntryFeeInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        if (_prizeInput != null) _prizeInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        if (_passwordInput != null) _passwordInput.contentType = TMP_InputField.ContentType.Password;
        if (_withFlorToggle != null)
        {
            var image = _withFlorToggle.GetComponent<Image>();
            image.color = TrucoClubUI.Raised;
            _withFlorToggle.targetGraphic = image;
            var check = TrucoClubUI.Panel("Selected", _withFlorToggle.transform, TrucoClubUI.Green,
                new Vector2(.02f, .2f), new Vector2(.11f, .8f));
            _withFlorToggle.graphic = check;
            var label = TrucoClubUI.Text(_withFlorToggle.transform, "Con Flor", 27, TrucoClubUI.Cream,
                new Vector2(.12f, 0), Vector2.one, true);
            _withFlorToggle.onValueChanged.AddListener(on => label.text = on ? "Con Flor" : "Sin Flor");
        }
        _closeOverlayBtn = TrucoClubUI.Button(safe, "VOLVER", null, new Vector2(.05f, .025f), new Vector2(.46f, .105f), true);
        _createTournamentBtn = TrucoClubUI.Button(safe, "CREAR TORNEO", null, new Vector2(.48f, .025f), new Vector2(.95f, .105f));
        foreach (var child in oldChildren)
            if (child.transform.parent == transform) child.SetActive(false);
    }

    static void AddFormField(RectTransform parent, Component field, string caption, int index)
    {
        if (field == null) return;
        var row = TrucoClubUI.Rect(caption, parent, new Vector2(0, 1), Vector2.one);
        row.pivot = new Vector2(.5f, 1); row.sizeDelta = new Vector2(0, 124); row.anchoredPosition = new Vector2(0, -index * 128);
        TrucoClubUI.Text(row, caption, 25, TrucoClubUI.Gold, new Vector2(0, .6f), Vector2.one, true);
        var rt = (RectTransform)field.transform;
        rt.SetParent(row, false); rt.anchorMin = new Vector2(.015f, .03f); rt.anchorMax = new Vector2(.985f, .61f);
        rt.offsetMin = rt.offsetMax = Vector2.zero; rt.localScale = Vector3.one;
        field.gameObject.SetActive(true);
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
        int maxPlayersVal = _maxPlayersInput != null && _maxPlayersInput.value == 1 ? 8 : 4;
        int prizeVal = 0;
        if (_prizeInput != null) int.TryParse(_prizeInput.text, out prizeVal);
        int pointsVal = _pointsInput != null && _pointsInput.value == 1 ? 30 : 15;
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
        if (_pointsInput != null) _pointsInput.value = 0;
        if (_typeInput != null) _typeInput.value = 0;
        if (_withFlorToggle != null) _withFlorToggle.isOn = true;
        gameObject.SetActive(false);
    }
}
