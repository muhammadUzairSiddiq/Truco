using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MH.Multiplayer;

public sealed class TrucoReleaseTournamentTests
{
    static TournamentBracketState Seed(int capacity)
    {
        var state = new TournamentBracketState(capacity);
        var players = new List<User>();
        for (int index = 0; index < capacity; index++) players.Add(new User { _id = "p" + index, username = "Player " + index });
        state.Seed(players);
        return state;
    }

    [TestCase(4)] [TestCase(8)]
    public void CompleteProjectionAndIndependentResultsCanMergeWithoutReassigningOpeningSeats(int capacity)
    {
        var current = Seed(capacity); var incoming = Seed(capacity);
        current.RecordWinner(0, 0, "p0", "Player 0");
        incoming.RecordWinner(0, 1, "p3", "Player 3");
        Assert.IsTrue(TournamentStartupValidation.CanMergeBracketProjection(current, incoming, capacity));
        incoming.MergeResults(current);
        Assert.IsTrue(TournamentStartupValidation.IsValidBracketProjection(incoming, capacity));
        Assert.IsTrue(incoming.rounds[1][0].IsReady);
        var temporary = incoming.rounds[0][0].first;
        incoming.rounds[0][0].first = incoming.rounds[0][0].second;
        incoming.rounds[0][0].second = temporary;
        Assert.IsFalse(TournamentStartupValidation.CanMergeBracketProjection(current, incoming, capacity), "Known opening positions must not change.");
    }

    [Test]
    public void MalformedProjectionCannotReplaceOrCrashAnExistingBracket()
    {
        var existing = Seed(4);
        Assert.IsFalse(TournamentStartupValidation.CanMergeBracketProjection(existing, Seed(8), 4));
        Assert.IsFalse(TournamentStartupValidation.IsValidBracketProjection(null, 4));
        var invalid = Seed(4); invalid.rounds[0][0].first = null;
        Assert.IsFalse(TournamentStartupValidation.IsValidBracketProjection(invalid, 4));
        invalid = Seed(4); invalid.rounds[0][0].winnerId = "foreign";
        Assert.IsFalse(TournamentStartupValidation.IsValidBracketProjection(invalid, 4));
        invalid = Seed(4); invalid.rounds[0][1].second.playerId = "p0";
        Assert.IsFalse(TournamentStartupValidation.IsValidBracketProjection(invalid, 4));
        invalid = Seed(4); invalid.rounds[1].Clear();
        Assert.IsFalse(TournamentStartupValidation.IsValidBracketProjection(invalid, 4));
        invalid = Seed(4); invalid.rounds[0][0].round = 2;
        Assert.IsFalse(TournamentStartupValidation.IsValidBracketProjection(invalid, 4));
        invalid = Seed(4); invalid.rounds[1][0].first.playerId = "foreign";
        Assert.IsFalse(TournamentStartupValidation.IsValidBracketProjection(invalid, 4), "Later seats must come from their own finished child match.");
    }

    [Test]
    public void MalformedOrMisroutedResultPropertiesAreIgnored()
    {
        foreach (object raw in new object[] { null, 123, "", "{broken", "{}" })
            Assert.IsFalse(TournamentStartupValidation.TryReadBracketResult(raw, 0, 0, out _));
        string json = JsonUtility.ToJson(new TournamentBracketState.BracketMatch { round = 1, index = 0, winnerId = "p0" });
        Assert.IsFalse(TournamentStartupValidation.TryReadBracketResult(json, 0, 0, out _));
        Assert.IsFalse(TournamentStartupValidation.TryReadBracketResult(json, 1, 1, out _));
        Assert.IsTrue(TournamentStartupValidation.TryReadBracketResult(json, 1, 0, out var result));
        Assert.AreEqual("p0", result.winnerId);
    }

    [Test]
    public void InactivePeerAndStaleHandTokensCannotStartTheClock()
    {
        var props = new Dictionary<object, object> { [TrucoTournamentHandReady.Property] = "match:3" };
        Assert.IsTrue(TrucoTournamentHandReady.HasCurrentPreparation(false, props, "match:3"));
        Assert.IsFalse(TrucoTournamentHandReady.HasCurrentPreparation(true, props, "match:3"));
        Assert.IsFalse(TrucoTournamentHandReady.HasCurrentPreparation(false, props, "match:4"));
        Assert.IsFalse(TrucoTournamentHandReady.HasCurrentPreparation(false, props, "other:3"));
        Assert.IsFalse(TrucoTournamentHandReady.HasCurrentPreparation(false, null, "match:3"));
    }

    [Test]
    public void OnlyTheKnownUnfinishedTableCanAcceptAPlayerAssignment()
    {
        var state = Seed(4);
        var table = new TournamentMatch { matchId = "server-match", roomName = TournamentBracketState.MatchRoomName("t", 0, 0),
            round = 0, matchIndex = 0, playerIds = new List<string> { "p1", "p0" } };
        Assert.IsTrue(TournamentStartupValidation.IsValidTableAssignment(table, "t", 4, "p0", state));
        Assert.IsFalse(TournamentStartupValidation.IsValidTableAssignment(null, "t", 4, "p0", state));
        Assert.IsFalse(TournamentStartupValidation.IsValidTableAssignment(table, "other", 4, "p0", state));
        Assert.IsFalse(TournamentStartupValidation.IsValidTableAssignment(table, "t", 4, "outsider", state));
        table.playerIds[1] = "foreign";
        Assert.IsFalse(TournamentStartupValidation.IsValidTableAssignment(table, "t", 4, "p1", state));
        table.playerIds[1] = "p0"; table.matchId = null;
        Assert.IsFalse(TournamentStartupValidation.IsValidTableAssignment(table, "t", 4, "p0", state));
        table.matchId = "server-match"; state.RecordWinner(0, 0, "p0", "Player 0");
        Assert.IsFalse(TournamentStartupValidation.IsValidTableAssignment(table, "t", 4, "p0", state), "A stale RPC must not re-enter a finished table.");
    }
}
