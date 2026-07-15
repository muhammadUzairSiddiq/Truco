using Photon.Pun;
using UnityEngine;

/// <summary>Runtime accessor for <see cref="TrucoPhotonRegionSettingsSO"/> (Resources/TrucoPhotonRegionSettings).</summary>
public static class TrucoPhotonRegionSettings
{
    const string ResourceName = "TrucoPhotonRegionSettings";

    static TrucoPhotonRegionSettingsSO _so;
    static bool _loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoLoad()
    {
        EnsureLoaded();
        ApplyToPhoton();
    }

    public static TrucoPhotonRegionSettingsSO Instance
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
        _so = Resources.Load<TrucoPhotonRegionSettingsSO>(ResourceName);
        if (_so == null)
        {
            Debug.LogWarning("[TrucoPhotonRegion] Missing Resources/" + ResourceName +
                             ".asset — defaulting to sa.");
            _so = ScriptableObject.CreateInstance<TrucoPhotonRegionSettingsSO>();
            _so.region = TrucoPhotonRegionSettingsSO.CloudRegion.SouthAmerica;
        }
    }

    public static void ApplyAsset(TrucoPhotonRegionSettingsSO so)
    {
        _so = so;
        _loaded = so != null;
        ApplyToPhoton();
    }

    /// <summary>Resolved region code (PlayerPrefs override → SO → sa).</summary>
    public static string RegionCode
    {
        get
        {
            EnsureLoaded();
            string key = _so != null && !string.IsNullOrEmpty(_so.playerPrefsOverrideKey)
                ? _so.playerPrefsOverrideKey
                : "TrucoPhotonRegion";
            if (PlayerPrefs.HasKey(key))
            {
                string pref = PlayerPrefs.GetString(key, string.Empty);
                if (TrucoPhotonRegionSettingsSO.TryParseCode(pref, out var fromPref))
                    return TrucoPhotonRegionSettingsSO.CodeFor(fromPref);
            }
            // Player never picked a region — default South America (not SO override at runtime).
            return "sa";
        }
    }

    public static TrucoPhotonRegionSettingsSO.CloudRegion CurrentRegion
    {
        get
        {
            TrucoPhotonRegionSettingsSO.TryParseCode(RegionCode, out var r);
            return r;
        }
    }

    /// <summary>
    /// Writes FixedRegion into PhotonServerSettings before ConnectUsingSettings.
    /// Call this before every Photon connect.
    /// </summary>
    public static void ApplyToPhoton()
    {
        EnsureLoaded();
        string code = RegionCode;
        var settings = PhotonNetwork.PhotonServerSettings;
        if (settings == null || settings.AppSettings == null)
        {
            Debug.LogWarning("[TrucoPhotonRegion] PhotonServerSettings missing — cannot apply region=" + code);
            return;
        }
        if (settings.AppSettings.FixedRegion == code) return;
        settings.AppSettings.FixedRegion = code;
        Debug.Log("[TrucoPhotonRegion] FixedRegion → " + code);
    }

    /// <summary>Set SO region (Editor/tests). Does not persist PlayerPrefs.</summary>
    public static void SetRegion(TrucoPhotonRegionSettingsSO.CloudRegion region)
    {
        EnsureLoaded();
        if (_so != null) _so.region = region;
        ApplyToPhoton();
    }

    /// <summary>Runtime override without rebuilding (cleared with ClearPlayerPrefsOverride).</summary>
    public static void SetPlayerPrefsOverride(TrucoPhotonRegionSettingsSO.CloudRegion region)
    {
        EnsureLoaded();
        string key = _so != null && !string.IsNullOrEmpty(_so.playerPrefsOverrideKey)
            ? _so.playerPrefsOverrideKey
            : "TrucoPhotonRegion";
        PlayerPrefs.SetString(key, TrucoPhotonRegionSettingsSO.CodeFor(region));
        PlayerPrefs.Save();
        ApplyToPhoton();
    }

    public static void ClearPlayerPrefsOverride()
    {
        EnsureLoaded();
        string key = _so != null && !string.IsNullOrEmpty(_so.playerPrefsOverrideKey)
            ? _so.playerPrefsOverrideKey
            : "TrucoPhotonRegion";
        if (PlayerPrefs.HasKey(key))
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
        ApplyToPhoton();
    }
}
