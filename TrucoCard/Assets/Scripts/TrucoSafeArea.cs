using UnityEngine;

[DisallowMultipleComponent]
public sealed class TrucoSafeArea : MonoBehaviour
{
    Rect _last; int _width, _height;
    void OnEnable() => Apply();
    void Update() { if (_last != Screen.safeArea || _width != Screen.width || _height != Screen.height) Apply(); }
    void Apply()
    {
        if (Screen.width <= 0 || Screen.height <= 0) return;
        _last = Screen.safeArea; _width = Screen.width; _height = Screen.height;
        var rt = (RectTransform)transform;
        rt.anchorMin = new Vector2(_last.xMin / _width, _last.yMin / _height);
        rt.anchorMax = new Vector2(_last.xMax / _width, _last.yMax / _height);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
