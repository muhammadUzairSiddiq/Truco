#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Keeps URP pipeline assets aligned with the installed package version so builds never fail
/// with "not at last version" when assets were saved from a newer Unity/URP editor.
/// </summary>
public static class TrucoUrpAssetVersionFix
{
    const string PipelineVersionField = "k_AssetVersion";
    const string PipelinePreviousVersionField = "k_AssetPreviousVersion";
    const string GlobalVersionField = "m_AssetVersion";
    const string GlobalSettingsTypeName = "UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings";

    static Type _globalSettingsType;

    [InitializeOnLoadMethod]
    static void ScheduleFixOnLoad() => EditorApplication.delayCall += FixAll;

    [MenuItem("Truco/Fix URP Asset Versions")]
    public static void FixAllFromMenu()
    {
        FixAll();
        RepairGlobalSettingsYamlIfNeeded();
    }

    [MenuItem("Truco/Repair URP Global Settings (missing types)")]
    public static void RepairGlobalSettingsFromMenu()
    {
        if (RepairGlobalSettingsYamlIfNeeded())
            Debug.Log("[TrucoUrpAssetVersionFix] Removed stale URP global settings references.");
        FixAll();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static void FixAllFromCommandLine()
    {
        FixAll();
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }

    public static void FixAll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        int pipelineLast = GetConstInt(typeof(UniversalRenderPipelineAsset), "k_LastVersion");
        int globalLast = GetGlobalSettingsLastVersion();
        if (pipelineLast <= 0 || globalLast <= 0)
            return;

        bool changed = false;
        changed |= FixPipelineAssets(pipelineLast);
        changed |= FixGlobalSettings(globalLast);
        changed |= RepairGlobalSettingsYamlIfNeeded();

        if (changed)
            AssetDatabase.SaveAssets();
    }

    /// <summary>Strip serialized settings from a newer Unity build that this URP package does not include.</summary>
    static bool RepairGlobalSettingsYamlIfNeeded()
    {
        const string staleMarker = "6520489364854931460";
        bool changed = false;
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineGlobalSettings"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
            var text = File.ReadAllText(path);
            if (!text.Contains(staleMarker)) continue;

            for (long rid = 6520489364854931460; rid <= 6520489364854931472; rid++)
            {
                text = text.Replace($"      - rid: {rid}\n", string.Empty);
                text = text.Replace($"      - rid: {rid}\r\n", string.Empty);
            }

            text = Regex.Replace(
                text,
                @"    - rid: 652048936485493146\d\r?\n      type:.*?(?=    - rid: 6753247021403209858)",
                string.Empty,
                RegexOptions.Singleline);

            File.WriteAllText(path, text);
            Debug.Log($"[TrucoUrpAssetVersionFix] Repaired stale references in {path}.");
            changed = true;
        }

        return changed;
    }

    static bool FixPipelineAssets(int lastVersion)
    {
        bool changed = false;
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset == null)
                continue;

            var so = new SerializedObject(asset);
            var version = so.FindProperty(PipelineVersionField);
            var previous = so.FindProperty(PipelinePreviousVersionField);
            if (version == null || previous == null)
                continue;

            int current = version.intValue;
            if (current == lastVersion)
                continue;

            if (current < lastVersion)
            {
                InvokeStaticMethod(typeof(UniversalRenderPipelineAsset), "UpgradeAsset", asset.GetInstanceID());
                so.Update();
                current = version.intValue;
            }

            if (current != lastVersion)
            {
                version.intValue = lastVersion;
                previous.intValue = lastVersion;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                Debug.Log($"[TrucoUrpAssetVersionFix] Set {path} URP pipeline version {current} -> {lastVersion}.");
                changed = true;
            }
        }

        return changed;
    }

    static bool FixGlobalSettings(int lastVersion)
    {
        var globalType = GetGlobalSettingsType();
        if (globalType == null)
            return false;

        bool changed = false;
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineGlobalSettings"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null || !globalType.IsInstanceOfType(asset))
                continue;

            var so = new SerializedObject(asset);
            var version = so.FindProperty(GlobalVersionField);
            if (version == null)
                continue;

            int current = version.intValue;
            if (current == lastVersion)
                continue;

            if (current < lastVersion)
            {
                InvokeStaticMethod(globalType, "UpgradeAsset", asset.GetInstanceID());
                so.Update();
                current = version.intValue;
            }

            if (current != lastVersion)
            {
                version.intValue = lastVersion;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                Debug.Log($"[TrucoUrpAssetVersionFix] Set {path} URP global settings version {current} -> {lastVersion}.");
                changed = true;
            }
        }

        return changed;
    }

    static Type GetGlobalSettingsType()
    {
        if (_globalSettingsType != null)
            return _globalSettingsType;

        _globalSettingsType = typeof(UniversalRenderPipelineAsset).Assembly.GetType(GlobalSettingsTypeName);
        return _globalSettingsType;
    }

    static int GetGlobalSettingsLastVersion()
    {
        var globalType = GetGlobalSettingsType();
        return globalType != null ? GetConstInt(globalType, "k_LastVersion") : -1;
    }

    static void InvokeStaticMethod(Type type, string methodName, int instanceId)
    {
        var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        method?.Invoke(null, new object[] { instanceId });
    }

    static int GetConstInt(Type type, string name)
    {
        var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        return field != null ? (int)field.GetValue(null) : -1;
    }
}

public class TrucoUrpAssetVersionFixBuild : IPreprocessBuildWithReport
{
    public int callbackOrder => int.MinValue + 99;

    public void OnPreprocessBuild(BuildReport report) => TrucoUrpAssetVersionFix.FixAll();
}
#endif
