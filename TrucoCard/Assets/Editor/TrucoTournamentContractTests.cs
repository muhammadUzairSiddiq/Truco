using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

public sealed class TrucoTournamentContractTests
{
    [TestCase(403, true)] [TestCase(400, true)] [TestCase(408, false)]
    [TestCase(409, false)] [TestCase(500, false)] [TestCase(0, false)]
    public void RejectedRequestsAreDistinguishedFromUncertainWrites(long status, bool rejected)
    {
        Assert.AreEqual(rejected, new HttpApiClient.RequestException(status, null, "test").IsDefiniteRejection);
    }

    [TestCase("{\"error\":{\"code\":\"WALLET_FROZEN\",\"message\":\"Wallet is frozen\"}}")]
    [TestCase("{\"code\":\"WALLET_FROZEN\",\"message\":\"Wallet is frozen\"}")]
    [TestCase("{\"error\":\"WALLET_FROZEN\"}")]
    public void FrozenWalletCodeSurvivesErrorParsing(string json)
    {
        Assert.AreEqual("WALLET_FROZEN", HttpApiClient.ReadErrorCode(json));
    }

    [Test]
    public void MissingListRulesDoNotBecomeConfirmedDefaults()
    {
        var missing = ApiController.TryParseTournamentListJson("[{\"_id\":\"cup\"}]");
        Assert.IsFalse(missing[0].rulesConfirmed);
        var actual = ApiController.TryParseTournamentListJson("[{\"_id\":\"cup\",\"withFlor\":false,\"targetScore\":30}]");
        Assert.IsTrue(actual[0].rulesConfirmed);
        Assert.IsFalse(actual[0].withFlor);
        Assert.AreEqual(30, actual[0].targetScore);
    }

    [Test]
    public void StartRequiresAllEntriesPaidAndRegistered()
    {
        var value = Fixture(); value.status = "registration"; value.localStatus = "registered";
        Assert.IsTrue(MH.Multiplayer.MultiplayerController.CanStartTournament(value));
        value.participants[0].entryPaid = false;
        Assert.IsFalse(MH.Multiplayer.MultiplayerController.CanStartTournament(value));
        value.participants[0].entryPaid = true; value.participants[0].status = "withdrawn";
        Assert.IsFalse(MH.Multiplayer.MultiplayerController.CanStartTournament(value));
        value.participants.RemoveAt(0);
        Assert.IsFalse(MH.Multiplayer.MultiplayerController.CanStartTournament(value));
    }

    [Test]
    public void DuplicatePaidSeatOrRegistrationCannotStartAFourPlayerTournament()
    {
        var value = Fixture();
        value.status = "registration";
        value.localStatus = "registered";
        value.participants[3].registrationId = value.participants[2].registrationId;
        Assert.IsFalse(TrucoTournamentService.CanStart(value));
        value.participants[3].registrationId = "entry-3";
        value.participants[3].position = 2;
        Assert.IsFalse(TrucoTournamentService.CanStart(value));
    }

    [Test]
    public void FirstSemifinalRemainsBlockedUntilTheSecondSemifinalIsPublished()
    {
        var snapshot = Fixture();
        var other = snapshot.matches.Single(m => m.round == 0 && m.index == 1);
        other.status = "waiting";
        other.matchId = other.roomName = other.firstPlayerId = other.secondPlayerId = null;
        var partial = Parse(Receipt(snapshot));
        Assert.IsFalse(TrucoTournamentService.HasCompleteOpeningRound(partial));
        Assert.IsNull(TrucoTournamentService.AssignedTable(partial));

        var complete = Fixture();
        complete.revision++;
        complete.status = "starting";
        var starting = Parse(Receipt(complete), partial);
        Assert.IsTrue(TrucoTournamentService.HasCompleteOpeningRound(starting));
        Assert.IsNull(TrucoTournamentService.AssignedTable(starting));
        complete.revision++;
        complete.status = "playing";
        var playing = Parse(Receipt(complete), starting);
        Assert.AreEqual("m0-0", TrucoTournamentService.AssignedTable(playing)?.matchId);

        complete.revision++;
        complete.participants[1].status = "withdrawn";
        var withdrawnOpponent = Parse(Receipt(complete), playing);
        Assert.IsTrue(TrucoTournamentService.HasCompleteOpeningRound(withdrawnOpponent));
        Assert.IsNull(TrucoTournamentService.AssignedTable(withdrawnOpponent));
    }

