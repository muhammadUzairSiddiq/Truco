using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Read-only project inspection. Does not infer backend/device acceptance from code.</summary>
public static class TrucoReleasePreflight
{
    [Serializable] public class Check { public string name; public bool passed; public string detail; }
    [Serializable] public class Report
    {
        public string unityVersion, bundleVersion;
        public int androidVersionCode;
        public bool automatedChecksPassed;
        public string acceptanceNote;
        public List<Check> checks = new List<Check>();
    }
    [MenuItem("Truco/Release/Run production preflight")]
    public static void Run()
    {
        var report = Inspect();
        string directory = Path.GetFullPath("Docs/QA/ReleaseAudit-2026-10-31");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "preflight.json"), JsonUtility.ToJson(report, true));
        foreach (var check in report.checks)
            Debug.Log("[RELEASE PREFLIGHT] " + (check.passed ? "PASS " : "BLOCKED ") + check.name + ": " + check.detail);
        Debug.Log("[RELEASE PREFLIGHT] " + report.acceptanceNote);
        if (Application.isBatchMode) EditorApplication.Exit(0); // A completed audit may contain blockers.
    }
    public static Report Inspect()
    {
        var report = new Report { unityVersion = Application.unityVersion, bundleVersion = PlayerSettings.bundleVersion,
            androidVersionCode = PlayerSettings.Android.bundleVersionCode,
            acceptanceNote = "Static checks do not certify production. Validate the signed Android artifact, authoritative backend workflows and real multiplayer reconnect/settlement before release." };
        void Add(string name, bool pass, string detail) => report.checks.Add(new Check { name = name, passed = pass, detail = detail });
        var practice = TrucoTournamentPracticeSettings.Load();
        Add("Editor tournament AI disabled", practice != null && !practice.enabledInEditor && !practice.autoPlayYourSeat,
            "TournamentPracticeSettings: enabledInEditor and autoPlayYourSeat must be false for the release handoff.");
        Add("Legacy AI disabled", TrucoDevelopmentSettings.Current != null && TrucoDevelopmentSettings.Current.mode == TrucoExecutionMode.Multiplayer,
            "TrucoDevelopmentSettings mode must be Multiplayer.");
        Add("No AI Resources asset", AssetDatabase.LoadMainAssetAtPath("Assets/Resources/TrucoDevelopmentSettings.asset") == null,
            "AI settings belong under Assets/Editor, outside player Resources.");
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Add("Enabled scenes exist", scenes.Length > 0 && scenes.All(File.Exists), string.Join(", ", scenes));
        var missing = new List<string>();
        foreach (string path in scenes.Where(File.Exists))
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var item in root.GetComponentsInChildren<Transform>(true))
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) > 0)
                            missing.Add(path + " :: " + item.name);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        Add("No missing scene scripts", missing.Count == 0, missing.Count == 0 ? "All enabled scenes inspected." : string.Join("; ", missing));
        string[] dependencies = AssetDatabase.GetDependencies(scenes, true);
        Add("Editor practice not scene dependencies", !dependencies.Any(p => p.StartsWith("Assets/Editor/", StringComparison.OrdinalIgnoreCase)),
            "Enabled build scenes must not reference Editor-only practice assets.");
        var endpoints = Resources.Load<TrucoApiEndpointsSO>("TrucoApiEndpoints");
        foreach (string key in new[] { "WalletSnapshot", "WalletCreateRequest", "WalletRequestStatus", "AdminAudit", "AdminSetChampion", "PhotonSession", "TournamentSnapshot", "TournamentJoin", "TournamentStart", "TournamentLeave", "TournamentReconcile", "TournamentFinalizeMatch", "TournamentFinalize", "MatchCancelLobbyRefund" })
            Add(key + " configured", endpoints != null && endpoints.TryGet(key, out var route) && route.StartsWith("/", StringComparison.Ordinal),
                "Client route configured; backend implementation and contract acceptance remain required.");
        if (endpoints != null)
        {
            var payload = new TrucoRemoteApiConfiguration.Payload { schemaVersion = 1, revision = endpoints.remoteConfigurationRevision,
                minimumClientContractVersion = 1, apiBaseUrl = endpoints.baseUrl, endpoints = endpoints.endpoints.ToArray() };
            bool valid = TrucoRemoteApiConfiguration.Validate(payload, endpoints, endpoints.remoteConfigurationRevision, out var configurationError);
            Add("API catalog validates", valid, valid ? endpoints.endpoints.Count + " configured logical keys." : configurationError);
        }
        Add("Android build support installed", BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android),
            "Checks the installed Unity Android target; does not build or sign an APK.");
        string android = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer");
        foreach (string tool in new[] { "SDK", "NDK", "OpenJDK" })
            Add("Bundled Android " + tool, Directory.Exists(Path.Combine(android, tool)),
                "An external toolchain may be configured separately; validate the actual Android build.");
        report.automatedChecksPassed = report.checks.All(c => c.passed);
        return report;
    }
}
