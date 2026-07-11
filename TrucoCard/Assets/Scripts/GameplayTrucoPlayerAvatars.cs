using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Maps seats to portraits: Player 1 Icon = local (bottom / my score), Player 2 Icon = opponent (top).
/// Must stay aligned with <see cref="GameManager.myPlayerScoreHandler"/> / otherPlayerScoreHandler —
/// never map by ActorNumber order alone (that swapped faces when local was actor 2).
/// </summary>
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
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null) return;
        var players = PhotonPlayerHelper.GetTrucoPlayers();
        if (players == null || players.Count == 0) return;

        Player local = PhotonNetwork.LocalPlayer;
        Player other = null;
        foreach (var p in players)
        {
            if (p != null && p.ActorNumber != local.ActorNumber)
            {
                other = p;
                break;
            }
        }

        var img1 = FindImage(_player1IconName);
        var img2 = FindImage(_player2IconName);
        if (img1 != null)
            img1.sprite = SpriteFor(local);
        if (img2 != null && other != null)
            img2.sprite = SpriteFor(other);
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
