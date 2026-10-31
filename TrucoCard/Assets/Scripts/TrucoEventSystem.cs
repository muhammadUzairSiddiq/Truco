using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>Creates a usable UI event system for screens opened without their normal scene bootstrap.</summary>
public static class TrucoEventSystem
{
    public static EventSystem Ensure()
    {
        var events = EventSystem.current != null ? EventSystem.current : Object.FindFirstObjectByType<EventSystem>();
        if (events == null) events = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        foreach (var legacy in events.GetComponents<StandaloneInputModule>()) legacy.enabled = false;
#pragma warning disable CS0618
        foreach (var legacy in events.GetComponents<TouchInputModule>()) legacy.enabled = false;
#pragma warning restore CS0618
        var input = events.currentInputModule as InputSystemUIInputModule;
        if (input == null) input = events.GetComponent<InputSystemUIInputModule>();
        if (input == null) input = events.gameObject.AddComponent<InputSystemUIInputModule>();
        if (input.actionsAsset == null && input.point?.action == null && input.move?.action == null && input.leftClick?.action == null
            && input.rightClick?.action == null && input.middleClick?.action == null && input.scrollWheel?.action == null
            && input.submit?.action == null && input.cancel?.action == null && input.trackedDevicePosition?.action == null
            && input.trackedDeviceOrientation?.action == null) input.AssignDefaultActions();
        input.enabled = true;
#else
        var input = events.GetComponent<StandaloneInputModule>();
        if (input == null) input = events.gameObject.AddComponent<StandaloneInputModule>();
        input.enabled = true;
#endif
        events.enabled = true;
        return events;
    }
}
