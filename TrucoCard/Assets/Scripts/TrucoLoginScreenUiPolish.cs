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
        TrucoLocalization.EnsureSpanishDefaultUnlessUserPicked();

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
                case "Registration":
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
            if (field != null && field.GetComponent<Image>() != null)
                TrucoMenuTheme.Surface(field.GetComponent<Image>(), TrucoMenuTheme.Card);
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
        ApplyPlayerLayouts();
    }

    static void ApplyPlayerLayouts()
    {
        var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var root in canvas)
        {
            var login = root.transform.Find("Player Login Panel");
            if (login == null) continue;
            var logo = login.Find("LOGO")?.GetComponent<Image>()?.sprite;
            var logoTexture = login.Find("LOGO")?.GetComponent<RawImage>()?.texture;
            foreach (string name in new[] { "Player Login Panel", "Player Register Panel", "OtpVerificationPanel", "ForgotPasswordPanel" })
            {
                var panel = root.transform.Find(name); if (panel == null) continue;
                TrucoMenuTheme.Background(panel);
                var oldBg = panel.Find("BG"); if (oldBg != null) oldBg.gameObject.SetActive(false);
                var brand = panel.Find("AuthBrand");
                if (brand == null)
                {
                    brand = TrucoClubUI.Panel("AuthBrand", panel, TrucoMenuTheme.Brown, new Vector2(0, .77f), Vector2.one).transform;
                    TrucoMenuTheme.Surface(brand.GetComponent<Image>(), TrucoMenuTheme.Brown, false);
                    brand.SetAsFirstSibling();
                    var sheet = TrucoClubUI.Panel("AuthFormSheet", panel, TrucoMenuTheme.Card, new Vector2(.06f, .095f), new Vector2(.94f, .64f));
                    sheet.transform.SetAsFirstSibling();
                }
                var oldLogo = panel.Find("LOGO") ?? panel.Find("LOGO (1)");
                if (oldLogo == null)
                {
                    oldLogo = TrucoClubUI.Rect("LOGO", panel, new Vector2(.14f, .80f), new Vector2(.86f, .96f));
                    if (logoTexture != null) oldLogo.gameObject.AddComponent<RawImage>().texture = logoTexture;
                    else oldLogo.gameObject.AddComponent<Image>().sprite = logo;
                }
                Place(oldLogo, .14f, .80f, .86f, .96f);
                var logoImage = oldLogo.GetComponent<Image>(); if (logoImage != null) logoImage.preserveAspect = true;
                var title = panel.Find("Header")?.GetComponent<TextMeshProUGUI>();
                if (title != null)
                {
                    Place(title.transform, .07f, .66f, .93f, .75f);
                    title.text = name == "Player Login Panel" ? "INICIAR SESIÓN" : name == "Player Register Panel" ? "CREAR CUENTA" : name == "OtpVerificationPanel" ? "VERIFICÁ TU CORREO" : "RECUPERAR CONTRASEÑA";
                    title.color = TrucoMenuTheme.Ink; title.fontStyle = FontStyles.Bold;
                    title.fontSize = 43; title.enableAutoSizing = true; title.fontSizeMin = 32; title.fontSizeMax = 43;
                    title.alignment = TextAlignmentOptions.Center;
                }
                foreach (var field in panel.GetComponentsInChildren<TMP_InputField>(true))
                {
                    field.transform.localScale = Vector3.one;
                    if (field.textComponent != null) { field.textComponent.fontSize = 34; field.textComponent.color = TrucoMenuTheme.Ink; }
                    if (field.placeholder is TMP_Text hint) { hint.fontSize = 32; hint.color = TrucoMenuTheme.Secondary; hint.fontStyle = FontStyles.Normal; }
                    var colors = field.colors; colors.normalColor = colors.selectedColor = colors.highlightedColor = Color.white; field.colors = colors;
                    var outline = field.GetComponent<Outline>() ?? field.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(.47f, .37f, .25f, .3f); outline.effectDistance = new Vector2(1, -1);
                }
                if (name == "Player Login Panel")
                {
                    Place(panel.Find("Email Field"), .12f, .51f, .88f, .585f);
                    Place(panel.Find("Password Field"), .12f, .41f, .88f, .485f);
                    LayoutButton(panel, "Login", "INICIAR SESIÓN", .30f, .375f);
                    LayoutButton(panel, "Register", "CREAR UNA CUENTA", .20f, .275f, false);
                    LayoutButton(panel, "ForgotPasswordBtn", "¿OLVIDASTE TU CONTRASEÑA?", .12f, .18f, false);
                    var close = panel.Find("Close"); if (close != null) close.gameObject.SetActive(false);
                }
                else if (name == "Player Register Panel")
                {
                    Place(panel.Find("User Name Field"), .12f, .54f, .88f, .615f);
                    Place(panel.Find("Email Field"), .12f, .44f, .88f, .515f);
                    Place(panel.Find("Password Field"), .12f, .34f, .88f, .415f);
                    LayoutButton(panel, "Register", "REGISTRARME", .23f, .305f);
                    LayoutButton(panel, "Login", "YA TENGO CUENTA", .13f, .205f, false);
                    var close = panel.Find("Close (1)"); if (close != null) close.gameObject.SetActive(false);
                }
                else
                {
                    bool otp = name == "OtpVerificationPanel";
                    Place(panel.Find(otp ? "OTPField" : "ForgotLoginEmailField"), .12f, .45f, .88f, .53f);
                    LayoutButton(panel, otp ? "SubmitOTPBtn" : "SendForgotEmailBtn", otp ? "VERIFICAR CÓDIGO" : "ENVIAR ENLACE", .33f, .41f);
                    if (otp) LayoutButton(panel, "ResendOTP", "REENVIAR CÓDIGO", .23f, .30f, false);
                    LayoutButton(panel, "Close", "VOLVER", .13f, .20f, false);
                }
            }
        }
    }
    static void Place(Transform value, float x0, float y0, float x1, float y1)
    {
        if (value is not RectTransform rt) return;
        rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = rt.offsetMax = Vector2.zero; rt.localScale = Vector3.one;
    }
    static void LayoutButton(Transform panel, string name, string caption, float y0, float y1, bool primary = true)
    {
        var value = panel.Find(name); if (value == null) return;
        value.gameObject.SetActive(true);
        Place(value, .12f, y0, .88f, y1);
        var button = value.GetComponent<Button>(); if (button == null) return;
        RestyleAuthButton(button, caption); TrucoMenuTheme.StyleButton(button, primary);
        if (button.targetGraphic is Image image) TrucoMenuTheme.Surface(image, primary ? TrucoClubUI.Green : TrucoMenuTheme.Brown);
    }

    static void RestyleAuthButton(Button btn, string label)
    {
        if (btn == null) return;
        TrucoMenuTheme.StyleButton(btn);
        var rootLabel = btn.GetComponent<TextMeshProUGUI>(); if (rootLabel != null) rootLabel.text = "";
        var img = btn.GetComponent<Image>();
        if (img == null)
        {
            var surface = btn.transform.Find("AuthButtonSurface");
            if (surface == null) surface = TrucoClubUI.Rect("AuthButtonSurface", btn.transform, Vector2.zero, Vector2.one);
            surface.SetAsFirstSibling(); img = surface.GetComponent<Image>() ?? surface.gameObject.AddComponent<Image>();
            btn.targetGraphic = img;
        }
        if (img != null)
        {
            TrucoMenuTheme.Surface(img, TrucoClubUI.Green);
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
