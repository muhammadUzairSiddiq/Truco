using UnityEngine;

/// <summary>
/// Single source of truth for client language, debug logging, and 1v1 Photon retry timing.
/// Edit <c>Resources/TrucoClientSettings</c> — runtime code reads only through <see cref="TrucoClientSettings"/>.
/// </summary>
[CreateAssetMenu(fileName = "TrucoClientSettings", menuName = "TrucoCard/Client Settings", order = 1)]
public class TrucoClientSettingsSO : ScriptableObject
{
    public enum DefaultLanguageOption
    {
        Spanish = 0,
        English = 1
    }

    [Header("Language")]
    [Tooltip("When off, all UI uses English (if enabled) regardless of saved preference.")]
    public bool spanishEnabled = true;

    [Tooltip("When off, all UI uses Spanish (if enabled) regardless of saved preference.")]
    public bool englishEnabled = true;

    [Tooltip("Used when both languages are enabled and the player has no saved preference.")]
    public DefaultLanguageOption defaultLanguage = DefaultLanguageOption.Spanish;

    [Tooltip("Show region + ENG/SPN on the profile / avatar screen (language buttons only when both languages are enabled).")]
    public bool showLanguageToggleInMenu = true;

    [Header("Debug")]
    [Tooltip("When off, TrucoDebugLog and tagged 1v1/API traces are suppressed.")]
    public bool debugLogsEnabled = true;

    [Header("1v1 Lobby cleanup")]
    [Tooltip("On each room-list refresh, POST /leave on every active lobby match tied to your account (host or guest).")]
    public bool autoLeaveAllMyLobbyMatchesOnRoomListOpen = true;

    [Header("API anti-cheat (backend)")]
    [Tooltip("Shared secret for POST /matches/:id/result and /walkover (header x-game-secret). Get value from backend; leave empty only for local stubs.")]
    public string gameSecret = "";

    [Header("1v1 Photon join retry (after ENTRAR)")]
    [Tooltip("Total seconds to keep retrying JoinRoom while waiting for the host's Photon room.")]
    [Min(1f)]
    public float photonJoinRetryTotalSeconds = 8f;

    [Tooltip("Delay between each JoinRoom retry.")]
    [Min(0.25f)]
    public float photonJoinRetryIntervalSeconds = 1f;

    public int ComputePhotonJoinMaxAttempts() =>
        Mathf.Max(1, Mathf.FloorToInt(photonJoinRetryTotalSeconds / Mathf.Max(0.25f, photonJoinRetryIntervalSeconds)));

    void OnValidate()
    {
        photonJoinRetryTotalSeconds = Mathf.Max(1f, photonJoinRetryTotalSeconds);
        photonJoinRetryIntervalSeconds = Mathf.Max(0.25f, photonJoinRetryIntervalSeconds);
        if (!spanishEnabled && !englishEnabled)
            Debug.LogWarning("[TrucoClientSettings] Both languages disabled — runtime will fall back to Spanish.");
    }
}
