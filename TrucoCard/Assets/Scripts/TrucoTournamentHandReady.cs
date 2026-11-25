using MH.Multiplayer;
using Photon.Pun;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using System.Collections.Generic;

public static class TrucoTournamentHandReady
{
    public const string Property = "trucoPreparedHand";
    public const string SceneProperty = "trucoGameplayHand";
    public const string RoundProperty = "trucoHandRound";

    public static string Token
    {
        get
        {
            string matchId = MultiplayerController.ActiveMatch?.matchId;
            int round = DataHandler.Instance != null ? DataHandler.Instance.roundNumber : 0;
            return string.IsNullOrEmpty(matchId) || round < 1 ? null : matchId + ":" + round;
        }
    }

    public static int RoomRound => PhotonNetwork.CurrentRoom?.CustomProperties != null
        && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoundProperty, out var raw)
        && raw is int round && round > 0 ? round : 1;

    public static bool IsActiveTable() => MultiplayerController.IsTournamentActive && TrucoMatchPlayGate.IsLiveTable();

    public static void MarkSceneLoaded()
    {
        if (!IsActiveTable() || string.IsNullOrEmpty(Token) || RoomRound != DataHandler.Instance.roundNumber
            || !PhotonNetwork.IsMessageQueueRunning || GameManager.Instance == null || UIMANAGER.Instance == null) return;
        var player = PhotonNetwork.LocalPlayer;
        if (!HasLoadedCurrentHand(player.IsInactive, player.CustomProperties, Token))
            player.SetCustomProperties(new Hashtable { [SceneProperty] = Token });
    }

    public static bool BothScenesLoaded() => BothHaveCurrentProperty(SceneProperty);

    public static bool AdvanceRound(int round)
    {
        if (round < 1 || !PhotonNetwork.IsMasterClient || !IsActiveTable() || RoomRound != round) return false;
        var room = PhotonNetwork.CurrentRoom;
        var values = new Hashtable { [RoundProperty] = round + 1 };
        // The first hand uses the implicit value 1. Later writes compare against the published round.
        var expected = room.CustomProperties.ContainsKey(RoundProperty) ? new Hashtable { [RoundProperty] = round } : null;
        return room.SetCustomProperties(values, expected);
    }

    public static void MarkPrepared()
    {
        if (!IsActiveTable() || !HasDealtCurrentHand() || string.IsNullOrEmpty(Token)) return;
        var player = PhotonNetwork.LocalPlayer;
        if (HasLoadedCurrentHand(player.IsInactive, player.CustomProperties, Token)
            && !HasCurrentPreparation(player.IsInactive, player.CustomProperties, Token))
            player.SetCustomProperties(new Hashtable { [Property] = Token });
    }

    public static bool BothPrepared()
    {
        if (!MultiplayerController.IsTournamentActive) return true;
        string token = Token;
        if (!HasDealtCurrentHand() || !IsActiveTable() || string.IsNullOrEmpty(token)
            || RoomRound != DataHandler.Instance.roundNumber) return false;
        var players = PhotonPlayerHelper.GetTrucoPlayers();
        if (players.Count != 2) return false;
        foreach (var player in players)
            if (!HasPreparedCurrentHand(player.IsInactive, player.CustomProperties, token)) return false;
        return true;
    }

    static bool HasDealtCurrentHand() => GameManager.Instance != null && GameManager.Instance.HasCurrentTournamentDeal()
        && !GameManager.Instance.HandResolved && !GameManager.Instance._gameEnded;

    static bool BothHaveCurrentProperty(string property)
    {
        string token = Token;
        if (!IsActiveTable() || string.IsNullOrEmpty(token) || RoomRound != DataHandler.Instance.roundNumber) return false;
        var players = PhotonPlayerHelper.GetTrucoPlayers();
        if (players.Count != 2) return false;
        foreach (var player in players)
            if (!HasCurrentProperty(player.IsInactive, player.CustomProperties, property, token)) return false;
        return true;
    }

    public static bool HasCurrentPreparation(bool inactive, IDictionary<object, object> properties, string expectedToken) =>
        HasCurrentProperty(inactive, properties, Property, expectedToken);

    public static bool HasLoadedCurrentHand(bool inactive, IDictionary<object, object> properties, string expectedToken) =>
        HasCurrentProperty(inactive, properties, SceneProperty, expectedToken);

    public static bool HasPreparedCurrentHand(bool inactive, IDictionary<object, object> properties, string expectedToken) =>
        HasLoadedCurrentHand(inactive, properties, expectedToken) && HasCurrentPreparation(inactive, properties, expectedToken);

    static bool HasCurrentProperty(bool inactive, IDictionary<object, object> properties, string property, string expectedToken) =>
        !inactive && !string.IsNullOrWhiteSpace(expectedToken) && properties != null
        && properties.TryGetValue(property, out object token) && token as string == expectedToken;
}
