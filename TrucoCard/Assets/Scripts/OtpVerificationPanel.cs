using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class OtpVerificationPanel : MonoBehaviour
{

    [SerializeField] private TMP_InputField _otpInputField;
    [SerializeField] private Button _resendBtn;
    [SerializeField] private Button _submitBtn;

    private float _initTime = 60;
    private bool _canResend = true;
    private LoginRequest _requestCredientials;

    void OnEnable() => ApplySpanishLabels();

    void ApplySpanishLabels()
    {
        TrucoLocalization.EnsureSpanishDefaultUnlessUserPicked();
        if (_otpInputField != null && (_otpInputField.placeholder is TMP_Text ph))
            ph.text = "Código OTP";
    }

    public void SetRequestData(LoginRequest requestData)
    {
        _requestCredientials = requestData;
        ApplySpanishLabels();

        _resendBtn.onClick.RemoveAllListeners();
        _submitBtn.onClick.RemoveAllListeners();

        _resendBtn.onClick.AddListener(() =>
        {
            SendOTP(_requestCredientials.email);
        });

        _submitBtn.onClick.AddListener(VerifyOTP);
    }


    public async void SendOTP(string targetMail)
    {
        AppManager.Instance.DisplayLoadingUI("Enviando código…");

        _resendBtn.interactable = false;
        _submitBtn.interactable = false;

        bool OTPSent = await ApiController.SendVerificationOTP(targetMail, () =>
        {
            AppManager.Instance.DisplayNotification("Revisá tu correo para el código OTP");

            _initTime = Time.time;
            _canResend = false;
            _submitBtn.interactable = true;
        },
        (ERR) => 
        {
            AppManager.Instance.DisplayNotification(ERR);

            _canResend = true;
            _submitBtn.interactable = true;
            _resendBtn.interactable = true;
        });
    }

    public async void VerifyOTP()
    {
        string email = _requestCredientials.email;
        string otp = _otpInputField.text.Trim();

        AppManager.Instance.DisplayLoadingUI("Verificando código…");

        bool otpVerified = await ApiController.VerifyOTP(email, otp, () =>
        {
            AppManager.Instance.DisplayNotification("¡Correo verificado!", () =>
            {
                AppManager.Instance.DisplayLoadingUI("Cargando perfil…");

                ApiController.Init(
                    new LoginRequest { email = _requestCredientials.email, password = _requestCredientials.password },
                    () =>
                    {
                        if (_requestCredientials != null)
                            LoginCredentialsStore.Save(_requestCredientials.email, _requestCredientials.password);
                        PostLoginSceneRouter.LoadSceneForCredentials(_requestCredientials.email, _requestCredientials.password);
                    },
                    (ERR) =>
                    {
                        AppManager.Instance.DisplayNotification("No se pudo cargar el perfil: " + ERR);
                    });

            });

            gameObject.SetActive(false);
        },
        (ERR) =>
        {
            AppManager.Instance.DisplayNotification(ERR);
        });

    }

    private void Update()
    {
        if (_canResend) return;

        if(Time.time - _initTime > 60)
        {
            _canResend = true;
            _resendBtn.interactable = true;
        }
    }
}
