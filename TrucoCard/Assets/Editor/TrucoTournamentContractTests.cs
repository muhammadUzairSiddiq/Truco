using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

public sealed class TrucoTournamentContractTests
{
    static TrucoTournamentService.Snapshot Fixture(int count = 4, bool completed = false)
    {
        var value = new TrucoTournamentService.Snapshot { tournamentId = "cup", name = "Cup", revision = completed ? 5 : 1,
            status = completed ? "completed" : "playing", localStatus = completed ? "champion" : "playing", maxPlayers = count,
            withFlor = true, targetScore = 15, entryFee = 50, advertisedPrize = 200,
            participants = new List<TrucoTournamentService.Participant>(), matches = new List<TrucoTournamentService.Match>(),
            refunds = new List<TrucoTournamentService.Refund>(), wallet = new TrucoTournamentService.Wallet { available = 950, held = 0, revision = 2 } };
        var state = new TournamentBracketState(count); var players = new List<User>();
        for (int index = 0; index < count; index++)
        {
            players.Add(new User { _id = "p" + index, username = "Player " + index });
            value.participants.Add(new TrucoTournamentService.Participant { playerId = "p" + index, name = "Player " + index,
                position = index, registrationId = "entry-" + index, status = "registered", entryPaid = true });
        }
        state.Seed(players);
        for (int round = 0; round < state.rounds.Count; round++) foreach (var table in state.rounds[round])
        {
            var match = new TrucoTournamentService.Match { matchId = table.IsReady || completed ? "m" + round + "-" + table.index : null,
                round = round, index = table.index, roomName = table.IsReady || completed ? TournamentBracketState.MatchRoomName("cup", round, table.index) : null,
                firstPlayerId = table.first.playerId, secondPlayerId = table.second.playerId,
                status = completed ? "finished" : table.IsReady ? "ready" : "waiting" };
            if (completed) { match.winnerId = table.first.playerId; state.RecordWinner(round, table.index, match.winnerId, table.first.playerName); }
            value.matches.Add(match);
        }
        if (completed)
        {
            value.payout = new TrucoTournamentService.Payout { status = "paid", amount = 180, currency = "TRUCOINS", payoutId = "payment-1", championId = "p0" };
            value.wallet.available = 1130; value.wallet.revision = 3;
        }
        else value.localAssignment = new TrucoTournamentService.Assignment { matchId = "m0-0", round = 0, index = 0,
            roomName = TournamentBracketState.MatchRoomName("cup", 0, 0), playerIds = new List<string> { "p0", "p1" } };
        return value;
    }
    static JObject Receipt(TrucoTournamentService.Snapshot value, string key = null) => new JObject {
        ["success"] = true, ["data"] = new JObject { ["schemaVersion"] = 1, ["idempotencyKey"] = key, ["snapshot"] = JObject.FromObject(value) }
    };
    static TrucoTournamentService.Snapshot Parse(JObject json, TrucoTournamentService.Snapshot old = null, string key = null) =>
        TrucoTournamentService.ReadReceipt(json.ToString(), "cup", "p0", old, key);

