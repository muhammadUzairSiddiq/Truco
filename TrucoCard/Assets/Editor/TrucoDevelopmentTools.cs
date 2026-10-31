using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class TrucoDevelopmentTools : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;
    public void OnPreprocessBuild(BuildReport report)
    {
        if ((report.summary.options & BuildOptions.Development) == 0 && TrucoDevelopmentSettings.Current != null
            && TrucoDevelopmentSettings.Current.mode != TrucoExecutionMode.Multiplayer)
            throw new BuildFailedException("Select Multiplayer in the Editor development settings. All AI code is Editor-only.");
    }
    [MenuItem("Truco/Development/Settings")]
    public static void Settings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<TrucoDevelopmentSettings>("Assets/Editor/TrucoDevelopmentSettings.asset");
        if (settings == null) { settings = ScriptableObject.CreateInstance<TrucoDevelopmentSettings>(); AssetDatabase.CreateAsset(settings, "Assets/Editor/TrucoDevelopmentSettings.asset"); AssetDatabase.SaveAssets(); }
        Selection.activeObject = settings;
    }
    [MenuItem("Truco/Development/Start AI peer (Play Mode)")]
    public static void StartPeer()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("Enter Play Mode first. Two independent Unity instances are required for a real Photon match."); return; }
        TrucoDevelopmentBot.StartTestPeer();
    }
}
