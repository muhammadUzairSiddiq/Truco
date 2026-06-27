using UnityEngine;

/// <summary>Persists the last 1v1 room you hosted so the lobby list still treats you as host after Play Mode / app restart.</summary>
public static class TrucoActiveHostMatchStore
{
    const string Key = "truco_active_host_match";

    public static void Remember(string matchId)
    {
        if (string.IsNullOrEmpty(matchId)) return;
        PlayerPrefs.SetString(Key, matchId);
        PlayerPrefs.Save();
    }

    public static bool IsRememberedHost(string matchId) =>
        !string.IsNullOrEmpty(matchId) && PlayerPrefs.GetString(Key, "") == matchId;

    public static string GetRememberedMatchId() => PlayerPrefs.GetString(Key, "");

    public static void Clear()
    {
        if (!PlayerPrefs.HasKey(Key)) return;
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }
}
