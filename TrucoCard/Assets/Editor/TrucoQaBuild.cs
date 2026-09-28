using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Build verification only. Refuses the real product and never uses release signing.</summary>
public static class TrucoQaBuild
{
    public static void Android()
    {
        if (!PlayerSettings.productName.StartsWith("Truco-QA-", StringComparison.Ordinal))
            throw new InvalidOperationException("Run this only inside an isolated QA clone.");
        PlayerSettings.Android.useCustomKeystore = false;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.truco.qa.clienta");
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds"));
        Directory.CreateDirectory(directory);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = Path.Combine(directory, "Truco-QA-ClientA.apk"),
            target = BuildTarget.Android,
            options = BuildOptions.Development
        });
        Debug.Log("[QA BUILD] " + report.summary.result + "; errors=" + report.summary.totalErrors);
        if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
