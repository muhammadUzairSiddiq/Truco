using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TrucoClientSettingsSO))]
public class TrucoClientSettingsSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var so = (TrucoClientSettingsSO)target;
        DrawDefaultInspector();
        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "Runtime reads this asset from Resources/TrucoClientSettings.\n" +
            "Photon join retries: " + so.ComputePhotonJoinMaxAttempts() + " attempts × " +
            so.photonJoinRetryIntervalSeconds + "s ≈ " + so.photonJoinRetryTotalSeconds + "s total.",
            MessageType.Info);
        if (GUILayout.Button("Apply Now (Play Mode)"))
        {
            TrucoClientSettings.ApplyAsset(so);
            TrucoLocalization.ApplyFromSettings();
            Debug.Log("[TrucoClientSettings] Applied from inspector.");
        }
    }
}
