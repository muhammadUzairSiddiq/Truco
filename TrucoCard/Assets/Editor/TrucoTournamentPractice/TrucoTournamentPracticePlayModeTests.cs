using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine.TestTools;
using UnityEditor.TestTools;

public class TrucoTournamentPracticePlayModeTests
{
    [UnityTest]
    public IEnumerator EditorBypassButtonsAndAIReachChampionWithoutBackendRegistration()
    {
        TrucoPracticeTestSettingsScope.Capture(nameof(TrucoTournamentPracticePlayModeTests));
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
            UnityEditor.SceneManagement.NewSceneMode.Single);
        yield return new EnterPlayMode();
        var settings = TrucoTournamentPracticeSettings.Load();
        settings.enabledInEditor = true; settings.autoPlayYourSeat = true; settings.aiActionDelay = .05f;
        foreach (int capacity in new[] { 4, 8 })
        {
        MainMenuViewCoordinator.TriggerTournamentFromMain();
        Assert.IsNotNull(TrucoTournamentPracticeUI.Root, "The main-menu entry must work without an authenticated backend session.");
        var details = TrucoTournamentPracticeUI.Root.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name.StartsWith("VER TORNEO") && b.transform.parent.name == "PracticeTournament" + capacity);
        details.onClick.Invoke();
        TrucoTournamentPracticeUI.Root.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == "ENTRAR A LA PRÁCTICA").onClick.Invoke();
        double deadline = EditorApplication.timeSinceStartup + 60;
        bool champion = false;
        while (EditorApplication.timeSinceStartup < deadline)
        {
            champion = TrucoTournamentPracticeUI.Root != null && TrucoTournamentPracticeUI.Root.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text.Contains("¡CAMPEÓN DEL TORNEO!"));
            if (champion) break;
            yield return null;
        }
        Assert.IsTrue(champion, "Practice must reach the champion screen through played AI actions.");
        Assert.IsTrue(TrucoTournamentPracticeUI.Root.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text.Contains((capacity - 1) + " partidas completadas")));
        TrucoTournamentPracticeUI.Close();
        }
        yield return new ExitPlayMode();
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        TrucoTournamentPracticeUI.Close();
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        TrucoPracticeTestSettingsScope.Restore(nameof(TrucoTournamentPracticePlayModeTests));
    }
}
