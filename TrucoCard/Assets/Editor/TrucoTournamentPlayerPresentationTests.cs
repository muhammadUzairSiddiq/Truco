using System;
using System.Linq;
using System.Reflection;
using MH.Multiplayer;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Exercises only production UI, with the Editor AI switch off and no backend/Photon operations.</summary>
public sealed class TrucoTournamentPlayerPresentationTests
{
    GameObject _root;
    TournamentSelectionOverlay _overlay;
    bool _practice;
    static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void BuildPlayerBrowser()
    {
        var settings = TrucoTournamentPracticeSettings.Load();
        _practice = settings.enabledInEditor; settings.enabledInEditor = false;
        _root = new GameObject("PlayerTournamentBrowser", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        var holder = TrucoClubUI.Panel("Content", _root.transform, TrucoClubUI.Ink, Vector2.zero, Vector2.one);
        var scroll = TrucoClubUI.Rect("Scroll View", holder.transform, Vector2.zero, Vector2.one);
        var content = TrucoClubUI.Rect("Content", scroll, new Vector2(0, 1), Vector2.one);
        _overlay = _root.AddComponent<TournamentSelectionOverlay>();
        typeof(TournamentSelectionOverlay).GetField("_contentHolder", Fields).SetValue(_overlay, holder.gameObject);
        typeof(TournamentSelectionOverlay).GetField("_tournamentsCardHolder", Fields).SetValue(_overlay, content);
        holder.gameObject.SetActive(false);
    }
    [TearDown]
    public void Cleanup()
    {
        foreach (var bracket in Object.FindObjectsByType<TournamentBracketOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Object.DestroyImmediate(bracket.gameObject);
        if (_root != null) Object.DestroyImmediate(_root);
        TrucoTournamentPracticeSettings.Load().enabledInEditor = _practice;
    }
    Button Button(string caption) => _root.GetComponentsInChildren<Button>(true)
        .Single(button => button.GetComponentInChildren<TextMeshProUGUI>(true)?.text == caption);
    string State => _root.GetComponentsInChildren<TextMeshProUGUI>(true).Single(t => t.name == "TournamentListState").text;

    [Test]
    public void LoadingAndFailureKeepThePlayerScreenAndBothFormatActionsAvailable()
    {
        _overlay.DisplayLoading();
        Assert.IsTrue(_overlay.IsVisible);
        StringAssert.Contains("CARGANDO", State);
        Assert.IsFalse(Button("ACTUALIZAR").interactable);
        Assert.IsTrue(Button("4 JUGADORES").isActiveAndEnabled);
        Assert.IsTrue(Button("8 JUGADORES").isActiveAndEnabled);
        _overlay.DisplayUnavailable();
        Assert.IsTrue(_overlay.IsVisible);
        StringAssert.Contains("NO DISPONIBLES", State);
        Assert.IsTrue(Button("ACTUALIZAR").interactable);
        Assert.IsTrue(Button("4 JUGADORES").interactable);
        Assert.IsTrue(Button("8 JUGADORES").interactable);
    }

    [TestCase(4)]
    [TestCase(8)]
    public void PlayerCanOpenAndCloseTheRealBracketRendererWithoutJoining(int count)
    {
        var snapshot = MultiplayerController.CurrentSnapshot;
        var active = MultiplayerController.IsTournamentActive;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        _overlay.DisplayUnavailable();
        Button(count + " JUGADORES").onClick.Invoke();
        var bracket = Object.FindFirstObjectByType<TournamentBracketOverlay>();
        Assert.IsNotNull(bracket);
        var state = (TournamentBracketState)typeof(TournamentBracketOverlay).GetField("_format", Fields).GetValue(bracket);
        Assert.AreEqual(count, state.maxPlayers);
        var host = (Transform)typeof(TournamentBracketOverlay).GetField("_roundsHost", Fields).GetValue(bracket);
        Assert.AreEqual(count - 1, host.Cast<Transform>().Count(child => child.name.StartsWith("Match_")));
        Assert.IsTrue(state.rounds.SelectMany(round => round).All(match => !match.IsReady && !match.IsFinished));
        StringAssert.Contains("FORMATO DEL TORNEO", bracket.transform.Find("Root/Header/Status").GetComponent<TextMeshProUGUI>().text);
        Assert.IsFalse(bracket.transform.Find("Root/Refresh").gameObject.activeSelf);
        bracket.transform.Find("Root/Leave").GetComponent<Button>().onClick.Invoke();
        Assert.IsFalse(bracket.gameObject.activeSelf);
        Assert.IsTrue(_overlay.IsVisible);
        Assert.AreSame(snapshot, MultiplayerController.CurrentSnapshot);
        Assert.AreEqual(active, MultiplayerController.IsTournamentActive);
        Assert.AreEqual(scene, UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    [Test]
    public void EmptyListIsDistinctFromFailureAndRepeatedStatesDoNotDuplicateChrome()
    {
        _overlay.DisplayLoading(); _overlay.DisplayUnavailable();
        _overlay.DisplayTournamentSelectionUI(Array.Empty<Tournament>());
        StringAssert.Contains("NO HAY TORNEOS ABIERTOS", State);
        Assert.AreEqual(1, _root.GetComponentsInChildren<Transform>(true).Count(t => t.name == "TournamentFormats"));
        Assert.AreEqual(1, _root.GetComponentsInChildren<Transform>(true).Count(t => t.name == "TournamentListState"));
        _overlay.HideTournamentSelectionUI();
        Assert.IsFalse(_overlay.IsVisible);
    }

    [Test]
    public void OpeningLiveBracketClearsTheReadOnlyFormatMode()
    {
        TournamentBracketOverlay.ShowFormat(8);
        var overlay = Object.FindFirstObjectByType<TournamentBracketOverlay>();
        TournamentBracketOverlay.Show(false);
        Assert.IsNull(typeof(TournamentBracketOverlay).GetField("_format", Fields).GetValue(overlay));
        Assert.IsTrue(overlay.transform.Find("Root/Refresh").gameObject.activeSelf);
    }
}
