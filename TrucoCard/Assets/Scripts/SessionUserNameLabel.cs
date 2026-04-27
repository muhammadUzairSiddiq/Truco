using TMPro;
using UnityEngine;

/// <summary>Displays the logged-in user name on a TMP label. Do not use <see cref="CoinGetter"/> on the same object.</summary>
[RequireComponent(typeof(TMP_Text))]
public class SessionUserNameLabel : MonoBehaviour
{
    TMP_Text _label;
    string _last;

    void Awake() => _label = GetComponent<TMP_Text>();

    void OnEnable()
    {
        Apply();
        CancelInvoke();
        InvokeRepeating(nameof(Apply), 0.25f, 1.5f);
    }

    void OnDisable() => CancelInvoke();

    public void Apply()
    {
        if (_label == null) return;
        var u = ApiController.GetSessionUser != null ? ApiController.GetSessionUser.Data : null;
        if (u == null || string.IsNullOrEmpty(u.username)) return;
        if (u.username == _last) return;
        _last = u.username;
        _label.text = u.username;
    }
}
