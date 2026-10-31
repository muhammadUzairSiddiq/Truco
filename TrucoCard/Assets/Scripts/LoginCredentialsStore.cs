using UnityEngine;

/// <summary>Remember the login email only. Passwords are never persisted in PlayerPrefs.</summary>
public static class LoginCredentialsStore
{
    const string KeyEmail = "truco_auth_saved_email";
    const string KeyPassword = "truco_auth_saved_password";

    public static void Save(string email, string password)
    {
        if (string.IsNullOrEmpty(email)) return;
        PlayerPrefs.SetString(KeyEmail, email.Trim());
        PlayerPrefs.DeleteKey(KeyPassword);
        PlayerPrefs.Save();
    }

    public static void Load(out string email, out string password)
    {
        email = PlayerPrefs.GetString(KeyEmail, string.Empty);
        password = string.Empty;
        if (PlayerPrefs.HasKey(KeyPassword)) { PlayerPrefs.DeleteKey(KeyPassword); PlayerPrefs.Save(); }
    }

    public static bool HasEmailSaved() => !string.IsNullOrEmpty(PlayerPrefs.GetString(KeyEmail, string.Empty));

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(KeyEmail);
        PlayerPrefs.DeleteKey(KeyPassword);
        PlayerPrefs.Save();
    }
}
