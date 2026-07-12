using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OneVsOneRoomRowView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _metaText;
    [SerializeField] private TextMeshProUGUI _playersText;
    [SerializeField] private Button _joinButton;
    [SerializeField] private TextMeshProUGUI _joinButtonLabel;
    [Tooltip("Optional: red ‘Expirado’ line centered under the join button (runtime builders wire this).")]
    [SerializeField] private TextMeshProUGUI _expiredBelowJoin;
    [SerializeField] private GameObject _codeStripRoot;
    [SerializeField] private TextMeshProUGUI _codeCaption;
    [SerializeField] private TextMeshProUGUI _hostCodeText;
    [SerializeField] private TMP_InputField _joinerCodeField;

    private Player1v1Match _data;
    private System.Action<Player1v1Match, OneVsOneRoomRowView> _onJoin;
    private string _cachedHostCode;

    public Player1v1Match BoundMatch => _data;

    /// <summary>Fallback when the row has no in-bar code strip (e.g. auto lobby factory).</summary>
    public void SetRuntimeBinding(
        TextMeshProUGUI title,
        TextMeshProUGUI meta,
        TextMeshProUGUI players,
        Button join,
        TextMeshProUGUI joinLabel = null)
    {
        SetRuntimeBinding(title, meta, players, join, joinLabel, null, null, null, null, null);
    }

    public void SetRuntimeBinding(
        TextMeshProUGUI title,
        TextMeshProUGUI meta,
        TextMeshProUGUI players,
        Button join,
        TextMeshProUGUI joinLabel,
        GameObject codeStripRoot,
        TextMeshProUGUI codeCaption,
        TextMeshProUGUI hostCode,
        TMP_InputField joinerCode,
        TextMeshProUGUI expiredBelowJoin = null)
    {
        _titleText = title;
        _metaText = meta;
        _playersText = players;
        _joinButton = join;
        _joinButtonLabel = joinLabel;
        _codeStripRoot = codeStripRoot;
        _codeCaption = codeCaption;
        _hostCodeText = hostCode;
        _joinerCodeField = joinerCode;
        _expiredBelowJoin = expiredBelowJoin;
    }

    /// <param name="hostOnlyPrivateCode">Only for your private room as host (displayed in-bar, not in meta).</param>
    public void Bind(
        Player1v1Match m,
        System.Action<Player1v1Match, OneVsOneRoomRowView> onJoin,
        bool canJoin,
        string joinButtonOverride = null,
        string hostOnlyPrivateCode = null)
    {
        _data = m;
        _onJoin = onJoin;
        _cachedHostCode = hostOnlyPrivateCode;
        if (m == null) return;

        if (_titleText != null)
            _titleText.text = string.IsNullOrEmpty(m.name) ? m._id : m.name;

        int cost = m.GetEntryStake();
        int prize = m.GetPrizeForDisplay();
        string typeStr = m.GetTypeLabel();
        if (_metaText != null)
            _metaText.text = $"{typeStr}  ·  {TrucoTextosClient.EntradaAbrev}: {cost}  ·  {TrucoTextosClient.PremioAbrev}: {prize}";

        int count = m.GetTrucoPlayerCountForUi();
        if (_playersText != null)
            _playersText.text = string.Format(TrucoTextosClient.JugadoresEnSala, count, 2);

        bool priv = m.IsPrivate();
        bool host = m.IsCurrentUserHostOfRoom();
        bool showStrip = priv;
        if (_codeStripRoot != null)
        {
            if (codeStripLayout == null) codeStripLayout = _codeStripRoot.GetComponent<LayoutElement>();
            _codeStripRoot.SetActive(showStrip);
            if (codeStripLayout != null)
            {
                codeStripLayout.minWidth = showStrip ? 200f : 0f;
                codeStripLayout.preferredWidth = showStrip ? 300f : 0f;
            }
        }
        if (_codeCaption != null)
        {
            _codeCaption.gameObject.SetActive(showStrip);
            if (host && showStrip) _codeCaption.text = TrucoTextosClient.TuCodigoSala;
            else if (!host && showStrip) _codeCaption.text = TrucoTextosClient.CodigoParaUnir;
        }
        if (_hostCodeText != null)
        {
            bool showH = showStrip && host && !string.IsNullOrEmpty(hostOnlyPrivateCode);
            _hostCodeText.gameObject.SetActive(showH);
            if (showH) _hostCodeText.text = hostOnlyPrivateCode;
        }
        if (_joinerCodeField != null)
        {
            var root = _joinerCodeField.transform.parent != null ? _joinerCodeField.transform.parent.gameObject : null;
            bool showJ = showStrip && !host && canJoin;
            if (root != null) root.SetActive(showJ);
            _joinerCodeField.gameObject.SetActive(showJ);
            if (showJ) _joinerCodeField.text = string.Empty;
        }

        if (_joinButton != null)
        {
            _joinButton.onClick.RemoveAllListeners();
            if (canJoin) _joinButton.onClick.AddListener(() => _onJoin?.Invoke(_data, this));
            _joinButton.interactable = canJoin;
        }

        bool stale = m.IsStaleFullVersusPhoton() || m.IsHostAbandonedVersusPhoton();
        if (_joinButtonLabel != null)
        {
            if (stale)
                _joinButtonLabel.text = TrucoTextosClient.Entrar;
            else
                _joinButtonLabel.text = string.IsNullOrEmpty(joinButtonOverride)
                    ? TrucoTextosClient.Entrar
                    : joinButtonOverride;
            _joinButtonLabel.enableWordWrapping = false;
            _joinButtonLabel.overflowMode = TextOverflowModes.Ellipsis;
        }
        if (_expiredBelowJoin != null)
        {
            _expiredBelowJoin.gameObject.SetActive(stale);
            if (stale)
            {
                _expiredBelowJoin.text = m.IsHostAbandonedVersusPhoton()
                    ? TrucoTextosClient.AnfitrionSalioSala
                    : TrucoTextosClient.SalaExpiradaEtiqueta;
                _expiredBelowJoin.color = new Color(0.82f, 0.14f, 0.1f, 1f);
                _expiredBelowJoin.alignment = TextAlignmentOptions.Center;
            }
        }
    }

    LayoutElement codeStripLayout;
    void Awake()
    {
        if (_codeStripRoot != null) codeStripLayout = _codeStripRoot.GetComponent<LayoutElement>();
        TryBindExpiredFromHierarchy();
    }

    void TryBindExpiredFromHierarchy()
    {
        if (_expiredBelowJoin != null || _joinButton == null) return;
        var parent = _joinButton.transform.parent;
        if (parent == null) return;
        var hint = parent.Find("ExpiredHint");
        if (hint != null) _expiredBelowJoin = hint.GetComponent<TextMeshProUGUI>();
    }

    public string GetJoinerCodeText() => _joinerCodeField != null ? _joinerCodeField.text : string.Empty;

    public void SetJoinInteractable(bool interactable)
    {
        if (_joinButton != null)
            _joinButton.interactable = interactable;
    }

    /// <summary>Tras <see cref="OneVsOnePhotonFlow.OnRoomListUpdate"/> para actualizar 0/2–2/2 sin volver a pedir el API.</summary>
    public void RefreshFromLivePhoton()
    {
        if (_data == null || _onJoin == null) return;
        bool can = _data.CanClickJoinOnRoom();
        string joinTxt = null;
        if (!can)
        {
            if (_data.IsStaleFullVersusPhoton()) joinTxt = null;
            else if (_data.IsHostAbandonedVersusPhoton()) joinTxt = null;
            else if (_data.IsCurrentUserHostOfRoom()) joinTxt = TrucoTextosClient.TuSalaEsperando;
            else if (_data.GetTrucoPlayerCount() >= 2) joinTxt = TrucoTextosClient.SalaLlena;
        }
        string hostCode = _cachedHostCode;
        if (_data.IsPrivate() && _data.IsCurrentUserHostOfRoom())
        {
            if (!string.IsNullOrEmpty(_data.joinCode)) hostCode = _data.joinCode.Trim();
            if (string.IsNullOrEmpty(hostCode)) hostCode = OneVsOnePrivateRoomCode.TryGetRememberedForMatch(_data._id);
        }
        Bind(_data, _onJoin, can, joinTxt, hostCode);
    }
}
