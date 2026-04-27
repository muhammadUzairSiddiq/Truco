using UnityEngine;

/// <summary>Last successful login email/password in <see cref="PlayerPrefs"/> for field autofill. Stored as plain text on device — same tradeoff as browser "remember me".</summary>
public static class LoginCredentialsStore
{
    const string KeyEmail = "truco_auth_saved_email";
    const string KeyPassword = "truco_auth_saved_password";

    public static void Save(string email, string password)
    {
        if (string.IsNullOrEmpty(email)) return;
        PlayerPrefs.SetString(KeyEmail, email.Trim());
        PlayerPrefs.SetString(KeyPassword, password ?? string.Empty);
        PlayerPrefs.Save();
    }

    public static void Load(out string email, out string password)
    {
        email = PlayerPrefs.GetString(KeyEmail, string.Empty);
        password = PlayerPrefs.GetString(KeyPassword, string.Empty);
    }

    public static bool HasEmailSaved() => !string.IsNullOrEmpty(PlayerPrefs.GetString(KeyEmail, string.Empty));

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(KeyEmail);
        PlayerPrefs.DeleteKey(KeyPassword);
        PlayerPrefs.Save();
    }
}
