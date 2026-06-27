using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Ensures premium systems exist and applies scene-wide button feedback after every load.</summary>
[DefaultExecutionOrder(-900)]
public class TrucoPremiumUiBootstrap : MonoBehaviour
{
    static TrucoPremiumUiBootstrap _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (_instance != null) return;
        TrucoSceneTransition.Ensure();
        var go = new GameObject("[TrucoPremiumUiBootstrap]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<TrucoPremiumUiBootstrap>();
        _instance.Apply();
    }

    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        Apply();
    }

    void Apply()
    {
        StartCoroutine(ApplyDeferred());
    }

    IEnumerator ApplyDeferred()
    {
        // Wait one frame so runtime-built UI (factories) has spawned its buttons.
        yield return null;
        TrucoUiMotion.AttachButtonFeedbackSceneWide();
        // Second pass a moment later for async/lazy panels.
        yield return new WaitForSecondsRealtime(0.4f);
        TrucoUiMotion.AttachButtonFeedbackSceneWide();
    }
}
