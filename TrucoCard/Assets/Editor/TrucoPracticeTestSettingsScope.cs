using UnityEditor;

/// <summary>Keep test-only AI settings across domain reloads without leaving practice enabled.</summary>
public static class TrucoPracticeTestSettingsScope
{
    public static void Capture(string key)
    {
        var settings = TrucoTournamentPracticeSettings.Load();
        if (settings == null) return;
        SessionState.SetBool(key + ".saved", true);
        SessionState.SetBool(key + ".enabled", settings.enabledInEditor);
        SessionState.SetBool(key + ".auto", settings.autoPlayYourSeat);
        SessionState.SetFloat(key + ".delay", settings.aiActionDelay);
    }
    public static void Restore(string key)
    {
        if (!SessionState.GetBool(key + ".saved", false)) return;
        var settings = TrucoTournamentPracticeSettings.Load();
        if (settings != null)
        {
            settings.enabledInEditor = SessionState.GetBool(key + ".enabled", false);
            settings.autoPlayYourSeat = SessionState.GetBool(key + ".auto", false);
            settings.aiActionDelay = SessionState.GetFloat(key + ".delay", .45f);
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
        }
        SessionState.EraseBool(key + ".saved");
    }
}
