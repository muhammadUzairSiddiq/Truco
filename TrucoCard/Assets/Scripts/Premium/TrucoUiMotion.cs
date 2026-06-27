using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Premium UI micro-interactions: panel pop-in, fade-in, and bulk button feedback attachment.</summary>
public static class TrucoUiMotion
{
    /// <summary>Fade-only reveal — safe for full-screen stretch panels (never changes localScale).</summary>
    public static void FadeIn(Transform target, float duration = 0.28f)
    {
        if (target == null) return;
        var cg = EnsureCanvasGroup(target.gameObject);
        TrucoMotionRunner.Instance.StartCoroutine(FadeInRoutine(cg, duration));
    }

    static IEnumerator FadeInRoutine(CanvasGroup cg, float duration)
    {
        if (cg == null) yield break;
        cg.alpha = 0f;
        float t = 0f;
        duration = Mathf.Max(0.05f, duration);
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(t / duration);
            yield return null;
        }
        cg.alpha = 1f;
    }

    /// <summary>Scale pop-in for small cards only — not full-screen roots.</summary>
    public static void PopIn(Transform target, float duration = 0.32f, float startScale = 0.82f)
    {
        if (target == null) return;
        var rt = target as RectTransform;
        if (rt != null && rt.anchorMin != rt.anchorMax)
        {
            FadeIn(target, duration);
            return;
        }
        var cg = EnsureCanvasGroup(target.gameObject);
        TrucoMotionRunner.Instance.StartCoroutine(PopInRoutine(target, cg, duration, startScale));
    }

    static IEnumerator PopInRoutine(Transform target, CanvasGroup cg, float duration, float startScale)
    {
        if (target == null) yield break;
        Vector3 final = target.localScale == Vector3.zero ? Vector3.one : target.localScale;
        if (cg != null) cg.alpha = 0f;
        float t = 0f;
        duration = Mathf.Max(0.05f, duration);
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float eased = Mathf.Lerp(startScale, 1f, Mathf.SmoothStep(0f, 1f, k));
            target.localScale = final * eased;
            if (cg != null) cg.alpha = Mathf.Clamp01(k * 1.2f);
            yield return null;
        }
        target.localScale = final;
        if (cg != null) cg.alpha = 1f;
    }

    /// <summary>Gentle pulse loop (e.g. "your turn" highlight). Returns the running coroutine for stopping.</summary>
    public static Coroutine Pulse(Transform target, float amplitude = 0.06f, float speed = 3.2f)
    {
        if (target == null) return null;
        return TrucoMotionRunner.Instance.StartCoroutine(PulseRoutine(target, amplitude, speed));
    }

    static IEnumerator PulseRoutine(Transform target, float amplitude, float speed)
    {
        Vector3 baseScale = target.localScale;
        while (target != null)
        {
            float s = 1f + Mathf.Sin(Time.unscaledTime * speed) * amplitude;
            target.localScale = baseScale * s;
            yield return null;
        }
    }

    /// <summary>Attaches tactile feedback to every Button under a root (idempotent).</summary>
    public static void AttachButtonFeedback(GameObject root)
    {
        if (root == null) return;
        foreach (var btn in root.GetComponentsInChildren<Button>(true))
        {
            if (btn == null) continue;
            if (btn.GetComponent<TrucoButtonFeedback>() == null)
                btn.gameObject.AddComponent<TrucoButtonFeedback>();
        }
    }

    /// <summary>Attaches tactile feedback to all Buttons currently loaded in the active scene.</summary>
    public static void AttachButtonFeedbackSceneWide()
    {
        foreach (var btn in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (btn == null) continue;
            if (btn.GetComponent<TrucoButtonFeedback>() == null)
                btn.gameObject.AddComponent<TrucoButtonFeedback>();
        }
    }

    static CanvasGroup EnsureCanvasGroup(GameObject go)
    {
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float xm = x - 1f;
        return 1f + c3 * xm * xm * xm + c1 * xm * xm;
    }
}
