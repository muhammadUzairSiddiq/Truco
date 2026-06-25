using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OneVsOneCreateRoomPanel : MonoBehaviour
{
    [SerializeField] private TMP_InputField _nameField;
    [SerializeField] private Toggle _isPublicToggle;
    /// <summary>When set (two-option UI), paired with <see cref="_isPublicToggle"/> — only one is on; private = code required.</summary>
    [SerializeField] private Toggle _isPrivateToggle;
    [SerializeField] private GameObject _passwordGroup;
    [SerializeField] private TMP_InputField _passwordField;
    [SerializeField] private Toggle _fee5;
    [SerializeField] private Toggle _fee10;
    [SerializeField] private Toggle _fee15;
    [SerializeField] private Button _confirm;
    [SerializeField] private Button _cancel;
    [SerializeField] private TMP_Text _prizePreview;
    bool _feeTogglesWired;
    System.Action _onCancel;

    /// <summary>Optional: a label that shows the prize the winner will get for the selected entry fee.</summary>
    public void RefreshLocalizedLabels()
    {
        RefreshPrizePreview();
        TrucoRuntimeUiBuilders.SetFeeToggleAmountLabel(_fee5, 5);
        TrucoRuntimeUiBuilders.SetFeeToggleAmountLabel(_fee10, 10);
        TrucoRuntimeUiBuilders.SetFeeToggleAmountLabel(_fee15, 15);
        SetLabelInChild("FeeLabel", TrucoTextosClient.EntradaMonedas);
        SetLabelInChild("CodeLabel", TrucoTextosClient.CodigoSala4);
        SetAccessToggleLabel(_isPublicToggle, TrucoTextosClient.Publica);
        SetAccessToggleLabel(_isPrivateToggle, TrucoTextosClient.Privada);
        SetButtonLabel(_confirm, TrucoTextosClient.CrearSala);
        SetButtonLabel(_cancel, TrucoTextosClient.Volver);
        if (_nameField != null && _nameField.placeholder is TMP_Text ph)
            ph.text = TrucoTextosClient.NombreSala;
        if (_passwordField != null && _passwordField.placeholder is TMP_Text pph)
            pph.text = TrucoLocalization.T(TrucoLocalization.Key.CodigoMinPlaceholder);
        if (_prizePreview != null)
            _prizePreview.color = TrucoUiTheme.EntryPrizeAccent;
    }

    void SetLabelInChild(string goName, string text)
    {
        foreach (var ch in GetComponentsInChildren<Transform>(true))
        {
            if (ch.name != goName) continue;
            var tmp = ch.GetComponent<TMP_Text>();
            if (tmp != null) tmp.text = text;
            return;
        }
    }

    static void SetAccessToggleLabel(Toggle tgl, string label)
    {
        if (tgl == null) return;
        var row = tgl.transform.parent;
        if (row == null) return;
        for (int i = 0; i < row.childCount; i++)
        {
            var ch = row.GetChild(i);
            if (ch == tgl.transform) continue;
            var tmp = ch.GetComponent<TMP_Text>();
            if (tmp != null) { tmp.text = label; return; }
        }
    }

    static void SetButtonLabel(Button btn, string text)
    {
        if (btn == null) return;
        var tmp = btn.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.text = text;
    }

    public void BindPrizePreview(TMP_Text prizeLabel)
    {
        _prizePreview = prizeLabel;
        RefreshPrizePreview();
    }

    void RefreshPrizePreview()
    {
        if (_prizePreview == null) return;
        int entry = GetEntryFee();
        int prize = Player1v1MatchExtensions.ComputeOneVsOnePrize(entry);
        _prizePreview.text = Player1v1MatchExtensions.FormatEntryPrizeLabel(entry);
    }

    public void SetRuntimeBinding(
        TMP_InputField name,
        Toggle isPublic,
        GameObject passwordGroup,
        TMP_InputField password,
        Toggle fee5, Toggle fee10, Toggle fee15,
        Button confirm, Button cancel,
        Toggle privateAccessToggle = null)
    {
        _nameField = name;
        _isPublicToggle = isPublic;
        _isPrivateToggle = privateAccessToggle;
        _passwordGroup = passwordGroup;
        _passwordField = password;
        _fee5 = fee5;
        _fee10 = fee10;
        _fee15 = fee15;
        _confirm = confirm;
        _cancel = cancel;
    }

    public void Open(System.Action<OneVsOneCreateRoomPanel> onConfirm, System.Action onCancel = null)
    {
        _onCancel = onCancel;
        gameObject.SetActive(true);
        if (_isPrivateToggle != null)
        {
            if (_isPublicToggle != null) _isPublicToggle.SetIsOnWithoutNotify(true);
            _isPrivateToggle.SetIsOnWithoutNotify(false);
        }
        else if (_isPublicToggle != null) _isPublicToggle.isOn = true;
        if (_nameField != null)
        {
            string prefix = TrucoLocalization.IsEnglish ? "Room " : "Sala ";
            string fallback = TrucoLocalization.IsEnglish ? "player" : "jugador";
            _nameField.text = prefix + (ApiController.GetSessionUser?.Data?.username ?? fallback);
        }
        if (_passwordField != null) _passwordField.text = string.Empty;
        SyncPasswordGroup();
        if (_fee5 != null) { _fee5.isOn = true; }
        if (_fee10 != null) _fee10.isOn = false;
        if (_fee15 != null) _fee15.isOn = false;

        if (_isPrivateToggle != null)
        {
            if (_isPublicToggle != null)
            {
                _isPublicToggle.onValueChanged.RemoveAllListeners();
                _isPrivateToggle.onValueChanged.RemoveAllListeners();
                _isPublicToggle.onValueChanged.AddListener(v =>
                {
                    if (v) _isPrivateToggle.SetIsOnWithoutNotify(false);
                    SyncPasswordGroup();
                });
                _isPrivateToggle.onValueChanged.AddListener(v =>
                {
                    if (v) _isPublicToggle.SetIsOnWithoutNotify(false);
                    SyncPasswordGroup();
                });
            }
        }
        else if (_isPublicToggle != null)
        {
            _isPublicToggle.onValueChanged.RemoveAllListeners();
            _isPublicToggle.onValueChanged.AddListener(_ => SyncPasswordGroup());
        }

        if (!_feeTogglesWired) { _feeTogglesWired = true; WireFeeTogglesForExclusive(); }
        RefreshLocalizedLabels();

        if (_confirm != null)
        {
            _confirm.onClick.RemoveAllListeners();
            _confirm.onClick.AddListener(() => onConfirm?.Invoke(this));
        }
        if (_cancel != null)
        {
            _cancel.onClick.RemoveAllListeners();
            _cancel.onClick.AddListener(() => InvokeCancel());
        }
    }

    public void TriggerCancelFromChrome() => InvokeCancel();

    void InvokeCancel()
    {
        var cb = _onCancel;
        gameObject.SetActive(false);
        cb?.Invoke();
    }

    public void Close() => gameObject.SetActive(false);

    void SyncPasswordGroup()
    {
        if (_passwordGroup == null) return;
        bool needCode;
        if (_isPrivateToggle != null)
            needCode = _isPrivateToggle.isOn;
        else
            needCode = _isPublicToggle != null && !_isPublicToggle.isOn;
        _passwordGroup.SetActive(needCode);
    }

    public int GetEntryFee()
    {
        if (_fee15 != null && _fee15.isOn) return 15;
        if (_fee10 != null && _fee10.isOn) return 10;
        return 5;
    }

    public string GetRoomName()
    {
        if (_nameField == null) return TrucoLocalization.IsEnglish ? "Room" : "Sala";
        return _nameField.text.Trim();
    }

    public bool IsPublic()
    {
        if (_isPrivateToggle != null) return _isPublicToggle != null && _isPublicToggle.isOn;
        return _isPublicToggle == null || _isPublicToggle.isOn;
    }

    public string GetPassword() => _passwordField != null ? _passwordField.text : string.Empty;

    void WireFeeTogglesForExclusive()
    {
        if (_fee5 != null)
            _fee5.onValueChanged.AddListener(v => { if (v) { if (_fee10) _fee10.isOn = false; if (_fee15) _fee15.isOn = false; } RefreshPrizePreview(); });
        if (_fee10 != null)
            _fee10.onValueChanged.AddListener(v => { if (v) { if (_fee5) _fee5.isOn = false; if (_fee15) _fee15.isOn = false; } RefreshPrizePreview(); });
        if (_fee15 != null)
            _fee15.onValueChanged.AddListener(v => { if (v) { if (_fee5) _fee5.isOn = false; if (_fee10) _fee10.isOn = false; } RefreshPrizePreview(); });
    }
}
