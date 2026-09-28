using System;
using System.Collections;
using UnityEngine;

/// <summary>Light menu-side cleanup so returning from Gameplay does not keep unused assets resident.</summary>
public static class TrucoClientMemory
{
    public static void UnloadUnusedAfterMenuReady(MonoBehaviour owner)
    {
        if (owner == null || !owner.isActiveAndEnabled) return;
        owner.StartCoroutine(UnloadWhenStable());
    }

    static IEnumerator UnloadWhenStable()
    {
        // Let scene activation, dynamic UI construction, profile binding and Photon
        // cleanup finish before asking Unity to scan and release native resources.
        yield return null;
        yield return null;

        AsyncOperation unload;
        try
        {
            unload = Resources.UnloadUnusedAssets();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TrucoClientMemory] " + ex.Message);
            yield break;
        }

        if (unload != null)
            yield return unload;
    }
}
