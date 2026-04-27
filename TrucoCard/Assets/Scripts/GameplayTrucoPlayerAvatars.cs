using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Maps Truco players (PUN order) to Player 1 / Player 2 portrait <see cref="Image"/>s.</summary>
public class GameplayTrucoPlayerAvatars : MonoBehaviourPunCallbacks
{
    [SerializeField] private string _player1IconName = "Player 1 Icon";
    [SerializeField] private string _player2IconName = "Player 2 Icon";

    void Start()
    {
        if (SpectatorContext.IsSpectator) return;
        if (TrucoAvatarRepository.Instance == null)
        {
            var g = new GameObject("TrucoAvatarRepository");
            g.AddComponent<TrucoAvatarRepository>();
        }
        TrucoAvatarRepository.Instance.EnsureCache();
        Refresh();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer) => Refresh();

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps) => Refresh();

    void Refresh()
    {
        var players = PhotonPlayerHelper.GetTrucoPlayers();
        if (players == null || players.Count == 0) return;

        var img1 = FindImage(_player1IconName);
        var img2 = FindImage(_player2IconName);
        if (img1 != null && players.Count > 0)
            img1.sprite = SpriteFor(players[0]);
        if (img2 != null && players.Count > 1)
            img2.sprite = SpriteFor(players[1]);
    }

    static Image FindImage(string goName)
    {
        var go = GameObject.Find(goName);
        return go != null ? go.GetComponent<Image>() : null;
    }

    Sprite SpriteFor(Player p)
    {
        int idx = PhotonPlayerHelper.ResolveAvatarIndexForDisplay(p);
        var s = TrucoAvatarRepository.Instance != null ? TrucoAvatarRepository.Instance.GetPortrait(idx) : null;
        return s;
    }
}
