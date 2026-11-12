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

    [TestCase(320, 568)]
    [TestCase(480, 800)]
    [TestCase(390, 844)]
    public void ConfirmationActionsAndContentStayInsideThePortraitViewport(int width, int height)
    {
        var canvas = _root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var bounds = (RectTransform)_root.transform;
        bounds.sizeDelta = new Vector2(1080, 1080f * height / width);
        var prompt = TrucoThemeRuntime.BuildPrompt(bounds, TrucoCanvasOrder.Confirm, twoActions: true);
        prompt.Title.text = "INSCRIBIRSE AL TORNEO";
        prompt.SetBody("Prod Smoke 701\n4 jugadores · 15 puntos\nCon Flor\nEntrada: 10 Trucoins\nPremio previsto: 40 Trucoins");
        TrucoThemeRuntime.SetAction(prompt.Secondary, "VOLVER", null);
        TrucoThemeRuntime.SetAction(prompt.Primary, "INSCRIBIRME", null);
        foreach (var item in new[] { prompt.Title.rectTransform, (RectTransform)prompt.Secondary.transform,
                     (RectTransform)prompt.Primary.transform })
        {
            var corners = new Vector3[4];
            item.GetWorldCorners(corners);
            Assert.That(corners.All(corner => bounds.rect.Contains(bounds.InverseTransformPoint(corner))));
        }
        var scroll = bounds.GetComponentInChildren<ScrollRect>();
        Assert.That(scroll.content.rect.height, Is.LessThanOrEqualTo(scroll.viewport.rect.height));
        prompt.SetBody(string.Join("\n", Enumerable.Repeat("La respuesta del servidor todavía requiere confirmación.", 32)));
        Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
        Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(.01f));
    }

    [Test]
    public void ConfirmationKeepsEachActionDistinctAndClosesBeforeCallingBack()
    {
        int confirm = 0, cancel = 0;
        void Show() => TrucoConfirmDialog.Show("INSCRIBIRSE AL TORNEO", "Entrada: 10 Trucoins", "VOLVER", "INSCRIBIRME",
            () => { Assert.IsFalse(TrucoConfirmDialog.IsVisible()); confirm++; },
            () => { Assert.IsFalse(TrucoConfirmDialog.IsVisible()); cancel++; });
        Show();
        var dialog = GameObject.Find("TrucoConfirmDialogCanvas");
        try
        {
            dialog.GetComponentsInChildren<Button>().Single(button => button.name == "PromptSecondary").onClick.Invoke();
            Show();
            dialog.GetComponentsInChildren<Button>().Single(button => button.name == "PromptPrimary").onClick.Invoke();
            Assert.AreEqual(1, confirm);
            Assert.AreEqual(1, cancel);
        }
        finally { Object.DestroyImmediate(dialog); }
    }

    [Test]
    public void RuntimeNotificationAndLoadingCreateTheirOwnLayersAndCloseCorrectly()
    {
        var noticeHolder = TrucoClubUI.Rect("Notice", _root.transform, Vector2.zero, Vector2.one);
        var loadingHolder = TrucoClubUI.Rect("Loading", _root.transform, Vector2.zero, Vector2.one);
        var notice = _root.AddComponent<NotificationOverlay>();
        var loading = _root.AddComponent<LoadingOverlay>();
        typeof(NotificationOverlay).GetField("_contentHolder", Fields).SetValue(notice, noticeHolder.gameObject);
        typeof(LoadingOverlay).GetField("_contentHolder", Fields).SetValue(loading, loadingHolder.gameObject);
        noticeHolder.gameObject.SetActive(false);
        loadingHolder.gameObject.SetActive(false);
        loading.DisplayLoadingUI("Conectando…");
        Assert.IsTrue(loadingHolder.gameObject.activeSelf);
        Assert.AreEqual(TrucoCanvasOrder.Loading, loadingHolder.GetComponent<Canvas>().sortingOrder);
        loading.HideLoadingUI();
        Assert.IsFalse(loadingHolder.gameObject.activeSelf);

        notice.DisplayNotification("Primer mensaje", () => notice.DisplayNotification("Segundo mensaje"));
        Assert.AreEqual(TrucoCanvasOrder.Notification, noticeHolder.GetComponent<Canvas>().sortingOrder);
        var close = noticeHolder.GetComponentInChildren<Button>();
        close.onClick.Invoke();
        Assert.IsTrue(noticeHolder.gameObject.activeSelf);
        Assert.That(noticeHolder.GetComponentsInChildren<TextMeshProUGUI>().Any(text => text.text == "Segundo mensaje"));
        close.onClick.Invoke();
        Assert.IsFalse(noticeHolder.gameObject.activeSelf);
    }
}
