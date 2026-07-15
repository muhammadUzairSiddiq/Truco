using PlayFab;
using PlayFab.ClientModels;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
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
        TrucoLocalization.EnsureSpanishDefaultUnlessUserPicked();
        TrucoLoginScreenUiPolish.Apply();
        ApplySavedCredentialsToLoginFields();
    }

    void ApplySavedCredentialsToLoginFields()
    {
        LoginCredentialsStore.Load(out var email, out var password);
        if (loginEmailField != null && !string.IsNullOrEmpty(email))
            loginEmailField.text = email;
        if (loginPasswordField != null)
            loginPasswordField.text = password;
    }

    public void Login()
    {
        if (!ValidEmailPassword(loginEmailField.text, loginPasswordField.text))
        {
            AppManager.Instance.DisplayNotification("Correo o contraseña inválidos.");

            return;
        }

        LoginRequest loginRequestData = new LoginRequest
        {
            email = loginEmailField.text.Trim(),
            password = loginPasswordField.text.Trim()
        };

        AppManager.Instance.DisplayLoadingUI("Iniciando sesión…");
     
        ApiController.Init(loginRequestData, () =>
        {
            if (ApiController.GetSessionUser.Data.emailVerified)
            {
                OnLoginSuccess();
            }
            else
            {
                AppManager.Instance.DisplayNotification("Verificá tu correo. Revisá tu bandeja de entrada.");

                otpVerificationPanel.gameObject.SetActive(true);
                otpVerificationPanel.SetRequestData(loginRequestData);
            }
        },
        (ERR) =>
        {
           AppManager.Instance.DisplayNotification("Error al iniciar sesión: " + ERR);
        });
    }

    public async void ForgotPassword()
    {
        if (string.IsNullOrEmpty(forgotLoginEmailField.text) || !forgotLoginEmailField.text.Contains("@") || !forgotLoginEmailField.text.Contains(".com"))
        {
            AppManager.Instance.DisplayNotification("Correo inválido.");
            return;
        }
        var request = new SendAccountRecoveryEmailRequest
        {
            Email = forgotLoginEmailField.text.Trim(),

        };

        AppManager.Instance.DisplayLoadingUI("Enviando correo de recuperación…");

        sendForgotEmailBtn.interactable = false;

        bool requestSent = await ApiController.ForgotPassword(forgotLoginEmailField.text.Trim(),
            () =>
            {
                sendForgotEmailBtn.interactable = true;
                forgotLoginEmailField.text = string.Empty;
                forgotPasswordPanel.SetActive(false);

                AppManager.Instance.DisplayNotification("Correo de recuperación enviado. Revisá tu bandeja.");

            },
            (ERR) =>
            {
                sendForgotEmailBtn.interactable = true;
                AppManager.Instance.DisplayNotification("No se pudo enviar el correo: " + ERR);
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
            AppManager.Instance.DisplayNotification("Correo o contraseña inválidos.");
            return;
        }
        if (registerUsernameField.text.Trim().Length < 5)
        {
            AppManager.Instance.DisplayNotification("El nombre de usuario debe tener al menos 5 caracteres.");

            return;
        }

        RegisterRequest registerRequest = new RegisterRequest
        {
            email = registerEmailField.text.Trim(),
            name = registerUsernameField.text.Trim(),
            password = registerPasswordField.text.Trim(),
        };


        AppManager.Instance.DisplayLoadingUI("Validando datos…");


        await ApiController.RegisterAsync(registerRequest,
            () =>
            {
                LoginCredentialsStore.Save(registerRequest.email, registerRequest.password);
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
        AppManager.Instance.DisplayNotification("Registro exitoso. Verificá tu correo electrónico.");



        //SetDisplayName(registerUsernameField.text);
        //PlayfabDataManager.Instance.SetInitialData();
        registerEmailField.text = string.Empty;
        registerPasswordField.text = string.Empty;
        registerUsernameField.text = string.Empty;
        registerPanel.SetActive(false);
        ApplySavedCredentialsToLoginFields();
        //loginPanel.SetActive(true);
    }
    
    void OnLoginSuccess()
    {
        if (loginEmailField != null && loginPasswordField != null)
            LoginCredentialsStore.Save(loginEmailField.text.Trim(), loginPasswordField.text);
        PostLoginSceneRouter.LoadSceneForCredentials(loginEmailField.text, loginPasswordField.text);
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