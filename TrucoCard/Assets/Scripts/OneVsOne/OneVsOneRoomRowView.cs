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
    [SerializeField] private GameObject _codeStripRoot;
    [SerializeField] private TextMeshProUGUI _codeCaption;
    [SerializeField] private TextMeshProUGUI _hostCodeText;
    [SerializeField] private TMP_InputField _joinerCodeField;

    private Player1v1Match _data;
    private System.Action<Player1v1Match, OneVsOneRoomRowView> _onJoin;

    /// <summary>Fallback when the row has no in-bar code strip (e.g. auto lobby factory).</summary>
    public void SetRuntimeBinding(
        TextMeshProUGUI title,
        TextMeshProUGUI meta,
        TextMeshProUGUI players,
        Button join,
        TextMeshProUGUI joinLabel = null)
    {
        SetRuntimeBinding(title, meta, players, join, joinLabel, null, null, null, null);
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
        TMP_InputField joinerCode)
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
        if (m == null) return;

        if (_titleText != null)
            _titleText.text = string.IsNullOrEmpty(m.name) ? m._id : m.name;

        int cost = m.GetEntryStake();
        string typeStr = m.GetTypeLabel();
        if (_metaText != null)
            _metaText.text = $"{typeStr}  ·  {TrucoTextosClient.EntradaAbrev}: {cost}";

        int count = m.GetTrucoPlayerCount();
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
        if (_joinButtonLabel != null)
            _joinButtonLabel.text = string.IsNullOrEmpty(joinButtonOverride)
                ? TrucoTextosClient.Entrar
                : joinButtonOverride;
    }

    LayoutElement codeStripLayout;
    void Awake()
    {
        if (_codeStripRoot != null) codeStripLayout = _codeStripRoot.GetComponent<LayoutElement>();
    }

    public string GetJoinerCodeText() => _joinerCodeField != null ? _joinerCodeField.text : string.Empty;
}
