using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Premium tactile button feedback: scale punch on press, subtle lift on hover. Auto-attached at runtime.</summary>
[DisallowMultipleComponent]
public class TrucoButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    Vector3 _baseScale;
    Coroutine _anim;
    bool _pressed;

    const float PressScale = 0.92f;
    const float HoverScale = 1.04f;
    const float Speed = 14f;

    void Awake() { _baseScale = transform.localScale; }
    void OnEnable() { transform.localScale = _baseScale; }

    public void OnPointerDown(PointerEventData e) { _pressed = true; AnimateTo(_baseScale * PressScale); }
    public void OnPointerUp(PointerEventData e) { _pressed = false; AnimateTo(_baseScale); }
    public void OnPointerEnter(PointerEventData e) { if (!_pressed) AnimateTo(_baseScale * HoverScale); }
    public void OnPointerExit(PointerEventData e) { if (!_pressed) AnimateTo(_baseScale); }

    void AnimateTo(Vector3 target)
    {
        if (!gameObject.activeInHierarchy) { transform.localScale = target; return; }
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(Lerp(target));
    }

    System.Collections.IEnumerator Lerp(Vector3 target)
    {
        while ((transform.localScale - target).sqrMagnitude > 0.00001f)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, target, Speed * Time.unscaledDeltaTime);
            yield return null;
        }
        transform.localScale = target;
        _anim = null;
    }
}
