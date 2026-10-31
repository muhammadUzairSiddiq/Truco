using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// After API login succeeds, chooses the admin screen only after a server role check.
/// </summary>
public static class PostLoginSceneRouter
{
    public const string AdminSceneName = "AdminScene";
    public const string MainMenuSceneName = "MainMenu";

    static bool _routing;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRouting() => _routing = false;

    public static void LoadSceneForCredentials(string email, string password)
    {
        // Signature retained for the existing login/verification listeners. Credentials
        // are not used as role evidence or retained across the asynchronous check.
        _ = RouteFromServerRoleAsync();
    }
    static async System.Threading.Tasks.Task RouteFromServerRoleAsync()
    {
        if (_routing) return;
        _routing = true;
        try
        {
            int generation = HttpApiClient.SessionGeneration;
            string userId = ApiController.GetSessionUser?.Data?._id;
            AppManager.Instance?.DisplayLoadingUI("Verificando permisos…");
            bool isAdmin = await ApiController.CheckUserAdmin();
            if (generation != HttpApiClient.SessionGeneration || string.IsNullOrEmpty(userId)
                || userId != ApiController.GetSessionUser?.Data?._id) return;
            AppManager.Instance?.HideLoadingUI();
            TrucoSceneTransition.Go(isAdmin ? AdminSceneName : MainMenuSceneName);
        }
        finally { _routing = false; }
    }
}
