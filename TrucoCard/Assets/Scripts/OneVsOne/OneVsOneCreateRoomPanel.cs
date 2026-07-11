using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OneVsOneCreateRoomPanel : MonoBehaviour
{
    public static readonly int[] EntryFeeOptions =
    {
        5, 10, 15, 20, 25, 30, 50, 75, 100, 150, 250, 500, 1000, 2500, 5000
    };

    [SerializeField] private TMP_InputField _nameField;
    [SerializeField] private Toggle _isPublicToggle;
    [SerializeField] private Toggle _isPrivateToggle;
    [SerializeField] private GameObject _passwordGroup;
    [SerializeField] private TMP_InputField _passwordField;
    [SerializeField] private Toggle _fee5;
    [SerializeField] private Toggle _fee10;
    [SerializeField] private Toggle _fee15;
    [SerializeField] private Toggle _withFlorToggle;
    [SerializeField] private Button _confirm;
    [SerializeField] private Button _cancel;
    [SerializeField] private TMP_Text _prizePreview;

    readonly List<Toggle> _feeToggles = new List<Toggle>();
    bool _feeTogglesWired;
    System.Action _onCancel;

    public void RefreshLocalizedLabels()
    {
        RefreshPrizePreview();
        foreach (var t in _feeToggles)
        {
            if (t == null) continue;
            int stake = ParseFeeFromToggle(t);
            TrucoRuntimeUiBuilders.SetFeeToggleAmountLabel(t, stake);
        }
        SetLabelInChild("FeeLabel", TrucoTextosClient.EntradaMonedas);
        SetLabelInChild("CodeLabel", TrucoTextosClient.CodigoSala4);
        SetPillLabel(_isPublicToggle, TrucoTextosClient.Publica);
        SetPillLabel(_isPrivateToggle, TrucoTextosClient.Privada);
        SetPillLabel(_withFlorToggle, TrucoTextosClient.ConFlor);
        SetFlorSegmentLabels();
        SetButtonLabel(_confirm, TrucoTextosClient.CrearSala);
        SetButtonLabel(_cancel, TrucoTextosClient.Volver);
        if (_nameField != null && _nameField.placeholder is TMP_Text ph)
            ph.text = TrucoTextosClient.NombreSala;
        if (_passwordField != null && _passwordField.placeholder is TMP_Text pph)
            pph.text = TrucoLocalization.T(TrucoLocalization.Key.CodigoMinPlaceholder);
        if (_prizePreview != null)
            _prizePreview.color = Color.white;
    }

    static int ParseFeeFromToggle(Toggle t)
    {
        if (t == null) return 5;
        var tmp = t.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null && int.TryParse(tmp.text, out int v)) return v;
        if (int.TryParse(t.name.Replace("Fee_", ""), out v)) return v;
        return 5;
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

    static void SetPillLabel(Toggle tgl, string label)
    {
        if (tgl == null) return;
        var tmp = tgl.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.text = label;
    }

    void SetFlorSegmentLabels()
    {
        if (_withFlorToggle == null) return;
        var row = _withFlorToggle.transform.parent;
        if (row == null) return;
        foreach (var t in row.GetComponentsInChildren<Toggle>(true))
        {
            if (t == _withFlorToggle) SetPillLabel(t, TrucoTextosClient.ConFlor);
            else if (t.name.Contains("SinFlor") || t.name.Contains("FlorB"))
                SetPillLabel(t, TrucoTextosClient.SinFlor);
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
        _prizePreview.text = Player1v1MatchExtensions.FormatEntryPrizeLabel(entry);
    }

    public void SetRuntimeBinding(
        TMP_InputField name,
        Toggle isPublic,
        GameObject passwordGroup,
        TMP_InputField password,
        Toggle fee5, Toggle fee10, Toggle fee15,
        Button confirm, Button cancel,
        Toggle privateAccessToggle = null,
        IList<Toggle> extraFeeToggles = null)
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
        _feeToggles.Clear();
        if (fee5 != null) _feeToggles.Add(fee5);
        if (fee10 != null) _feeToggles.Add(fee10);
        if (fee15 != null) _feeToggles.Add(fee15);
        if (extraFeeToggles != null)
            foreach (var t in extraFeeToggles)
                if (t != null && !_feeToggles.Contains(t)) _feeToggles.Add(t);
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
        SelectFeeToggle(5);

        if (_isPrivateToggle != null && _isPublicToggle != null)
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
        else if (_isPublicToggle != null)
        {
            _isPublicToggle.onValueChanged.RemoveAllListeners();
            _isPublicToggle.onValueChanged.AddListener(_ => SyncPasswordGroup());
        }

        if (!_feeTogglesWired) { _feeTogglesWired = true; WireFeeTogglesForExclusive(); }
        RefreshLocalizedLabels();

        if (_confirm != null)
        {
            _confirm.interactable = true;
            _confirm.onClick.RemoveAllListeners();
            _confirm.onClick.AddListener(() =>
            {
                if (_confirm != null) _confirm.interactable = false;
                onConfirm?.Invoke(this);
            });
        }
        if (_cancel != null)
        {
            _cancel.onClick.RemoveAllListeners();
            _cancel.onClick.AddListener(() => InvokeCancel());
        }
    }

    void SelectFeeToggle(int amount)
    {
        foreach (var t in _feeToggles)
        {
            if (t == null) continue;
            bool on = ParseFeeFromToggle(t) == amount;
            t.SetIsOnWithoutNotify(on);
            TrucoFormUiPolish.RefreshPillVisual(t, on);
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

    public void SetConfirmInteractable(bool interactable)
    {
        if (_confirm != null) _confirm.interactable = interactable;
    }

    void SyncPasswordGroup()
    {
        if (_passwordGroup == null) return;
        bool needCode = _isPrivateToggle != null ? _isPrivateToggle.isOn : _isPublicToggle != null && !_isPublicToggle.isOn;
        _passwordGroup.SetActive(needCode);
    }

    public int GetEntryFee()
    {
        foreach (var t in _feeToggles)
            if (t != null && t.isOn) return ParseFeeFromToggle(t);
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

    public bool WithFlor() => _withFlorToggle == null || _withFlorToggle.isOn;

    public void BindFlorToggle(Toggle florToggle)
    {
        _withFlorToggle = florToggle;
        if (_withFlorToggle != null) _withFlorToggle.SetIsOnWithoutNotify(true);
    }

    void WireFeeTogglesForExclusive()
    {
        foreach (var t in _feeToggles)
        {
            if (t == null) continue;
            t.onValueChanged.AddListener(v =>
            {
                if (!v) return;
                foreach (var other in _feeToggles)
                {
                    if (other == null || other == t) continue;
                    other.SetIsOnWithoutNotify(false);
                    TrucoFormUiPolish.RefreshPillVisual(other, false);
                }
                TrucoFormUiPolish.RefreshPillVisual(t, true);
                RefreshPrizePreview();
            });
        }
    }
}
