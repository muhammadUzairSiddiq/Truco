using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Exercises real mixed-canvas ordering and pointer dispatch. A screenshot of an
/// isolated canvas cannot detect a wrapped sorting order behind the main menu.
/// </summary>
public sealed class TrucoCanvasLayerTests
{
    readonly List<GameObject> _objects = new List<GameObject>();
    EventSystem _events;

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        TrucoTournamentPracticeUI.Close();
        for (int i = _objects.Count - 1; i >= 0; i--)
            if (_objects[i] != null)
            {
                if (Application.isPlaying) Object.Destroy(_objects[i]);
                else Object.DestroyImmediate(_objects[i]);
            }
        _objects.Clear();
        if (EditorApplication.isPlaying)
        {
            yield return null;
            yield return new ExitPlayMode();
        }
        TrucoPracticeTestSettingsScope.Restore(nameof(TrucoCanvasLayerTests));
    }

    [Test]
    public void EveryOverlayOrderRoundTripsThroughUnityWithoutSignedShortWrapping()
    {
        var canvas = Track(new GameObject("LayerRangeRegression", typeof(Canvas))).GetComponent<Canvas>();
        foreach (var field in typeof(TrucoCanvasOrder).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType != typeof(int) || !field.IsLiteral) continue;
            int order = (int)field.GetRawConstantValue();
            Assert.That(order, Is.InRange((int)short.MinValue, (int)short.MaxValue), field.Name);
            canvas.sortingOrder = order;
            Assert.AreEqual(order, canvas.sortingOrder, field.Name + " must survive Unity's signed 16-bit storage.");
            if (field.Name != nameof(TrucoCanvasOrder.Navigation))
                Assert.Greater(order, TrucoCanvasOrder.Navigation, field.Name + " must appear above the navigation canvas.");
        }
        Assert.Greater(TrucoCanvasOrder.Transition, TrucoCanvasOrder.Confirm);
        Assert.Greater(TrucoCanvasOrder.Confirm, TrucoCanvasOrder.EditorPractice);
        Assert.Greater(TrucoCanvasOrder.Bracket, TrucoCanvasOrder.TournamentStatus,
            "VER CUADRO must open above the waiting/status screen that launched it.");
    }

    [UnityTest]
    public IEnumerator RealOverlayGraphicsReceivePointersAboveTheMenuAfterRenderedFrames()
    {
        TrucoPracticeTestSettingsScope.Capture(nameof(TrucoCanvasLayerTests));
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var settings = TrucoTournamentPracticeSettings.Load();
        settings.enabledInEditor = true; settings.autoPlayYourSeat = false;
        // Runtime bootstraps create the real transition canvas. Allow its normal
        // fade to release pointer capture before testing the UI underneath it.
        yield return new WaitForSecondsRealtime(.65f);
        CreateMenuAndEvents();
        yield return RenderedFrames();
        TrucoTournamentPracticeUI.OpenList();
        yield return RenderedFrames();
        var practice = TrucoTournamentPracticeUI.Root;
        Assert.AreEqual(TrucoCanvasOrder.EditorPractice, practice.GetComponent<Canvas>().sortingOrder);
        var details = practice.GetComponentsInChildren<Button>(true)
            .First(button => button.name.StartsWith("VER TORNEO", StringComparison.Ordinal));

        // Raycast the actual visible control with the old menu still present.
        // Invoke onClick directly would pass even if that control was behind it.
        var hit = FirstHitAt(details.transform as RectTransform);
        Assert.AreSame(details, hit.gameObject.GetComponentInParent<Button>(),
            "The tournament entry must be the top pointer target above menu order 32000. Top hit: " +
            hit.gameObject.name + " on " + hit.gameObject.GetComponentInParent<Canvas>()?.name +
            " order=" + hit.gameObject.GetComponentInParent<Canvas>()?.sortingOrder);
        Click(hit);
        yield return RenderedFrames();

        Assert.IsNotNull(TrucoTournamentPracticeUI.Root);
        Assert.IsTrue(TrucoTournamentPracticeUI.Root.GetComponentsInChildren<TextMeshProUGUI>(true)
            .Any(label => label.text == "DETALLES DEL TORNEO"), "A pointer click must open the details screen.");
        Assert.AreEqual(TrucoCanvasOrder.EditorPractice,
            TrucoTournamentPracticeUI.Root.GetComponent<Canvas>().sortingOrder, "Subsequent pages must retain the visible layer.");
        TrucoTournamentPracticeUI.Close();
        yield return RenderedFrames();

        // Build real production presentation only: no singleton creation, server
        // requests, match registration, Photon connection or economy operations.
        var productionTypes = new[] { typeof(TournamentBracketOverlay), typeof(TournamentChampionOverlay), typeof(TrucoTournamentStatusScreen) };
        int[] productionOrders = { TrucoCanvasOrder.Bracket, TrucoCanvasOrder.Champion, TrucoCanvasOrder.TournamentStatus };
        for (int i = 0; i < productionTypes.Length; i++)
        {
            var componentType = productionTypes[i];
            var root = Track(new GameObject(componentType.Name + "LayerRegression"));
            var component = root.AddComponent(componentType);
            componentType.GetMethod("Build", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(component, null);
            root.SetActive(true);
            yield return RenderedFrames();
            var canvas = root.GetComponent<Canvas>();
            Assert.AreEqual(productionOrders[i], canvas.sortingOrder, componentType.Name);
            Assert.Greater(canvas.sortingOrder, TrucoCanvasOrder.Navigation);
            hit = FirstHitAt(root.transform as RectTransform);
            Assert.IsTrue(hit.gameObject.transform.IsChildOf(root.transform),
                componentType.Name + " must intercept pointers above the underlying menu.");
            root.SetActive(false); Object.Destroy(root);
            yield return RenderedFrames();
        }

        var snapshot = new TrucoWalletService.Snapshot { available = 100, held = 0 };
        var wallet = Track(TrucoWalletScreen.EditorPreview(0, snapshot));
        yield return RenderedFrames();
        Assert.AreEqual(TrucoCanvasOrder.Screen, wallet.GetComponent<Canvas>().sortingOrder);
        var buy = wallet.GetComponentsInChildren<Button>(true).Single(button => button.name == "COMPRAR TRUCOINS");
        hit = FirstHitAt(buy.transform as RectTransform);
        Assert.AreSame(buy, hit.gameObject.GetComponentInParent<Button>());
        Click(hit);
        yield return RenderedFrames();
        Assert.IsNotEmpty(wallet.GetComponentsInChildren<TMP_InputField>(true),
            "The wallet control must also respond to a pointer click above the menu.");
    }

    [UnityTest]
    public IEnumerator PlayerBracketReceivesPointersAboveTheWaitingScreenWithAIAbsent()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        // Direct empty-scene entry has no sceneLoaded callback to finish the bootstrap transition.
        TrucoSceneTransition.ReleaseCover();
        TrucoSceneTransition.Ensure().FadeIn(.15f);
        yield return new WaitForSecondsRealtime(.25f);
        CreateMenuAndEvents();
        var status = Track(TrucoTournamentStatusScreen.EditorPreview(TrucoTournamentPhase.Filling));
        var show = status.GetComponentsInChildren<Button>().Single(button => button.name == "VER CUADRO");
        // A read-only empty format needs no API account, registration or development AI.
        show.onClick.RemoveAllListeners();
        show.onClick.AddListener(() => TournamentBracketOverlay.ShowFormat(8));
        yield return RenderedFrames();
        var hit = FirstHitAt(show.transform as RectTransform);
        Assert.AreSame(show, hit.gameObject.GetComponentInParent<Button>(),
            "Top hit: " + hit.gameObject.name + " on " + hit.gameObject.GetComponentInParent<Canvas>()?.name);
        Click(hit);
        var bracket = Object.FindFirstObjectByType<TournamentBracketOverlay>();
        Assert.IsNotNull(bracket); Track(bracket.gameObject);
        yield return RenderedFrames();
        var back = bracket.transform.Find("Root/Leave").GetComponent<Button>();
        hit = FirstHitAt(back.transform as RectTransform);
        Assert.AreSame(back, hit.gameObject.GetComponentInParent<Button>(),
            "The visible bracket must receive taps above the opaque waiting screen.");
        Click(hit);
        yield return RenderedFrames();
        Assert.IsFalse(bracket.gameObject.activeSelf);
        Assert.IsTrue(status.activeInHierarchy);
    }

    static IEnumerator RenderedFrames()
    {
        // Graphic.depth and CanvasRenderer state require a real player frame;
        // EditMode layout rebuilding alone produces no raycastable graphics.
        for (int i = 0; i < 3; i++) { Canvas.ForceUpdateCanvases(); yield return null; }
        Canvas.ForceUpdateCanvases();
    }

    void Click(RaycastResult hit)
    {
        var pointer = new PointerEventData(_events) {
            position = hit.screenPosition, button = PointerEventData.InputButton.Left
        };
        ExecuteEvents.ExecuteHierarchy(hit.gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }

    void CreateMenuAndEvents()
    {
        var events = Track(new GameObject("CanvasRegressionEventSystem", typeof(EventSystem)));
        _events = events.GetComponent<EventSystem>();
        var menu = Track(new GameObject("UnderlyingMenuCanvas", typeof(Canvas), typeof(GraphicRaycaster)));
        var canvas = menu.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var blocker = new GameObject("UnderlyingMenuPointerTarget", typeof(RectTransform), typeof(Image));
        blocker.transform.SetParent(menu.transform, false);
        var rect = blocker.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    RaycastResult FirstHitAt(RectTransform target)
    {
        Assert.IsNotNull(target);
        for (int i = 0; i < 3; i++) Canvas.ForceUpdateCanvases();
        var corners = new Vector3[4]; target.GetWorldCorners(corners);
        var point = RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[2]) * .5f);
        var pointer = new PointerEventData(_events) { position = point };
        var hits = new List<RaycastResult>(); _events.RaycastAll(pointer, hits);
        Assert.IsNotEmpty(hits, "Real UI graphics must be raycastable at " + point);
        return hits[0];
    }

    GameObject Track(GameObject value) { _objects.Add(value); return value; }
}
