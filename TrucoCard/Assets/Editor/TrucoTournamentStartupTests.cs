using System.Collections.Generic;
using MH.Multiplayer;
using NUnit.Framework;

public class TrucoTournamentStartupTests
{
    static TournamentMatch[] Plan(int capacity)
    {
        var tables = new TournamentMatch[capacity / 2];
        for (int i = 0; i < tables.Length; i++)
            tables[i] = new TournamentMatch {
                matchId = "match" + i, roomName = TournamentBracketState.MatchRoomName("t", 0, i),
                round = 0, matchIndex = i, playerIds = new List<string> { "p" + (i * 2), "p" + (i * 2 + 1) }
            };
        return tables;
    }

    [Test]
    public void CompleteOpeningPlansAssignEveryPlayerInFourAndEightPlayerEvents()
    {
        foreach (int count in new[] { 4, 8 })
            for (int i = 0; i < count; i++)
            {
                var tables = Plan(count);
                Assert.IsTrue(TournamentStartupValidation.TryValidateOpeningTables(tables, "t", count,
                    "p" + i, out var assigned));
                Assert.AreSame(tables[i / 2], assigned);
            }
    }

    static void Reject(TournamentMatch[] tables, int capacity = 4, string player = "p0")
    {
        Assert.IsFalse(TournamentStartupValidation.TryValidateOpeningTables(tables, "t", capacity, player, out var assigned));
        Assert.IsNull(assigned, "Never leak a partial assignment when a later table fails validation.");
    }

    [Test]
    public void PartialDuplicateForeignAndMalformedPlansCannotStart()
    {
        Reject(null);
        Reject(Plan(4), 8);
        Reject(Plan(4), 16);
        Reject(Plan(4), 4, "outsider");
        var tables = Plan(4); tables[1] = null; Reject(tables);
        tables = Plan(4); tables[1].matchId = " "; Reject(tables);
        tables = Plan(4); tables[1].matchId = tables[0].matchId; Reject(tables);
        tables = Plan(4); tables[1].playerIds[0] = "p0"; Reject(tables);
        tables = Plan(4); tables[1].playerIds[1] = ""; Reject(tables);
        tables = Plan(4); tables[1].playerIds = null; Reject(tables);
        tables = Plan(4); tables[1].roomName = "foreign-room"; Reject(tables);
        tables = Plan(4); tables[1].matchIndex = 0; Reject(tables);
        tables = Plan(4); tables[1].round = 1; Reject(tables);
    }

    [Test]
    public void FailureTakesPrecedenceOverOtherwiseValidTableIdentity()
    {
        var table = Plan(4)[0];
        var props = new Dictionary<object, object> {
            ["isTournamentMatch"] = true, ["backendMatchId"] = table.matchId, ["tournamentId"] = "t"
        };
        Assert.IsTrue(TournamentStartupValidation.HasMatchingTableIdentity(table, "t", table.roomName, props));
        props["tournamentFailure"] = "No se pudieron crear todas las partidas.";
        Assert.IsTrue(TournamentStartupValidation.HasFailure(props, out var message));
        Assert.IsNotEmpty(message);
        Assert.IsFalse(TournamentStartupValidation.HasMatchingTableIdentity(table, "t", table.roomName, props));
    }

    [Test]
    public void MissingOrMismatchedServerIdentityCannotLoadGameplay()
    {
        var table = Plan(4)[0];
        var props = new Dictionary<object, object> {
            ["isTournamentMatch"] = true, ["backendMatchId"] = table.matchId, ["tournamentId"] = "t"
        };
        Assert.IsFalse(TournamentStartupValidation.HasMatchingTableIdentity(table, "t", "another-room", props));
        Assert.IsFalse(TournamentStartupValidation.HasMatchingTableIdentity(table, "other", table.roomName, props));
        props["backendMatchId"] = "stale";
        Assert.IsFalse(TournamentStartupValidation.HasMatchingTableIdentity(table, "t", table.roomName, props));
        props.Remove("backendMatchId");
        Assert.IsFalse(TournamentStartupValidation.HasMatchingTableIdentity(table, "t", table.roomName, props));
        Assert.IsFalse(TournamentStartupValidation.HasMatchingTableIdentity(null, "t", table.roomName, props));
    }
}
