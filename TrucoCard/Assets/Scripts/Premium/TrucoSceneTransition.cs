using System;
using System.Collections;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Global premium scene transition. A persistent full-screen overlay fades the screen in/out so every
/// scene change (menu, gameplay, login, logout) feels smooth. Photon scene sync is preserved: callers
/// fade to black first, then trigger the real load (SceneManager or PhotonNetwork.LoadLevel).
/// </summary>
[DefaultExecutionOrder(-1000)]
public class TrucoSceneTransition : MonoBehaviour
{
    static TrucoSceneTransition _instance;

    CanvasGroup _group;
    Image _cover;
    Coroutine _running;

    const float DefaultFadeOut = 0.35f;
    const float DefaultFadeIn = 0.45f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        Ensure();
    }

    public static TrucoSceneTransition Ensure()
    {
        if (_instance != null) return _instance;
        var go = new GameObject("[TrucoSceneTransition]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<TrucoSceneTransition>();
        _instance.Build();
        return _instance;
    }

    void Build()
    {
        var canvasGo = new GameObject("TransitionCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        var coverGo = new GameObject("Cover", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        coverGo.transform.SetParent(canvasGo.transform, false);
        _group = coverGo.GetComponent<CanvasGroup>();
        _group.alpha = 1f;
        _group.blocksRaycasts = true;
        _group.interactable = false;

        _cover = coverGo.GetComponent<Image>();
        _cover.color = new Color(0.06f, 0.05f, 0.04f, 1f);
        var rt = _cover.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        BuildSpinner(coverGo.transform);
    }

    RectTransform _spinner;

    void BuildSpinner(Transform parent)
    {
        var go = new GameObject("Spinner", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        _spinner = go.GetComponent<RectTransform>();
        _spinner.anchorMin = _spinner.anchorMax = new Vector2(0.5f, 0.5f);
        _spinner.pivot = new Vector2(0.5f, 0.5f);
        _spinner.sizeDelta = new Vector2(96f, 96f);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.92f, 0.78f, 0.42f, 1f);
        var panel = TrucoUiAssetLoader.Panel;
        if (panel != null) { img.sprite = panel; img.type = Image.Type.Simple; }
        img.preserveAspect = true;
    }

    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        FadeIn();
        if (scene.name == PostLoginSceneRouter.MainMenuSceneName)
            StartCoroutine(RestoreMainMenuNavAfterFade());
    }

    IEnumerator RestoreMainMenuNavAfterFade()
    {
        yield return new WaitForSecondsRealtime(DefaultFadeIn + 0.05f);
        MainMenuViewCoordinator.EnsureBottomNavVisible();
    }

    void Update()
    {
        if (_spinner != null && _group != null && _group.alpha > 0.05f)
            _spinner.Rotate(0f, 0f, -220f * Time.unscaledDeltaTime);
    }

    public static void Go(string sceneName, float fadeOut = DefaultFadeOut)
    {
        Ensure().RunFadeOut(() => SceneManager.LoadScene(sceneName), fadeOut);
    }

    public static void GoPhoton(string sceneName, float fadeOut = DefaultFadeOut)
    {
        Ensure().RunFadeOut(() =>
        {
            if (PhotonNetwork.InRoom) PhotonNetwork.LoadLevel(sceneName);
            else SceneManager.LoadScene(sceneName);
        }, fadeOut);
    }

    public static void FadeOutThen(Action onCovered, float fadeOut = DefaultFadeOut)
    {
        Ensure().RunFadeOut(onCovered, fadeOut);
    }

    public void FadeIn(float dur = DefaultFadeIn)
    {
        if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(FadeRoutine(_group.alpha, 0f, dur, null));
    }

    void RunFadeOut(Action onCovered, float dur)
    {
        if (_running != null) StopCoroutine(_running);
        _group.blocksRaycasts = true;
        _running = StartCoroutine(FadeRoutine(_group.alpha, 1f, dur, onCovered));
    }

    IEnumerator FadeRoutine(float from, float to, float dur, Action onDone)
    {
        if (_spinner != null) _spinner.gameObject.SetActive(to > 0.5f);
        _group.blocksRaycasts = to > 0.5f;
        float t = 0f;
        dur = Mathf.Max(0.01f, dur);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / dur);
            _group.alpha = Mathf.Lerp(from, to, k);
            yield return null;
        }
        _group.alpha = to;
        _group.blocksRaycasts = to > 0.5f;
        _running = null;
        onDone?.Invoke();
    }
}