    [Test]
    public void PaidPlayerCanRecoverTheServerPublishedSeatWhenTheAssignmentFieldWasOmitted()
    {
        var snapshot = Fixture();
        var explicitSeat = Parse(Receipt(snapshot));
        snapshot.localAssignment = null;
        var recoveredSeat = Parse(Receipt(snapshot), explicitSeat);
        Assert.AreEqual("m0-0", TrucoTournamentService.AssignedTable(recoveredSeat)?.matchId);
        snapshot.localStatus = "not_registered";
        var outsider = TrucoTournamentService.ReadReceipt(Receipt(snapshot).ToString(), "cup", "outsider");
        Assert.IsNull(TrucoTournamentService.AssignedTable(outsider));
    }

    [Test]
    public void LiveListPlayersShapeKeepsRegisteredPlayersAbleToFindAStartedTournament()
    {
        var rows = ApiController.TryParseTournamentListJson(@"{""success"":true,""tournaments"":[
          {""_id"":""t"",""status"":""playing"",""players"":[{""_id"":""seat-0"",""username"":""One"",""email"":""private""},""seat-1"",{""id"":""seat-0""}]},
          {""_id"":""legacy"",""status"":""active"",""players"":[{""id"":""seat-1""}]},
          {""_id"":""ended"",""status"":""completed"",""players"":[{""id"":""seat-1""}]}]}");
        Assert.AreEqual(2, rows[0].participants.Count);
        Assert.IsNull(rows[0].participants[0].email);
        Assert.IsTrue(rows[0].HasPlayer("seat-1"));
        Assert.IsTrue(SessionTournament.IsBrowsable(rows[0], "seat-1"));
        Assert.IsTrue(SessionTournament.IsBrowsable(rows[1], "seat-1"));
        Assert.IsFalse(SessionTournament.IsBrowsable(rows[1], "other"));
        Assert.IsFalse(SessionTournament.IsBrowsable(rows[2], "seat-1"));
    }

    [Test]
    public void FourPlayersReceiveTwoSemifinalsThenOnlyTheWinnersReceiveTheFinal()
    {
        var waiting = Fixture(); waiting.status = "registration"; waiting.localStatus = "registered"; waiting.localAssignment = null;
        foreach (var match in waiting.matches)
        { match.matchId = match.roomName = match.firstPlayerId = match.secondPlayerId = null; match.status = "waiting"; }
        Assert.IsTrue(MH.Multiplayer.MultiplayerController.CanStartTournament(waiting));
        var previous = new TrucoTournamentService.Snapshot[4];
        for (int player = 0; player < 4; player++)
        {
            var local = Fixture(); local.revision = 2;
            int table = player / 2;
            local.localAssignment = new TrucoTournamentService.Assignment { round = 0, index = table,
                matchId = "m0-" + table, roomName = TournamentBracketState.MatchRoomName("cup", 0, table),
                playerIds = new List<string> { "p" + (table * 2), "p" + (table * 2 + 1) } };
            previous[player] = TrucoTournamentService.ReadReceipt(Receipt(local).ToString(), "cup", "p" + player, waiting);
            Assert.AreEqual(table, TrucoTournamentService.AssignedTable(previous[player]).matchIndex);
        }
        var next = Fixture(); next.revision = 3;
        next.matches[0].status = next.matches[1].status = "finished";
        next.matches[0].winnerId = "p0"; next.matches[1].winnerId = "p3";
        next.participants[1].status = next.participants[2].status = "eliminated";
        var final = next.matches.Single(m => m.round == 1);
        final.matchId = "final-server-id"; final.roomName = TournamentBracketState.MatchRoomName("cup", 1, 0);
        final.status = "ready"; final.firstPlayerId = "p0"; final.secondPlayerId = "p3";
        for (int player = 0; player < 4; player++)
        {
            bool finalist = player == 0 || player == 3;
            next.localStatus = finalist ? "playing" : "eliminated";
            next.localAssignment = finalist ? new TrucoTournamentService.Assignment { round = 1, index = 0,
                matchId = final.matchId, roomName = final.roomName, playerIds = new List<string> { "p0", "p3" } } : null;
            var parsed = TrucoTournamentService.ReadReceipt(Receipt(next).ToString(), "cup", "p" + player, previous[player]);
            if (finalist) Assert.AreEqual("final-server-id", TrucoTournamentService.AssignedTable(parsed).matchId);
            else Assert.IsNull(TrucoTournamentService.AssignedTable(parsed));
            previous[player] = parsed;
        }
        next.revision = 4; next.status = "completed"; next.localAssignment = null;
        final.status = "finished"; final.winnerId = "p3";
        next.participants[0].status = "eliminated";
        next.payout = new TrucoTournamentService.Payout { status = "paid", amount = 180,
            currency = "TRUCOINS", payoutId = "settlement-final", championId = "p3" };
        for (int player = 0; player < 4; player++)
        {
            next.localStatus = player == 3 ? "champion" : "eliminated";
            var parsed = TrucoTournamentService.ReadReceipt(Receipt(next).ToString(), "cup", "p" + player, previous[player]);
            Assert.IsTrue(TrucoTournamentService.HasPaidPayout(parsed));
            Assert.IsNull(TrucoTournamentService.AssignedTable(parsed));
        }
    }

    [Test]
    public void MixedTournamentRulesStayIndependentAndNewTournamentsDefaultToFlorAnd15()
    {
        var rows = ApiController.TryParseTournamentListJson("[{\"_id\":\"a\",\"withFlor\":true,\"targetScore\":15},{\"_id\":\"b\",\"withFlor\":false,\"targetScore\":30}]");
        Assert.IsTrue(rows[0].withFlor); Assert.AreEqual(15, rows[0].targetScore);
        Assert.IsFalse(rows[1].withFlor); Assert.AreEqual(30, rows[1].targetScore);
        var request = new CreateTournamentRequestAdmin();
        Assert.IsTrue(request.withFlor); Assert.AreEqual(15, request.targetScore);
    }

    [Test]
    public void AnOldPaidJoinReceiptCanOnlyStartANewEntryAfterANewerServerConfirmedExit()
    {
        var paid = Fixture(); paid.status = "registration"; paid.localStatus = "registered";
        var exited = Fixture(); exited.revision = paid.revision + 1;
        exited.status = "registration"; exited.localStatus = "not_registered";
        exited.participants.RemoveAt(0);
        Assert.IsTrue(TrucoTournamentService.CanRegisterAfterReplayedExit(paid, exited, "p0"));
        exited.revision = paid.revision;
        Assert.IsFalse(TrucoTournamentService.CanRegisterAfterReplayedExit(paid, exited, "p0"));
        exited.revision++;
        exited.localStatus = "registered";
        Assert.IsFalse(TrucoTournamentService.CanRegisterAfterReplayedExit(paid, exited, "p0"));
        exited.localStatus = "not_registered"; exited.status = "playing";
        Assert.IsFalse(TrucoTournamentService.CanRegisterAfterReplayedExit(paid, exited, "p0"));
        exited.status = "registration"; paid.participants[0].entryPaid = false;
        Assert.IsFalse(TrucoTournamentService.CanRegisterAfterReplayedExit(paid, exited, "p0"));
    }

    [Test]
    public void RefundHistoryFromRepeatedRegistrationsRemainsValidAndCannotHideAPendingRefund()
    {
        var value = Fixture();
        value.refunds.Add(new TrucoTournamentService.Refund { playerId = "p0", status = "refunded", amount = 50,
            refundId = "first-refund", _id = "01" });
        value.refunds.Add(new TrucoTournamentService.Refund { playerId = "p0", status = "refunded", amount = 50,
            refundId = "second-refund", _id = "02" });
        Assert.DoesNotThrow(() => Parse(Receipt(value)));
        Assert.AreEqual("second-refund", TrucoTournamentService.RefundForPlayer(value, "p0").refundId);
        var previous = Parse(Receipt(value));
        value.refunds.Reverse();
        Assert.DoesNotThrow(() => Parse(Receipt(value), previous));
        value.revision++;
        value.refunds.Add(new TrucoTournamentService.Refund { playerId = "p0", status = "pending", amount = 50, _id = "03" });
        Assert.DoesNotThrow(() => Parse(Receipt(value), previous));
        Assert.AreEqual("pending", TrucoTournamentService.RefundForPlayer(value, "p0").status);
        value.refunds[0].refundId = value.refunds[1].refundId;
        Assert.Throws<InvalidOperationException>(() => Parse(Receipt(value)));
    }

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
    public void RegistrationAcceptsEmptyMatchRowsBeforeTheServerAssignsSeats()
    {
        var registration = Fixture();
        registration.status = "registration";
        registration.localStatus = "not_registered";
        registration.localAssignment = null;
        registration.participants = registration.participants.Skip(1).Take(1).ToList();
        foreach (var row in registration.matches)
        {
            row.status = "waiting";
            row.matchId = row.roomName = row.firstPlayerId = row.secondPlayerId = null;
        }

        var previous = Parse(Receipt(registration));
        Assert.IsNull(TrucoTournamentService.AssignedTable(previous));
        Assert.AreEqual("p1", TrucoTournamentService.ToBracket(previous).rounds[0][0].second.playerId);

        registration.revision++;
        registration.participants[0].position = 0;
        Assert.DoesNotThrow(() => Parse(Receipt(registration), previous));

        var playing = Fixture();
        playing.revision = 3;
        Assert.DoesNotThrow(() => Parse(Receipt(playing), previous));
        playing.matches[0].firstPlayerId = playing.matches[0].secondPlayerId = null;
        Assert.Throws<InvalidOperationException>(() => Parse(Receipt(playing), previous));
    }

    [Test]
    public void LegacySuccessAndIncompleteFinancialFieldsCannotActivateTheContract()
    {
        Assert.Throws<InvalidOperationException>(() => TrucoTournamentService.ReadReceipt("{\"success\":true}", "cup", "p0"));
        var json = Receipt(Fixture()); json["data"]["schemaVersion"] = 2;
        Assert.Throws<InvalidOperationException>(() => Parse(json));
        json = Receipt(Fixture()); ((JObject)json["data"]["snapshot"]["wallet"]).Remove("held");
        var error = Assert.Throws<InvalidOperationException>(() => Parse(json));
        Assert.AreEqual("snapshot.wallet.held: expected Integer, received missing", error.InnerException?.Message);
        json = Receipt(Fixture()); ((JObject)json["data"]["snapshot"]).Remove("withFlor");
        Assert.Throws<InvalidOperationException>(() => Parse(json));
        json = Receipt(Fixture()); json["data"]["snapshot"]["entryFee"] = 1.5;
        Assert.Throws<InvalidOperationException>(() => Parse(json));
        json = Receipt(Fixture()); json["data"]["snapshot"]["participants"][0] = "unexpected";
        error = Assert.Throws<InvalidOperationException>(() => Parse(json));
        Assert.AreEqual("snapshot.participants[0]: expected Object, received String", error.InnerException?.Message);
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
