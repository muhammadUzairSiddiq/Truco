using System.Collections.Generic;
using MH.Multiplayer;
using NUnit.Framework;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public sealed class TrucoTournamentAssignedRoomTests
{
    sealed class FixturePlayer : Player
    {
        public FixturePlayer(string user, int actor, bool local, string readyMatch, bool inactive = false, bool spectator = false)
            : base("Fixture Player", actor, local, new Hashtable
            {
                [ActorProperties.UserId] = user,
                [ActorProperties.IsInactive] = inactive,
                [MultiplayerController.TournamentSeatReadyProperty] = readyMatch,
                [PhotonPlayerHelper.SpectatorKey] = spectator
            }) { }
    }

    [Test]
    public void TwoSemifinalsLaunchIndependentlyAndBothClientsCanLoadFromTheirOwnRoomMarker()
    {
        const string firstMatch = "semifinal-0";
        const string secondMatch = "semifinal-1";
        var first = new TournamentMatch { matchId = firstMatch, playerIds = new List<string> { "p1", "p2" } };
        var second = new TournamentMatch { matchId = secondMatch, playerIds = new List<string> { "p3", "p4" } };

        var firstHost = new FixturePlayer("p1", 1, true, firstMatch);
        var firstGuest = new FixturePlayer("p2", 2, false, firstMatch);
        var secondHost = new FixturePlayer("p3", 1, true, secondMatch);
        var secondGuest = new FixturePlayer("p4", 2, false, secondMatch);
        var firstReady = MultiplayerController.HasReadyAssignedPair(new[] { firstHost, firstGuest }, first, "p1");
        var secondReady = MultiplayerController.HasReadyAssignedPair(new[] { secondHost, secondGuest }, second, "p3");

        Assert.IsTrue(firstReady && secondReady);
        Assert.IsFalse(MultiplayerController.HasReadyAssignedPair(new[] { firstHost, secondGuest }, first, "p1"));
        Assert.AreEqual(MultiplayerController.AssignedTableLaunchAction.Publish,
            MultiplayerController.GetAssignedTableLaunchAction(true, firstReady, null, firstMatch));
        Assert.AreEqual(MultiplayerController.AssignedTableLaunchAction.Wait,
            MultiplayerController.GetAssignedTableLaunchAction(false, firstReady, null, firstMatch));
        Assert.AreEqual(MultiplayerController.AssignedTableLaunchAction.Wait,
            MultiplayerController.GetAssignedTableLaunchAction(false, secondReady, firstMatch, secondMatch));
        Assert.AreEqual(MultiplayerController.AssignedTableLaunchAction.Load,
            MultiplayerController.GetAssignedTableLaunchAction(false, secondReady, secondMatch, secondMatch));
        Assert.AreEqual(MultiplayerController.AssignedTableLaunchAction.Load,
            MultiplayerController.GetAssignedTableLaunchAction(true, firstReady, firstMatch, firstMatch));
    }

    [Test]
    public void InactiveSpectatorOrStaleReadyPeerCannotOpenGameplay()
    {
        var table = new TournamentMatch { matchId = "semifinal", playerIds = new List<string> { "p1", "p2" } };
        var local = new FixturePlayer("p1", 1, true, table.matchId);
        foreach (var peer in new[] {
            new FixturePlayer("p2", 2, false, table.matchId, inactive: true),
            new FixturePlayer("p2", 2, false, table.matchId, spectator: true),
            new FixturePlayer("p2", 2, false, "old-game"),
            new FixturePlayer("outsider", 2, false, table.matchId) })
        {
            bool ready = MultiplayerController.HasReadyAssignedPair(new[] { local, peer }, table, "p1");
            Assert.IsFalse(ready);
            Assert.AreEqual(MultiplayerController.AssignedTableLaunchAction.Wait,
                MultiplayerController.GetAssignedTableLaunchAction(true, ready, table.matchId, table.matchId));
        }
    }

    [TestCase("tournament", null, "sa")]
    [TestCase("tournament", "TMatch_cup_R0_M0", "sa")]
    [TestCase("spectator", "TMatch_cup_R0_M0", "sa")]
    [TestCase("spectator", "Match_123", "asia")]
    [TestCase("one_vs_one", "Match_123", "asia")]
    public void TournamentPlayersShareBuildRegionRegardlessOfDeviceOverride(string mode, string room, string expected)
    {
        Assert.AreEqual(expected, TrucoPhotonAuthentication.ResolveConnectionRegion(mode, room, "asia", "sa"));
    }
}
