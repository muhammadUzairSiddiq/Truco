using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OneVsOneCreateRoomPanel : MonoBehaviour
{
    [SerializeField] private TMP_InputField _nameField;
    [SerializeField] private Toggle _isPublicToggle;
    [SerializeField] private GameObject _passwordGroup;
    [SerializeField] private TMP_InputField _passwordField;
    [SerializeField] private Toggle _fee10;
    [SerializeField] private Toggle _fee100;
    [SerializeField] private Toggle _fee500;
    [SerializeField] private Button _confirm;
    [SerializeField] private Button _cancel;
    bool _feeTogglesWired;

    public void SetRuntimeBinding(
        TMP_InputField name,
        Toggle isPublic,
        GameObject passwordGroup,
        TMP_InputField password,
        Toggle fee10, Toggle fee100, Toggle fee500,
        Button confirm, Button cancel)
    {
        _nameField = name;
        _isPublicToggle = isPublic;
        _passwordGroup = passwordGroup;
        _passwordField = password;
        _fee10 = fee10;
        _fee100 = fee100;
        _fee500 = fee500;
        _confirm = confirm;
        _cancel = cancel;
    }

    public void Open(System.Action<OneVsOneCreateRoomPanel> onConfirm, System.Action onCancel = null)
    {
        gameObject.SetActive(true);
        if (_isPublicToggle != null) _isPublicToggle.isOn = true;
        if (_nameField != null) _nameField.text = "Sala " + (ApiController.GetSessionUser?.Data?.username ?? "jugador");
        SyncPasswordGroup();
        if (_fee10 != null) { _fee10.isOn = true; }
        if (_fee100 != null) _fee100.isOn = false;
        if (_fee500 != null) _fee500.isOn = false;

        if (_isPublicToggle != null)
        {
            _isPublicToggle.onValueChanged.RemoveAllListeners();
            _isPublicToggle.onValueChanged.AddListener(_ => SyncPasswordGroup());
        }

        if (!_feeTogglesWired) { _feeTogglesWired = true; WireFeeTogglesForExclusive(); }

        if (_confirm != null)
        {
            _confirm.onClick.RemoveAllListeners();
            _confirm.onClick.AddListener(() => onConfirm?.Invoke(this));
        }
        if (_cancel != null)
        {
            _cancel.onClick.RemoveAllListeners();
            _cancel.onClick.AddListener(() =>
            {
                gameObject.SetActive(false);
                onCancel?.Invoke();
            });
        }
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    void SyncPasswordGroup()
    {
        if (_passwordGroup == null) return;
        bool pub = _isPublicToggle == null || _isPublicToggle.isOn;
        _passwordGroup.SetActive(!pub);
    }

    public int GetEntryFee()
    {
        if (_fee500 != null && _fee500.isOn) return 500;
        if (_fee100 != null && _fee100.isOn) return 100;
        return 10;
    }

    public string GetRoomName()
    {
        return _nameField != null ? _nameField.text.Trim() : "Sala";
    }

    public bool IsPublic() => _isPublicToggle == null || _isPublicToggle.isOn;

    public string GetPassword() => _passwordField != null ? _passwordField.text : string.Empty;

    void WireFeeTogglesForExclusive()
    {
        if (_fee10 != null)
            _fee10.onValueChanged.AddListener(v => { if (v) { if (_fee100) _fee100.isOn = false; if (_fee500) _fee500.isOn = false; } });
        if (_fee100 != null)
            _fee100.onValueChanged.AddListener(v => { if (v) { if (_fee10) _fee10.isOn = false; if (_fee500) _fee500.isOn = false; } });
        if (_fee500 != null)
            _fee500.onValueChanged.AddListener(v => { if (v) { if (_fee10) _fee10.isOn = false; if (_fee100) _fee100.isOn = false; } });
    }
}
