using PlayFab;
using PlayFab.ClientModels;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayFabLogin : MonoBehaviour
{
    [SerializeField] private TMP_InputField loginEmailField;
    [SerializeField] private TMP_InputField loginPasswordField;
    [SerializeField] private TMP_InputField forgotLoginEmailField;
    [SerializeField] private TMP_InputField registerEmailField;
    [SerializeField] private TMP_InputField registerPasswordField;
    [SerializeField] private TMP_InputField registerUsernameField;
    [SerializeField] private Button sendForgotEmailBtn;
    [SerializeField] private GameObject registerPanel;
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject forgotPasswordPanel;
    [SerializeField] private OtpVerificationPanel otpVerificationPanel;

    void Start()
    {
        //LoginWithCustomID();

        AppManager.Instance.HideLoadingUI();
    }

    public void Login()
    {
        if (!ValidEmailPassword(loginEmailField.text, loginPasswordField.text))
        {
            AppManager.Instance.DisplayNotification("Invalid email or password.");

            return;
        }

        LoginRequest loginRequestData = new LoginRequest
        {
            email = loginEmailField.text.Trim(),
            password = loginPasswordField.text.Trim()
        };

        AppManager.Instance.DisplayLoadingUI("Logging in...");
     
        ApiController.Init(loginRequestData, () =>
        {
            if (ApiController.GetSessionUser.Data.emailVerified)
            {
                OnLoginSuccess();
            }
            else
            {
                AppManager.Instance.DisplayNotification("Please verify your email address.\nCheck your inbox for a verification link.!");

                otpVerificationPanel.gameObject.SetActive(true);
                otpVerificationPanel.SetRequestData(loginRequestData);
            }
        },
        (ERR) =>
        {
           AppManager.Instance.DisplayNotification("Login failed: " + ERR);
        });
    }

    public async void ForgotPassword()
    {
        if (string.IsNullOrEmpty(forgotLoginEmailField.text) || !forgotLoginEmailField.text.Contains("@") || !forgotLoginEmailField.text.Contains(".com"))
        {
            AppManager.Instance.DisplayNotification("Invalid email address.");
            return;
        }
        var request = new SendAccountRecoveryEmailRequest
        {
            Email = forgotLoginEmailField.text.Trim(),

        };

        AppManager.Instance.DisplayLoadingUI("Sending recovery email...");

        sendForgotEmailBtn.interactable = false;

        bool requestSent = await ApiController.ForgotPassword(forgotLoginEmailField.text.Trim(),
            () =>
            {
                sendForgotEmailBtn.interactable = true;
                forgotLoginEmailField.text = string.Empty;
                forgotPasswordPanel.SetActive(false);

                AppManager.Instance.DisplayNotification("Recovery email sent, please check your inbox.");

            },
            (ERR) =>
            {
                sendForgotEmailBtn.interactable = true;
                AppManager.Instance.DisplayNotification("Failed to send recovery email: " + ERR);
            });
    }

    private bool ValidEmailPassword(string email, string password)
    {
        bool result = true;
        
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) 
                                        || !email.Contains("@")
                                        || !email.Contains(".com"))
        {
            Debug.LogError("Email and password fields cannot be empty.");
            result = false;
        }

        return result;
    }

    public async void Register()
    {
        if (!ValidEmailPassword(registerEmailField.text, registerPasswordField.text))
        {
            AppManager.Instance.DisplayNotification("Invalid email or password.");
            return;
        }
        if (registerUsernameField.text.Trim().Length < 5)
        {
            AppManager.Instance.DisplayNotification("Invalid username - length must be equal or greater than 5!");

            return;
        }

        RegisterRequest registerRequest = new RegisterRequest
        {
            email = registerEmailField.text.Trim(),
            name = registerUsernameField.text.Trim(),
            password = registerPasswordField.text.Trim(),
        };


        AppManager.Instance.DisplayLoadingUI("Validating Credientials...");


        await ApiController.RegisterAsync(registerRequest,
            () =>
            {
                OnRegisterSuccess();

                otpVerificationPanel.gameObject.SetActive(true);
                otpVerificationPanel.SetRequestData(new LoginRequest { email= registerRequest.email, password = registerRequest.password});
            },
            (ERR) =>
            {
                AppManager.Instance.DisplayNotification(ERR);
            });
    }
    
    void OnRegisterSuccess()
    {
        AppManager.Instance.DisplayNotification("Registered Successfully, Please verify your email address.\nCheck your inbox for a verification link.!");



        //SetDisplayName(registerUsernameField.text);
        //PlayfabDataManager.Instance.SetInitialData();
        registerEmailField.text = string.Empty;
        registerPasswordField.text = string.Empty;
        registerUsernameField.text = string.Empty;
        loginEmailField.text = string.Empty;
        loginPasswordField.text = string.Empty;
        registerPanel.SetActive(false);
        //loginPanel.SetActive(true);
    }
    
    void OnLoginSuccess()
    {
        //bool isAdmin = await ApiController.CheckUserAdmin();

        //if (isAdmin)
        //{

        //    ApiController.GetSessionUser.Data.role = "admin";
        //    SceneManager.LoadScene("AdminScene");
        //}
        //else
        //{
        //    ApiController.GetSessionUser.Data.role = "player";
        //    SceneManager.LoadScene("MainMenu");
        //}

        SceneManager.LoadScene("MainMenu");
    }

    public void GetUserData()
    {
        //PlayfabDataManager.Instance.CheckIfAdmin(DataHandler.Instance.IngameData.playFabID, isAdmin =>
        //{
        //    if (isAdmin)
        //    {
        //        Debug.Log("User is admin");
        //        // Show admin dashboard
        //        SceneManager.LoadScene("AdminScene");
        //    }
        //    else
        //    {
        //        Debug.Log("User is not admin");
        //        // Show player dashboard
        //    }
        //});
    }
    
    void OnError(PlayFabError error)
    {
        Debug.LogError(error.GenerateErrorReport());
    }
    
    public void SetDisplayName(string displayName)
    {
        var request = new UpdateUserTitleDisplayNameRequest
        {
            DisplayName = displayName
        };

        PlayFabClientAPI.UpdateUserTitleDisplayName(request, result =>
            {
                Debug.Log("Display name updated to: " + result.DisplayName);
            },
            error =>
            {
                Debug.LogError("Failed to update display name: " + error.GenerateErrorReport());
            });
    }
    
}