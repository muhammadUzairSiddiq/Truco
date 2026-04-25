using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Threading.Tasks;
public class TournamentPasswordInputOverlay : MonoBehaviour
{
    #region Setters/Private Variables

    [SerializeField] private GameObject _contentHolder;

    [SerializeField] private TMP_InputField _passwordInputField;
    [SerializeField] private Button _submitBtn;
    [SerializeField] private Button _closeBtn;

    private string _targetTournamentId = string.Empty;
    private Action _onValidationAction = delegate { };

    #endregion

    private void Start()
    {
        _submitBtn.onClick.RemoveAllListeners();
        _submitBtn.onClick.AddListener(()=> { SubmitBtn_fn(); });

        _closeBtn.onClick.RemoveAllListeners();
        _closeBtn.onClick.AddListener(CloseBtn_fn);
    }

    public void InitValidation(string targetTournamentId, Action OnSuccessAction = null)
    {
        _contentHolder.SetActive(true);
        _passwordInputField.text = string.Empty;

        _onValidationAction = OnSuccessAction;
        _targetTournamentId = targetTournamentId;
    }

    private async Task SubmitBtn_fn()
    {
        string passwordInput = _passwordInputField.text.Trim();
        if (passwordInput.Length < 1)
        {
            AppManager.Instance.DisplayNotification("Please enter a valid password.");
            return;
        }

        AppManager.Instance.DisplayLoadingUI("Validating Password");

        bool validated = await ApiController.ValidatePrivateTournament(_targetTournamentId, passwordInput);

        if (validated)
        {
            _onValidationAction?.Invoke();
        }
        else
        {
            AppManager.Instance.DisplayNotification("Invalid password. Please try again.");
        }
    }

    private void CloseBtn_fn()
    {
        _contentHolder.SetActive(false);
    }
}
