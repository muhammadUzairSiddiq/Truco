using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// After API login succeeds, routes to <see cref="AdminSceneName"/> for the built-in admin test account,
/// otherwise to the main menu.
/// </summary>
public static class PostLoginSceneRouter
{
    public const string AdminSceneName = "AdminScene";
    public const string MainMenuSceneName = "MainMenu";

    const string AdminEmail = "admin@truco.com";
    const string AdminPassword = "admin@123";

    public static bool IsAdminCredentials(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || password == null) return false;
        return string.Equals(email.Trim(), AdminEmail, StringComparison.OrdinalIgnoreCase)
            && string.Equals(password.Trim(), AdminPassword, StringComparison.Ordinal);
    }

    /// <summary>True if <paramref name="email"/> is the dev admin address (login already proved password on client).</summary>
    public static bool IsBuiltInAdminSessionEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        return string.Equals(email.Trim(), AdminEmail, StringComparison.OrdinalIgnoreCase);
    }

    public static void LoadSceneForCredentials(string email, string password)
    {
        if (AppManager.Instance != null)
            AppManager.Instance.HideLoadingUI();

        TrucoSceneTransition.Go(IsAdminCredentials(email, password) ? AdminSceneName : MainMenuSceneName);
    }
}
