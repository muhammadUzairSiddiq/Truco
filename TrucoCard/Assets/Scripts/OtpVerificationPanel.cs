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

    public void SetRequestData(LoginRequest requestData)
    {
        _requestCredientials = requestData;

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
        AppManager.Instance.DisplayLoadingUI("Sending OTP");

        _resendBtn.interactable = false;
        _submitBtn.interactable = false;

        bool OTPSent = await ApiController.SendVerificationOTP(targetMail, () =>
        {
            AppManager.Instance.DisplayNotification("Please Check your inbox for OTP");

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

        AppManager.Instance.DisplayLoadingUI("Verifying OTP");

        bool otpVerified = await ApiController.VerifyOTP(email, otp, () =>
        {
            AppManager.Instance.DisplayNotification("OTP Verified Successfully!", () =>
            {
                AppManager.Instance.DisplayLoadingUI("Fetching Profile...");

                ApiController.Init(
                    new LoginRequest { email = _requestCredientials.email, password = _requestCredientials.password },
                    () =>
                    {
                        SceneManager.LoadScene("MainMenu");
                    },
                    (ERR) =>
                    {
                        AppManager.Instance.DisplayNotification("Failed to fetch profile: " + ERR);
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