    [TestCase(4)] [TestCase(8)]
    public void VersionedServerSnapshotAssignsOnlyTheAuthenticatedPlayersPublishedTable(int capacity)
    {
        var value = Parse(Receipt(Fixture(capacity)));
        var table = TrucoTournamentService.AssignedTable(value);
        Assert.AreEqual("m0-0", table.matchId); Assert.AreEqual(0, table.round);
        CollectionAssert.AreEquivalent(new[] { "p0", "p1" }, table.playerIds);
        Assert.AreEqual(capacity / 2, TrucoTournamentService.ToBracket(value).rounds[0].Count);
    }
    [Test]
    public void LegacySuccessAndIncompleteFinancialFieldsCannotActivateTheContract()
    {
        Assert.Throws<InvalidOperationException>(() => TrucoTournamentService.ReadReceipt("{\"success\":true}", "cup", "p0"));
        var json = Receipt(Fixture()); json["data"]["schemaVersion"] = 2;
        Assert.Throws<InvalidOperationException>(() => Parse(json));
        json = Receipt(Fixture()); ((JObject)json["data"]["snapshot"]["wallet"]).Remove("held");
        Assert.Throws<InvalidOperationException>(() => Parse(json));
        json = Receipt(Fixture()); ((JObject)json["data"]["snapshot"]).Remove("withFlor");
        Assert.Throws<InvalidOperationException>(() => Parse(json));
        json = Receipt(Fixture()); json["data"]["snapshot"]["entryFee"] = 1.5;
        Assert.Throws<InvalidOperationException>(() => Parse(json));
    }
    [Test]
    public void ReplayKeyAndRevisionMustMatchAndOpeningPositionsCannotChange()
    {
        var old = Fixture(); var json = Receipt(Fixture(), "stable-key");
        Assert.DoesNotThrow(() => Parse(json, old, "stable-key"));
        Assert.Throws<InvalidOperationException>(() => Parse(json, old, "different-key"));
        var value = Fixture(); value.revision = 0;
        Assert.Throws<InvalidOperationException>(() => Parse(Receipt(value), old));
        value = Fixture(); value.name = "conflicting same revision";
        Assert.Throws<InvalidOperationException>(() => Parse(Receipt(value), old));
        value = Fixture(); value.revision = 2; value.participants[0].position = 1; value.participants[1].position = 0;
        Assert.Throws<InvalidOperationException>(() => Parse(Receipt(value), old));
    }
    [Test]
    public void SnapshotArrayOrderingDoesNotInventARevisionConflict()
    {
        var old = Fixture(); var incoming = Fixture(); incoming.participants.Reverse(); incoming.matches.Reverse();
        Assert.DoesNotThrow(() => Parse(Receipt(incoming), old));
    }
    [TestCase(4)] [TestCase(8)]
    public void PaidPrizeUsesActualReceiptAmountAndRequiresTheFinishedFinal(int capacity)
    {
        var value = Parse(Receipt(Fixture(capacity, true)));
        Assert.IsTrue(TrucoTournamentService.HasPaidPayout(value));
        Assert.AreEqual(180, value.payout.amount); Assert.AreNotEqual(value.advertisedPrize, value.payout.amount);
        value.payout.championId = "p1";
        Assert.Throws<InvalidOperationException>(() => Parse(Receipt(value)));
        value = Fixture(capacity, true); value.payout.payoutId = null;
        Assert.Throws<InvalidOperationException>(() => Parse(Receipt(value)));
        value = Fixture(capacity, true); value.payout.currency = "OTHER";
        Assert.Throws<InvalidOperationException>(() => Parse(Receipt(value)));
        value = Fixture(capacity, true); value.status = "playing";
        Assert.Throws<InvalidOperationException>(() => Parse(Receipt(value)));
        var json = Receipt(Fixture(capacity, true)); json["data"]["snapshot"]["payout"]["amount"] = 180.5;
        Assert.Throws<InvalidOperationException>(() => Parse(json));
    }
    [Test]
    public void BothDisconnectCancellationHasNoWinnerAndPendingRefundIsNotAConfirmedRefund()
    {
        var value = Fixture(); value.status = "cancelled"; value.localStatus = "withdrawn"; value.localAssignment = null;
        foreach (var match in value.matches) { match.status = "cancelled"; match.winnerId = null; }
        foreach (var player in value.participants)
            value.refunds.Add(new TrucoTournamentService.Refund { playerId = player.playerId, amount = 50, status = "pending" });
        var parsed = Parse(Receipt(value));
        Assert.IsFalse(TrucoTournamentService.HasPaidPayout(parsed));
        Assert.IsTrue(parsed.matches.All(m => string.IsNullOrEmpty(m.winnerId)));
        Assert.AreEqual("pending", parsed.refunds[0].status);
        Assert.AreEqual(950, parsed.wallet.available, "A pending refund cannot be added locally.");
        value.refunds[0].status = "refunded";
        Assert.Throws<InvalidOperationException>(() => Parse(Receipt(value)), "A confirmed refund needs its server receipt ID.");
        value.refunds[0].refundId = "refund-1"; value.wallet.available = 1000;
        Assert.DoesNotThrow(() => Parse(Receipt(value)));
        value.refunds[0].amount = 45;
        Assert.Throws<InvalidOperationException>(() => Parse(Receipt(value)), "A cancelled entry cannot keep a commission.");
    }
    [Test]
    public void ForeignAssignmentOrResultCannotChangeTheBracket()
    {
        var value = Fixture(); value.localAssignment.playerIds = new List<string> { "p2", "p3" };
        Assert.Throws<InvalidOperationException>(() => Parse(Receipt(value)));
        value = Fixture(); value.matches[0].winnerId = "outsider"; value.matches[0].status = "finished";
        Assert.Throws<InvalidOperationException>(() => Parse(Receipt(value)));
        value = Fixture(); value.matches[0].matchId = null;
        Assert.Throws<InvalidOperationException>(() => Parse(Receipt(value)));
        value = Fixture(); value.participants[1].entryPaid = false;
        Assert.Throws<InvalidOperationException>(() => Parse(Receipt(value)));
    }

    [Test]
    public void ConfirmedPaymentAndWalletRevisionsCannotBeRewritten()
    {
        var old = Fixture(4, true); var value = Fixture(4, true); value.revision = 6;
        value.payout.amount = 181;
        Assert.Throws<InvalidOperationException>(() => Parse(Receipt(value), old));
        value = Fixture(4, true); value.revision = 6; value.wallet.revision = 1;
        Assert.Throws<InvalidOperationException>(() => Parse(Receipt(value), old));
        value = Fixture(4, true); value.revision = 6; value.status = "playing";
        Assert.Throws<InvalidOperationException>(() => Parse(Receipt(value), old));
    }
}
