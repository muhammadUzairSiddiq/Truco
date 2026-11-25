using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>Reproducible focused checks from the menu or a local CI request file. Never included in players.</summary>
[InitializeOnLoad]
public static class TrucoTournamentVerificationRunner
{
    const string RequestPath = "Temp/TrucoTournamentVerification.request";
    const string ResultPath = "Temp/TrucoTournamentVerification.xml";
    static TestRunnerApi _runner;

    static TrucoTournamentVerificationRunner() => EditorApplication.update += CheckRequest;
    static void CheckRequest()
    {
        if (_runner != null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
            || EditorApplication.isUpdating || !File.Exists(RequestPath)) return;
        File.Delete(RequestPath);
        Run();
    }

    [MenuItem("Truco/Run Tournament Release Checks")]
    public static void Run()
    {
        if (_runner != null) return;
        Directory.CreateDirectory("Temp");
        _runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        _runner.RegisterCallbacks(new Results());
        _runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
            groupNames = new[] { "TrucoHttpSessionStateTests", "TrucoHttpAuthenticationIntegrationTests", "TrucoTournamentContractTests", "TrucoPhotonAuthenticationTests",
                "TrucoTournamentStartupTests", "TrucoTournamentAssignedRoomTests", "TrucoTournamentFeedbackTests", "TrucoReleaseTournamentTests",
                "TrucoTournamentPlayerPresentationTests", "TrucoReleaseGameplayTests" } }));
    }

    sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, Path.GetFullPath(ResultPath));
            Debug.Log($"[Tournament verification] Passed={result.PassCount}, Failed={result.FailCount}, Skipped={result.SkipCount}. Results: {ResultPath}");
            UnityEngine.Object.DestroyImmediate(_runner);
            _runner = null;
        }
    }
}
