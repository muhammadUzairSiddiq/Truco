using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Replaces English baked login/register button art with Spanish runtime labels (same Button handlers).</summary>
public static class TrucoLoginScreenUiPolish
{
    const string CaptionChild = "TrucoCaption";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnSceneLoaded() => ApplyIfLoginScene();

    public static void ApplyIfLoginScene()
    {
        if (SceneManager.GetActiveScene().name != "LoginScreen") return;
        Apply();
    }

    public static void Apply()
    {
        TrucoLocalization.ApplyFromSettings();

        foreach (var btn in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (btn == null) continue;
            string n = btn.gameObject.name;
            if (n == "Login") RestyleAuthButton(btn, "INICIAR SESIÓN");
            else if (n == "Register") RestyleAuthButton(btn, "REGISTRARSE");
        }

        foreach (var tmp in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tmp == null) continue;
            if (tmp.GetComponentInParent<Button>(true) != null) continue;
            switch (tmp.text?.Trim())
            {
                case "Login":
                case "Player Login":
                    tmp.text = "Iniciar sesión";
                    break;
                case "Register":
                    tmp.text = "Registrarse";
                    break;
                case "Admin Login":
                    tmp.text = "Admin";
                    break;
                case "Email":
                case "E-mail":
                    tmp.text = "Correo electrónico";
                    break;
                case "Password":
                    tmp.text = "Contraseña";
                    break;
                case "Username":
                case "User Name":
                    tmp.text = "Nombre de usuario";
                    break;
                case "OTP":
                case "Enter OTP":
                    tmp.text = "Código OTP";
                    break;
                case "Verify":
                case "Verify OTP":
                    tmp.text = "Verificar";
                    break;
                case "Resend":
                case "Resend OTP":
                    tmp.text = "Reenviar código";
                    break;
                case "Forgot Password?":
                case "Forgot password?":
                    tmp.text = "¿Olvidaste tu contraseña?";
                    break;
            }
        }

        foreach (var field in Object.FindObjectsByType<TMP_InputField>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (field == null || field.placeholder is not TMP_Text ph) continue;
            string n = field.gameObject.name.ToLowerInvariant();
            if (n.Contains("email") || n.Contains("correo"))
                ph.text = "Correo electrónico";
            else if (n.Contains("pass"))
                ph.text = "Contraseña";
            else if (n.Contains("user") || n.Contains("name"))
                ph.text = "Nombre de usuario";
            else if (n.Contains("otp"))
                ph.text = "Código OTP";
        }
    }

    static void RestyleAuthButton(Button btn, string label)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null)
        {
            var green = TrucoUiAssetLoader.GreenButton;
            if (green != null)
            {
                img.sprite = green;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.sprite = null;
                img.color = TrucoUiTheme.CreateFormButtonPrimary;
            }
        }

        foreach (Transform ch in btn.transform)
        {
            var cImg = ch.GetComponent<Image>();
            if (cImg != null && cImg != img) cImg.enabled = false;
            var oldTmp = ch.GetComponent<TextMeshProUGUI>();
            if (oldTmp != null && ch.name != CaptionChild) oldTmp.enabled = false;
        }

        var capGo = btn.transform.Find(CaptionChild);
        if (capGo == null)
        {
            capGo = new GameObject(CaptionChild, typeof(RectTransform)).transform;
            capGo.SetParent(btn.transform, false);
            var rt = capGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(12f, 8f);
            rt.offsetMax = new Vector2(-12f, -8f);
        }

        var cap = capGo.GetComponent<TextMeshProUGUI>();
        if (cap == null) cap = capGo.gameObject.AddComponent<TextMeshProUGUI>();
        cap.text = label;
        cap.fontSize = 36f;
        cap.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        cap.alignment = TextAlignmentOptions.Center;
        cap.color = Color.white;
        cap.enableAutoSizing = true;
        cap.fontSizeMin = 18f;
        cap.fontSizeMax = 36f;
        cap.overflowMode = TextOverflowModes.Ellipsis;
        cap.raycastTarget = false;
        var f = TMP_Settings.defaultFontAsset;
        if (f != null) cap.font = f;
    }
}
