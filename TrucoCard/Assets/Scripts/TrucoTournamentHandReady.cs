using MH.Multiplayer;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using System.Collections.Generic;

public static class TrucoTournamentHandReady
{
    public const string Property = "trucoPreparedHand";
    public static string Token => MultiplayerController.ActiveMatch?.matchId + ":" + (DataHandler.Instance != null ? DataHandler.Instance.roundNumber : -1);
    public static void MarkPrepared()
    {
        if (!MultiplayerController.IsTournamentActive || !TrucoMatchPlayGate.IsLiveTable()
            || GameManager.Instance == null || !GameManager.Instance.HasHandBeenDealt()
            || GameManager.Instance.HandResolved || GameManager.Instance._gameEnded) return;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [Property] = Token });
    }
    public static bool BothPrepared()
    {
        if (!MultiplayerController.IsTournamentActive) return true;
        if (!TrucoMatchPlayGate.IsLiveTable() || GameManager.Instance == null || !GameManager.Instance.HasHandBeenDealt()
            || GameManager.Instance.HandResolved || GameManager.Instance._gameEnded) return false;
        var players = PhotonPlayerHelper.GetTrucoPlayers();
        if (players.Count != 2) return false;
        foreach (var player in players)
            if (player == null || !HasCurrentPreparation(player.IsInactive, player.CustomProperties, Token)) return false;
        return true;
    }

    public static bool HasCurrentPreparation(bool inactive, IDictionary<object, object> properties, string expectedToken) =>
        !inactive && !string.IsNullOrWhiteSpace(expectedToken) && properties != null
        && properties.TryGetValue(Property, out object token) && token as string == expectedToken;
}
