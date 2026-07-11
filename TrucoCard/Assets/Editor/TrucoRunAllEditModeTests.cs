#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>Run all Truco EditMode tests from the menu (Window → General → Test Runner also works).</summary>
public static class TrucoRunAllEditModeTests
{
    [MenuItem("Truco/Run All EditMode Tests")]
    public static void RunFromMenu()
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        var filter = new Filter
        {
            testMode = TestMode.EditMode,
            groupNames = Array.Empty<string>(),
            categoryNames = Array.Empty<string>()
        };
        api.Execute(new ExecutionSettings(filter));
        Debug.Log("[Truco] EditMode test run started — open Window → General → Test Runner for results.");
    }
}
#endif
