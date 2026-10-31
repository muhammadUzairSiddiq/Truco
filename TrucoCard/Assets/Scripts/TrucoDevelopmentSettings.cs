#if UNITY_EDITOR
using UnityEngine;

public enum TrucoExecutionMode { Multiplayer, DevelopmentAI }

[CreateAssetMenu(menuName = "Truco/Development Settings", fileName = "TrucoDevelopmentSettings")]
public sealed class TrucoDevelopmentSettings : ScriptableObject
{
    public TrucoExecutionMode mode = TrucoExecutionMode.Multiplayer;
    [Min(1)] public int seed = 1731;
    [Range(.2f, 3f)] public float actionInterval = .65f;
    [Tooltip("Only rooms with this prefix may be driven by a development bot.")]
    public string roomPrefix = "TRUCO_AI_QA_";
    public string testRoom = "local-verification";
    [Tooltip("Separate QA Photon AppId. Anonymous synthetic peers are never allowed on the production Photon app.")]
    public string stagingPhotonAppId;
    public bool withFlor = true;
    public int targetScore = 15;
    static TrucoDevelopmentSettings _settings;
    public static TrucoDevelopmentSettings Current => _settings != null ? _settings : _settings = UnityEditor.AssetDatabase.LoadAssetAtPath<TrucoDevelopmentSettings>("Assets/Editor/TrucoDevelopmentSettings.asset");
    public static bool AIEnabled
    {
        get
        {
#if UNITY_EDITOR
            return Current != null && Current.mode == TrucoExecutionMode.DevelopmentAI;
#else
            return false;
#endif
        }
    }
    public static bool CanDriveRoom(string roomName)
    {
        return AIEnabled && !string.IsNullOrWhiteSpace(Current.roomPrefix) && Current.roomPrefix.StartsWith("TRUCO_AI_QA_")
            && !string.IsNullOrEmpty(roomName) && roomName.StartsWith(Current.roomPrefix, System.StringComparison.Ordinal);
    }
    public static bool IsSeparateStagingApp(string productionAppId, string stagingAppId) =>
        System.Guid.TryParse(productionAppId, out var production) && System.Guid.TryParse(stagingAppId, out var staging)
        && production != System.Guid.Empty && staging != System.Guid.Empty && production != staging;

    public static Photon.Realtime.AppSettings CreateStagingPhotonSettings()
    {
        var source = Photon.Pun.PhotonNetwork.PhotonServerSettings?.AppSettings;
        if (source == null || Current == null || !IsSeparateStagingApp(source.AppIdRealtime, Current.stagingPhotonAppId)) return null;
        var settings = source.CopyTo(new Photon.Realtime.AppSettings());
        settings.AppIdRealtime = Current.stagingPhotonAppId;
        settings.AppVersion = "truco-ai-development-v1";
        return settings;
    }

    public static bool IsIsolatedNetworkRoom(string roomName)
    {
        var settings = CreateStagingPhotonSettings();
        return settings != null && CanDriveRoom(roomName)
            && Photon.Pun.PhotonNetwork.NetworkingClient.AppId == settings.AppIdRealtime;
    }
}
#endif
