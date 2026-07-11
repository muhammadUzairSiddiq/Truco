using UnityEngine;

/// <summary>Runtime accessor for <see cref="TrucoClientSettingsSO"/> (Resources/TrucoClientSettings).</summary>
public static class TrucoClientSettings
{
    const string ResourceName = "TrucoClientSettings";

    static TrucoClientSettingsSO _so;
    static bool _loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoLoad() => EnsureLoaded();

    public static TrucoClientSettingsSO Instance
    {
        get
        {
            EnsureLoaded();
            return _so;
        }
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _so = Resources.Load<TrucoClientSettingsSO>(ResourceName);
        if (_so == null)
        {
            Debug.LogWarning("[TrucoClientSettings] Missing Resources/" + ResourceName +
                             ".asset — using in-memory defaults.");
            _so = ScriptableObject.CreateInstance<TrucoClientSettingsSO>();
        }
        if (_so.debugLogsEnabled)
            Debug.Log("[TrucoClientSettings] Loaded. ES=" + _so.spanishEnabled + " EN=" + _so.englishEnabled +
                      " debug=" + _so.debugLogsEnabled + " photonRetry=" + PhotonJoinMaxAttempts + "x" +
                      PhotonJoinRetryIntervalSeconds + "s gameSecretConfigured=" +
                      !string.IsNullOrEmpty(GameSecret));
    }

    public static void ApplyAsset(TrucoClientSettingsSO so)
    {
        _so = so;
        _loaded = so != null;
        if (_so != null && _so.debugLogsEnabled)
            Debug.Log("[TrucoClientSettings] Applied from inspector/test.");
    }

    public static bool SpanishEnabled => Instance != null && Instance.spanishEnabled;
    public static bool EnglishEnabled => Instance != null && Instance.englishEnabled;
    public static bool DebugLogsEnabled => Instance == null || Instance.debugLogsEnabled;
    public static bool ShowLanguageToggleInMenu =>
        Instance != null && Instance.showLanguageToggleInMenu && SpanishEnabled && EnglishEnabled;

    public static bool AutoLeaveAllMyLobbyMatchesOnRoomListOpen =>
        Instance == null || Instance.autoLeaveAllMyLobbyMatchesOnRoomListOpen;

    /// <summary>Shared secret for result/walkover (x-game-secret). Empty until backend provides it.</summary>
    public static string GameSecret =>
        Instance != null ? (Instance.gameSecret ?? string.Empty) : string.Empty;

    public static float PhotonJoinRetryTotalSeconds =>
        Instance != null ? Instance.photonJoinRetryTotalSeconds : 8f;

    public static float PhotonJoinRetryIntervalSeconds =>
        Instance != null ? Instance.photonJoinRetryIntervalSeconds : 1f;

    public static int PhotonJoinMaxAttempts =>
        Instance != null ? Instance.ComputePhotonJoinMaxAttempts() : 8;

    public static bool IsLanguageAllowed(TrucoLocalization.Lang lang)
    {
        if (lang == TrucoLocalization.Lang.English) return EnglishEnabled;
        return SpanishEnabled;
    }

    public static TrucoLocalization.Lang ResolveLanguage(TrucoLocalization.Lang preferred)
    {
        bool es = SpanishEnabled;
        bool en = EnglishEnabled;
        if (es && !en) return TrucoLocalization.Lang.Spanish;
        if (en && !es) return TrucoLocalization.Lang.English;
        if (!es && !en) return TrucoLocalization.Lang.Spanish;
        if (IsLanguageAllowed(preferred)) return preferred;
        var def = Instance != null
            ? (TrucoLocalization.Lang)Instance.defaultLanguage
            : TrucoLocalization.Lang.Spanish;
        return IsLanguageAllowed(def) ? def : TrucoLocalization.Lang.Spanish;
    }
}
