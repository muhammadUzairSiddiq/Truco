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

    private Player1v1Match _data;
    private System.Action<Player1v1Match> _onJoin;

    /// <summary>Runtime / factory wiring.</summary>
    public void SetRuntimeBinding(TextMeshProUGUI title, TextMeshProUGUI meta, TextMeshProUGUI players, Button join, TextMeshProUGUI joinLabel = null)
    {
        _titleText = title;
        _metaText = meta;
        _playersText = players;
        _joinButton = join;
        _joinButtonLabel = joinLabel;
    }

    public void Bind(Player1v1Match m, System.Action<Player1v1Match> onJoin, bool canJoin, string joinButtonOverride = null)
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

        if (_joinButton != null)
        {
            _joinButton.onClick.RemoveAllListeners();
            if (canJoin) _joinButton.onClick.AddListener(() => _onJoin?.Invoke(_data));
            _joinButton.interactable = canJoin;
        }
        if (_joinButtonLabel != null)
            _joinButtonLabel.text = string.IsNullOrEmpty(joinButtonOverride)
                ? TrucoTextosClient.Entrar
                : joinButtonOverride;
    }
}
