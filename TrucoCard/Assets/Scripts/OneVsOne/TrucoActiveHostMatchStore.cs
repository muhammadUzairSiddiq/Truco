using UnityEngine;

/// <summary>Persists the last 1v1 room you hosted so the lobby list still treats you as host after Play Mode / app restart.</summary>
public static class TrucoActiveHostMatchStore
{
    const string Key = "truco_active_host_match";
    const string FeeKey = "truco_active_host_entry_fee";

    public static void Remember(string matchId, int entryFee = 0)
    {
        if (string.IsNullOrEmpty(matchId)) return;
        string previous = PlayerPrefs.GetString(Key, "");
        PlayerPrefs.SetString(Key, matchId);
        if (entryFee > 0)
            PlayerPrefs.SetInt(FeeKey, entryFee);
        else if (previous != matchId)
            PlayerPrefs.SetInt(FeeKey, 0);
        PlayerPrefs.Save();
    }

    public static bool IsRememberedHost(string matchId) =>
        !string.IsNullOrEmpty(matchId) && PlayerPrefs.GetString(Key, "") == matchId;

    public static string GetRememberedMatchId() => PlayerPrefs.GetString(Key, "");

    public static int GetRememberedEntryFee() => PlayerPrefs.GetInt(FeeKey, 0);

    public static void Clear()
    {
        if (PlayerPrefs.HasKey(Key))
            PlayerPrefs.DeleteKey(Key);
        if (PlayerPrefs.HasKey(FeeKey))
            PlayerPrefs.DeleteKey(FeeKey);
        PlayerPrefs.Save();
    }
}
