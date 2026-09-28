using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TrucoGameplaySceneWiringTests
{
    [Test]
    public void TournamentUsesExistingGameplaySceneWithBothThreeCardHands()
    {
        const string path = "Assets/Scenes/Gameplay.unity";
        var scene = SceneManager.GetSceneByPath(path);
        bool openedHere = !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var roots = scene.GetRootGameObjects();
            Assert.AreEqual(1, roots.SelectMany(r => r.GetComponentsInChildren<GameManager>(true)).Count());
            Assert.AreEqual(1, roots.SelectMany(r => r.GetComponentsInChildren<TurnManager>(true)).Count());
            var ui = roots.SelectMany(r => r.GetComponentsInChildren<UIMANAGER>(true)).Single();
            var serialized = new SerializedObject(ui);
            foreach (string field in new[] { "myPlayerCards", "otherPlayersCards",
                         "myDisplayCardsPosition", "otherPlayersDisplayCardsPosition" })
            {
                var property = serialized.FindProperty(field);
                Assert.IsNotNull(property, field);
                Assert.AreEqual(3, property.arraySize, field);
                for (int i = 0; i < 3; i++)
                    Assert.IsNotNull(property.GetArrayElementAtIndex(i).objectReferenceValue, field + " slot " + i);
            }
            foreach (string field in new[] { "truco", "retruco", "vale4", "envido", "realEnvido", "faltaEnvido",
                         "queiro", "noQueiro", "flor", "conFlorQuiero", "contraFlor", "florChica", "mazo", "turnText" })
                Assert.IsNotNull(serialized.FindProperty(field).objectReferenceValue, field);
        }
        finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void RequiredSceneTransitionsAreIncludedInPlayerBuild()
    {
        foreach (string scene in new[] { "Gameplay", "MainMenu", "TempScene" })
            Assert.IsTrue(EditorBuildSettings.scenes.Any(s => s.enabled && s.path.EndsWith("/" + scene + ".unity")), scene);
    }

    [Test]
    public void TournamentSpriteSheetHasTimerAndCardAssets()
    {
        Assert.IsNotNull(TrucoGameplayUiSprites.ButtonRound, "Circular timer asset");
        Assert.IsNotNull(TrucoGameplayUiSprites.PanelGreen, "Tournament card asset");
        Assert.IsNotNull(TrucoGameplayUiSprites.IconTrophy, "Prize icon");
        Assert.IsNotNull(TrucoGameplayUiSprites.IconFlor, "Flor setting icon");
    }
}
