using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

public sealed class TrucoEventSystemTests
{
    readonly List<GameObject> _suspended = new List<GameObject>();
    readonly List<GameObject> _created = new List<GameObject>();
    readonly List<Object> _assets = new List<Object>();

    [SetUp]
    public void IsolateExistingEventSystems()
    {
        foreach (var events in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
        {
            if (!events.gameObject.activeSelf || _suspended.Contains(events.gameObject)) continue;
            _suspended.Add(events.gameObject);
            events.gameObject.SetActive(false);
        }
    }

    [TearDown]
    public void RestoreEventSystems()
    {
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        for (int i = _assets.Count - 1; i >= 0; i--)
            if (_assets[i] != null) Object.DestroyImmediate(_assets[i]);
        foreach (var previous in _suspended) if (previous != null) previous.SetActive(true);
        _created.Clear(); _assets.Clear(); _suspended.Clear();
    }

    EventSystem CreateEvents()
    {
        var host = new GameObject("InputFallbackRegression", typeof(EventSystem));
        _created.Add(host);
        return host.GetComponent<EventSystem>();
    }

#if ENABLE_INPUT_SYSTEM
    [Test]
    public void EmptyScreenReceivesDefaultInputActionsAndRepeatedEnsureDoesNotDuplicateModules()
    {
        var events = TrucoEventSystem.Ensure(); _created.Add(events.gameObject);
        var input = events.GetComponent<InputSystemUIInputModule>();
        Assert.IsNotNull(input);
        Assert.IsTrue(events.enabled && input.enabled);
        Assert.IsNotNull(input.actionsAsset);
        Assert.IsNotNull(input.point?.action);
        Assert.IsNotNull(input.leftClick?.action);
        Assert.IsNotNull(input.submit?.action);
        Assert.IsNotNull(input.cancel?.action);
        Assert.IsTrue(input.point.action.enabled);
        Assert.AreSame(events, TrucoEventSystem.Ensure());
        Assert.AreSame(input, TrucoEventSystem.Ensure().GetComponent<InputSystemUIInputModule>());
        Assert.AreEqual(1, events.GetComponents<BaseInputModule>().Length);
    }

    [Test]
    public void LegacyFallbackIsDisabledAndReplacedWithOneEnabledInputSystemModule()
    {
        var events = CreateEvents();
        var legacy = events.gameObject.AddComponent<StandaloneInputModule>();
#pragma warning disable CS0618
        var touch = events.gameObject.AddComponent<TouchInputModule>();
#pragma warning restore CS0618
        TrucoEventSystem.Ensure();
        Assert.IsFalse(legacy.enabled);
        Assert.IsFalse(touch.enabled);
        var input = events.GetComponent<InputSystemUIInputModule>();
        Assert.IsNotNull(input);
        Assert.IsTrue(input.enabled);
        Assert.IsNotNull(input.leftClick?.action);
        TrucoEventSystem.Ensure();
        Assert.AreEqual(1, events.GetComponents<InputSystemUIInputModule>().Length);
    }

    InputActionReference CreateCustomPoint(out InputActionAsset asset)
    {
        asset = ScriptableObject.CreateInstance<InputActionAsset>(); _assets.Add(asset);
        var action = asset.AddActionMap("UI").AddAction("Point", InputActionType.PassThrough, "<Mouse>/position");
        var reference = InputActionReference.Create(action); _assets.Add(reference);
        return reference;
    }

    [Test]
    public void ConfiguredActionAssetAndModuleSettingsArePreserved()
    {
        var events = CreateEvents();
        var input = events.gameObject.AddComponent<InputSystemUIInputModule>();
        var point = CreateCustomPoint(out var asset);
        input.actionsAsset = asset; input.point = point; input.deselectOnBackgroundClick = false;
        // The SDK's actionsAsset setter may create its own InputActionReference
        // wrapper; the action identity and bindings are the configured behavior.
        var configuredAction = point.action;
        var configuredId = configuredAction.id;
        input.enabled = false;
        Assert.AreSame(events, TrucoEventSystem.Ensure());
        Assert.IsTrue(input.enabled);
        Assert.AreSame(asset, input.actionsAsset);
        Assert.AreSame(configuredAction, input.point.action);
        Assert.AreEqual(configuredId, input.point.action.id);
        Assert.AreEqual(1, input.point.action.bindings.Count);
        Assert.AreEqual("<Mouse>/position", input.point.action.bindings[0].effectivePath);
        Assert.IsTrue(input.point.action.enabled);
        Assert.IsFalse(input.deselectOnBackgroundClick);
        Assert.AreEqual(1, events.GetComponents<InputSystemUIInputModule>().Length);
    }

    [Test]
    public void ExplicitActionReferencesArePreservedWithoutAModuleWideAsset()
    {
        var events = CreateEvents();
        var input = events.gameObject.AddComponent<InputSystemUIInputModule>();
        input.UnassignActions();
        var point = CreateCustomPoint(out _); input.point = point;
        TrucoEventSystem.Ensure();
        Assert.IsNull(input.actionsAsset);
        Assert.AreSame(point, input.point);
        Assert.IsNull(input.leftClick);
    }
#else
    [Test]
    public void LegacyOnlyScreenReceivesOneEnabledStandaloneModule()
    {
        var events = TrucoEventSystem.Ensure(); _created.Add(events.gameObject);
        Assert.IsTrue(events.GetComponent<StandaloneInputModule>().enabled);
        TrucoEventSystem.Ensure();
        Assert.AreEqual(1, events.GetComponents<StandaloneInputModule>().Length);
    }
#endif
}
