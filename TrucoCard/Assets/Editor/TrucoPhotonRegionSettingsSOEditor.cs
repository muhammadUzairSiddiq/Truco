using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TrucoPhotonRegionSettingsSO))]
public class TrucoPhotonRegionSettingsSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var so = (TrucoPhotonRegionSettingsSO)target;
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "Host and guest MUST use the same region or JoinRoom fails.\n" +
            "Current code: " + so.RegionCode + "\n" +
            "Asset path: Resources/TrucoPhotonRegionSettings",
            MessageType.Warning);

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = so.region == TrucoPhotonRegionSettingsSO.CloudRegion.SouthAmerica
            ? new Color(0.45f, 0.85f, 0.55f) : Color.white;
        if (GUILayout.Button("Use SA (Paraguay)", GUILayout.Height(32)))
        {
            Undo.RecordObject(so, "Photon region SA");
            so.region = TrucoPhotonRegionSettingsSO.CloudRegion.SouthAmerica;
            EditorUtility.SetDirty(so);
            TrucoPhotonRegionSettings.ApplyAsset(so);
            SyncPhotonServerSettingsAsset(so.RegionCode);
        }
        GUI.backgroundColor = so.region == TrucoPhotonRegionSettingsSO.CloudRegion.Asia
            ? new Color(0.55f, 0.7f, 1f) : Color.white;
        if (GUILayout.Button("Use Asia (dev ping)", GUILayout.Height(32)))
        {
            Undo.RecordObject(so, "Photon region Asia");
            so.region = TrucoPhotonRegionSettingsSO.CloudRegion.Asia;
            EditorUtility.SetDirty(so);
            TrucoPhotonRegionSettings.ApplyAsset(so);
            SyncPhotonServerSettingsAsset(so.RegionCode);
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Apply to Photon Now (Play Mode)"))
        {
            TrucoPhotonRegionSettings.ApplyAsset(so);
            SyncPhotonServerSettingsAsset(so.RegionCode);
            Debug.Log("[TrucoPhotonRegion] Applied " + so.RegionCode);
        }

        if (GUILayout.Button("Clear PlayerPrefs Override"))
        {
            TrucoPhotonRegionSettings.ClearPlayerPrefsOverride();
            Debug.Log("[TrucoPhotonRegion] PlayerPrefs override cleared.");
        }
    }

    static void SyncPhotonServerSettingsAsset(string code)
    {
        var settings = Photon.Pun.PhotonNetwork.PhotonServerSettings;
        if (settings == null || settings.AppSettings == null) return;
        if (settings.AppSettings.FixedRegion == code) return;
        Undo.RecordObject(settings, "Sync Photon FixedRegion");
        settings.AppSettings.FixedRegion = code;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("TrucoCard/Photon Region/SA (Paraguay)")]
    static void MenuSa()
    {
        SetViaMenu(TrucoPhotonRegionSettingsSO.CloudRegion.SouthAmerica);
    }

    [MenuItem("TrucoCard/Photon Region/Asia (dev)")]
    static void MenuAsia()
    {
        SetViaMenu(TrucoPhotonRegionSettingsSO.CloudRegion.Asia);
    }

    static void SetViaMenu(TrucoPhotonRegionSettingsSO.CloudRegion region)
    {
        var so = Resources.Load<TrucoPhotonRegionSettingsSO>("TrucoPhotonRegionSettings");
        if (so == null)
        {
            EnsureAssetExists();
            so = Resources.Load<TrucoPhotonRegionSettingsSO>("TrucoPhotonRegionSettings");
        }
        if (so == null)
        {
            Debug.LogError("[TrucoPhotonRegion] Could not load/create Resources/TrucoPhotonRegionSettings");
            return;
        }
        Undo.RecordObject(so, "Photon region");
        so.region = region;
        EditorUtility.SetDirty(so);
        TrucoPhotonRegionSettings.ApplyAsset(so);
        SyncPhotonServerSettingsAsset(so.RegionCode);
        Debug.Log("[TrucoPhotonRegion] Menu → " + so.RegionCode);
        Selection.activeObject = so;
    }

    [MenuItem("TrucoCard/Photon Region/Select Settings Asset")]
    static void SelectAsset()
    {
        EnsureAssetExists();
        var so = Resources.Load<TrucoPhotonRegionSettingsSO>("TrucoPhotonRegionSettings");
        if (so != null) Selection.activeObject = so;
    }

    static void EnsureAssetExists()
    {
        const string path = "Assets/Resources/TrucoPhotonRegionSettings.asset";
        if (AssetDatabase.LoadAssetAtPath<TrucoPhotonRegionSettingsSO>(path) != null) return;
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        var asset = ScriptableObject.CreateInstance<TrucoPhotonRegionSettingsSO>();
        asset.region = TrucoPhotonRegionSettingsSO.CloudRegion.SouthAmerica;
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        Debug.Log("[TrucoPhotonRegion] Created " + path);
    }
}
