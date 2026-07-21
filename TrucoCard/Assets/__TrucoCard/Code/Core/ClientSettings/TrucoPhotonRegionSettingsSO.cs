using UnityEngine;

/// <summary>
/// Dev/test switch for Photon Cloud FixedRegion (rooms only match inside one region).
/// Edit <c>Resources/TrucoPhotonRegionSettings</c> — Asia for local latency, SA for Paraguay production.
/// Both host and guest must use the same region or JoinRoom fails.
/// </summary>
[CreateAssetMenu(fileName = "TrucoPhotonRegionSettings", menuName = "TrucoCard/Photon Region Settings", order = 2)]
public class TrucoPhotonRegionSettingsSO : ScriptableObject
{
    public enum CloudRegion
    {
        [Tooltip("sa — South America (Paraguay / LatAm production)")]
        SouthAmerica = 0,
        [Tooltip("asia — lower ping when testing from Asia; host+guest must both use asia")]
        Asia = 1,
        [Tooltip("eu — Europe; host+guest must both use eu")]
        Europe = 2
    }

    [Header("Photon Fixed Region")]
    [Tooltip("All devices in a match must share this. Switch anytime for Editor/APK testing.")]
    public CloudRegion region = CloudRegion.SouthAmerica;

    [Tooltip("Optional PlayerPrefs override key. If set at runtime, overrides this asset until cleared.")]
    public string playerPrefsOverrideKey = "TrucoPhotonRegion";

    public string RegionCode
    {
        get
        {
            switch (region)
            {
                case CloudRegion.Asia: return "asia";
                case CloudRegion.Europe: return "eu";
                default: return "sa";
            }
        }
    }

    public static string CodeFor(CloudRegion r)
    {
        switch (r)
        {
            case CloudRegion.Asia: return "asia";
            case CloudRegion.Europe: return "eu";
            default: return "sa";
        }
    }

    public static bool TryParseCode(string code, out CloudRegion region)
    {
        if (string.IsNullOrEmpty(code))
        {
            region = CloudRegion.SouthAmerica;
            return false;
        }
        code = code.Trim().ToLowerInvariant();
        if (code == "asia" || code == "as")
        {
            region = CloudRegion.Asia;
            return true;
        }
        if (code == "eu" || code == "europe")
        {
            region = CloudRegion.Europe;
            return true;
        }
        if (code == "sa" || code == "southamerica" || code == "south america")
        {
            region = CloudRegion.SouthAmerica;
            return true;
        }
        region = CloudRegion.SouthAmerica;
        return false;
    }
}
