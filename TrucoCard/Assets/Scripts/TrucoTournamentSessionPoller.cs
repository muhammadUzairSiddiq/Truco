using MH.Multiplayer;
using UnityEngine;

/// <summary>Reads server snapshots while waiting/spectating; cannot create local results or credits.</summary>
public sealed class TrucoTournamentSessionPoller : MonoBehaviour
{
    static TrucoTournamentSessionPoller _instance;
    public static void Ensure()
    {
        if (_instance != null) return;
        var go = new GameObject("TournamentServerState"); DontDestroyOnLoad(go);
        _instance = go.AddComponent<TrucoTournamentSessionPoller>();
        _instance.InvokeRepeating(nameof(Poll), 2, 3);
    }
    void Poll() { if (MultiplayerController.IsTournamentActive) _ = MultiplayerController.RefreshAuthoritativeStateAsync(); }
    void OnDestroy() { if (_instance == this) _instance = null; }
}

/// <summary>Stores only an account-bound tournament ID for authenticated resume, never a local outcome.</summary>
public static class TrucoTournamentSessionStore
{
    static string _suppressedAccount;
    public static bool SuppressAutomaticResume
    {
        get => !string.IsNullOrEmpty(_suppressedAccount) && _suppressedAccount == ApiController.GetSessionUser?.Data?._id;
        set => _suppressedAccount = value ? ApiController.GetSessionUser?.Data?._id : null;
    }
    static string Key => "truco_tournament_resume_v1_" + (ApiController.GetSessionUser?.Data?._id ?? "signed-out");
    public static string Remembered() => string.IsNullOrEmpty(ApiController.GetSessionUser?.Data?._id) ? null : PlayerPrefs.GetString(Key, "");
    public static void Remember(string id) { PlayerPrefs.SetString(Key, id); PlayerPrefs.Save(); }
    public static void Forget() { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
}
