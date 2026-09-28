using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Threading.Tasks;
using TMPro;

public class AdminHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CreateTournamentOverlay _createTournamentOverlay;
    [SerializeField] private AllTournamentsOverlay _allTournamentsOverlay;
    [SerializeField] private AdminLiveDashboardOverlay _liveDashboard;
    [SerializeField] private AdminControlCenterOverlay _controlCenter;

    [Header("Buttons")]
    [SerializeField] private Button _fetchAllTournaments;
    [SerializeField] private Button _addNewTournament;
    [Tooltip("If empty, a third button is cloned from Add New Tournament at runtime.")]
    [SerializeField] private Button _liveDashboardButton;
    [SerializeField] private Button _controlCenterButton;

    [Header("Read-Only")]
    [SerializeField] private List<Tournament> _tournaments = new List<Tournament>();

    private bool _isAdminVerified = false;


    private void Start()
    {
        EnsureLiveDashboardHost();
        VerifyAdminAccess();

        RegisterButtonEvents();
    }


    #region Register Button Events

    private void RegisterButtonEvents()
    {
        _fetchAllTournaments.onClick.RemoveAllListeners();
        _addNewTournament.onClick.RemoveAllListeners();
        if (_liveDashboardButton != null)
            _liveDashboardButton.onClick.RemoveAllListeners();
        if (_controlCenterButton != null)
            _controlCenterButton.onClick.RemoveAllListeners();

        _fetchAllTournaments.onClick.AddListener(FetchAllTournamentsBtn_fn);
        _addNewTournament.onClick.AddListener(AddNewTournamentBtn_fn);
        if (_liveDashboardButton != null)
            _liveDashboardButton.onClick.AddListener(LiveDashboardBtn_fn);
        if (_controlCenterButton != null)
            _controlCenterButton.onClick.AddListener(ControlCenterBtn_fn);
    }

    void EnsureLiveDashboardHost()
    {
        if (_liveDashboard == null)
        {
            var canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null) return;
            var host = new GameObject("AdminLiveDashboardHost", typeof(RectTransform), typeof(AdminLiveDashboardOverlay));
            host.transform.SetParent(canvas.transform, false);
            var hrt = host.GetComponent<RectTransform>();
            hrt.anchorMin = Vector2.zero;
            hrt.anchorMax = Vector2.one;
            hrt.offsetMin = Vector2.zero;
            hrt.offsetMax = Vector2.zero;
            host.transform.SetAsLastSibling();
            _liveDashboard = host.GetComponent<AdminLiveDashboardOverlay>();
        }

        if (_controlCenter == null)
        {
            var canvas = Object.FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                var host = new GameObject("AdminControlCenterHost", typeof(RectTransform), typeof(AdminControlCenterOverlay));
                host.transform.SetParent(canvas.transform, false);
                var hrt = host.GetComponent<RectTransform>();
                hrt.anchorMin = Vector2.zero;
                hrt.anchorMax = Vector2.one;
                hrt.offsetMin = Vector2.zero;
                hrt.offsetMax = Vector2.zero;
                host.transform.SetAsLastSibling();
                _controlCenter = host.GetComponent<AdminControlCenterOverlay>();
            }
        }

        if (_liveDashboardButton == null && _addNewTournament != null)
        {
            var src = _addNewTournament.transform;
            var dup = Instantiate(src.gameObject, src.parent);
            dup.name = "LiveDashboardBtn";
            _liveDashboardButton = dup.GetComponent<Button>();
            if (_liveDashboardButton != null)
            {
                _liveDashboardButton.onClick.RemoveAllListeners();
                var tmp = dup.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null)
                    tmp.text = TrucoTextosClient.LiveDashboardButton;
            }
        }

        if (_controlCenterButton == null && _addNewTournament != null)
        {
            var src = _addNewTournament.transform;
            var dup = Instantiate(src.gameObject, src.parent);
            dup.name = "ControlCenterBtn";
            _controlCenterButton = dup.GetComponent<Button>();
            if (_controlCenterButton != null)
            {
                _controlCenterButton.onClick.RemoveAllListeners();
                var tmp = dup.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null)
                    tmp.text = TrucoTextosClient.AdminControlButton;
            }
        }
    }

    private void ControlCenterBtn_fn()
    {
        if (!_isAdminVerified)
        {
            AppManager.Instance.DisplayNotification("Se requiere acceso de admin");
            return;
        }
        if (_controlCenter == null) return;
        _controlCenter.Open();
    }

    private void LiveDashboardBtn_fn()
    {
        if (!_isAdminVerified)
        {
            AppManager.Instance.DisplayNotification("Se requiere acceso de admin para el panel en vivo");
            return;
        }

        if (_liveDashboard == null) return;
        _liveDashboard.Open();
    }

    private async void FetchAllTournamentsBtn_fn()
    {
        if (!_isAdminVerified)
        {
            AppManager.Instance.DisplayNotification("Se requiere acceso de admin para ver los torneos");
            return;
        }

        AppManager.Instance.DisplayLoadingUI("Cargando torneos…");

        _tournaments = await ApiController.GetAllTournamentsAdmin();
        if (_tournaments == null) _tournaments = new List<Tournament>();

        _allTournamentsOverlay.Init(_tournaments.ToArray());
    }

    private void AddNewTournamentBtn_fn()
    {
        if (!_isAdminVerified)
        {
            AppManager.Instance.DisplayNotification("Se requiere acceso de admin para crear torneos");
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
            var email = ApiController.GetSessionUser?.Data?.email;
            if (PostLoginSceneRouter.IsBuiltInAdminSessionEmail(email))
            {
                _isAdminVerified = true;
                AppManager.Instance.DisplayNotification("Admin access (built-in test account) verified");
            }
            else
            {
                AppManager.Instance.DisplayNotification("Non-Admin Access Detected, Admin functions will be disabled.");
                DisableAdminUI();
            }
        }
        else
        {
            AppManager.Instance.DisplayNotification("Admin Access Verified");
        }
    }

    private void DisableAdminUI()
    {
        if (_fetchAllTournaments != null) _fetchAllTournaments.interactable = false;
        if (_addNewTournament != null) _addNewTournament.interactable = false;
        if (_liveDashboardButton != null) _liveDashboardButton.interactable = false;
        if (_controlCenterButton != null) _controlCenterButton.interactable = false;
        if (_createTournamentOverlay != null) _createTournamentOverlay.gameObject.SetActive(false);
    }
}
