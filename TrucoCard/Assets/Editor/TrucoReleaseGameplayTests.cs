using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using Photon.Realtime;
using UnityEngine;

public sealed class TrucoReleaseGameplayTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void TournamentReconnectTimeoutDoesNotCreateALocalWinnerOrLoss(bool opponentTimedOut)
    {
        var host = new GameObject("TournamentReconnectAuthorityRegression"); host.SetActive(false);
        var snapshotProperty = typeof(MH.Multiplayer.MultiplayerController).GetProperty("CurrentSnapshot");
        var previousSnapshot = snapshotProperty.GetValue(null);
        try
        {
            // No server snapshot means no request can be made: deterministic, zero network I/O.
            snapshotProperty.SetValue(null, null);
            var game = host.AddComponent<GameManager>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(GameManager).GetField("_isInTournament", flags).SetValue(game, true);
            if (opponentTimedOut) game.WinByOpponentWalkover();
            else game.HandleReconnectionFailedExit();
            Assert.IsFalse(game.CanPlayCard(), "Cards remain disabled even when the local server snapshot is unavailable.");
            Assert.IsFalse(game._gameEnded, "Disconnects require an authoritative result before local victory presentation.");
            Assert.IsFalse((bool)typeof(GameManager).GetField("_tournamentMatchFinalized", flags).GetValue(game),
                "An absent server decision cannot eliminate a player locally.");
        }
        finally { snapshotProperty.SetValue(null, previousSnapshot); Object.DestroyImmediate(host); }
    }

    sealed class Seat : Player
    {
        public Seat(int actor, bool spectator, bool local = false, string publishedId = null, string claimedId = null)
            : base("Seat " + actor, actor, local, new ExitGames.Client.Photon.Hashtable {
                [PhotonPlayerHelper.SpectatorKey] = spectator,
                [ActorProperties.UserId] = publishedId, ["userId"] = claimedId }) { }
    }

    [Test]
    public void WritablePhotonPropertiesCannotImpersonateAPublishedParticipant()
    {
        var peer = new Seat(2, false, publishedId: "verified-opponent", claimedId: "victim");
        Assert.AreEqual("verified-opponent", PhotonPlayerHelper.TryGetBackendUserId(peer));
        peer.CustomProperties["userId"] = "another-victim";
        Assert.AreEqual("verified-opponent", PhotonPlayerHelper.TryGetBackendUserId(peer));
        Assert.IsNull(PhotonPlayerHelper.TryGetBackendUserId(new Seat(3, false, claimedId: "victim")),
            "A custom userId cannot replace missing authenticated identity.");
    }

    [Test]
    public void PublishedRoomPairMustMatchTheActualApiAssignmentAndLocalAccount()
    {
        var local = new Seat(1, false, true, "local", "forged-local");
        var opponent = new Seat(2, false, publishedId: "opponent", claimedId: "forged-opponent");
        var spectator = new Seat(3, true, publishedId: "admin");
        var assignment = new[] { "local", "opponent" };
        Assert.IsTrue(PhotonPlayerHelper.HasMatchingAuthenticatedPair(new[] { local, spectator, opponent }, assignment, "local"));
        Assert.IsFalse(PhotonPlayerHelper.HasMatchingAuthenticatedPair(new[] { local, opponent }, assignment, "opponent"));
        Assert.IsFalse(PhotonPlayerHelper.HasMatchingAuthenticatedPair(new[] { local, new Seat(2, false, publishedId: "intruder", claimedId: "opponent") }, assignment, "local"));
        Assert.IsFalse(PhotonPlayerHelper.HasMatchingAuthenticatedPair(new[] { local, new Seat(2, false, publishedId: "local") }, assignment, "local"));
        Assert.IsFalse(PhotonPlayerHelper.HasMatchingAuthenticatedPair(new[] { local, new Seat(2, false, claimedId: "opponent") }, assignment, "local"));
        Assert.IsFalse(PhotonPlayerHelper.HasMatchingAuthenticatedPair(new[] { local, opponent }, new[] { "local" }, "local"));
    }

    [TestCase("", false)]
    [TestCase("not-an-app-id", false)]
    [TestCase("11111111-1111-1111-1111-111111111111", false)]
    [TestCase("22222222-2222-2222-2222-222222222222", true)]
    public void SyntheticQaRequiresADifferentValidStagingPhotonApp(string staging, bool allowed)
    {
        Assert.AreEqual(allowed, TrucoDevelopmentSettings.IsSeparateStagingApp("11111111-1111-1111-1111-111111111111", staging));
    }

    static List<DeckCards> Cards(CardSuit suit, int firstRank) => new List<DeckCards> {
        new DeckCards { suit = suit, rank = firstRank }, new DeckCards { suit = suit, rank = firstRank + 1 }, new DeckCards { suit = suit, rank = firstRank + 2 }
    };

    [Test]
    public void CachedNoFlorCannotSuppressTheCurrentOpponentsFlor()
    {
        var host = new GameObject("CachedNoFlorRegression"); host.SetActive(false);
        bool previous = OneVsOneMatchSession.WithFlor;
        try
        {
            OneVsOneMatchSession.SetWithFlor(true);
            var game = host.AddComponent<GameManager>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(GameManager).GetField("_handSeats", flags).SetValue(game, new TrucoHandSeats(1, 3, 2));
            typeof(GameManager).GetField("_player1Cards", flags).SetValue(game, Cards(CardSuit.Swords, 1));
            typeof(GameManager).GetField("_player2Cards", flags).SetValue(game, Cards(CardSuit.Coins, 4));
            game.otherPlayerDeniedFlor = true;
            typeof(GameManager).GetMethod("NoFlor", flags).Invoke(game, null);
            Assert.IsFalse(game.otherPlayerDeniedFlor, "A cached denial cannot override a Flor in the current validated deal.");
            game.player1Score.Add(new ScoreClass { suit = CardSuit.Swords, value = 1 });
            game.player2Score.Add(new ScoreClass { suit = CardSuit.Coins, value = 4 });
            typeof(GameManager).GetMethod("NoFlor", flags).Invoke(game, null);
            Assert.IsTrue(game.otherPlayerDeniedFlor, "Playing a card still closes the opponent's Flor window.");
        }
        finally { OneVsOneMatchSession.SetWithFlor(previous); Object.DestroyImmediate(host); }
    }

    [TestCase(1)]
    [TestCase(2)]
    public void SerializedDealKeepsItsOwnersWhenMasterAndManoActorsRotate(int round)
    {
        var host = new GameObject("ActorOwnedDealRegression"); host.SetActive(false);
        try
        {
            var game = host.AddComponent<GameManager>();
            int originalDealer = round == 1 ? 1 : 3;
            int pie = round == 1 ? 3 : 1;
            var ownership = new TrucoHandSeats(originalDealer, pie, round);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(GameManager).GetField("_handSeats", flags).SetValue(game, ownership);
            typeof(GameManager).GetField("_player1Cards", flags).SetValue(game, Cards(CardSuit.Swords, 1));
            typeof(GameManager).GetField("_player2Cards", flags).SetValue(game, Cards(CardSuit.Coins, 4));
            string snapshot = (string)typeof(GameManager).GetMethod("SerializeHandsForSync", flags).Invoke(game, null);
            var parts = snapshot.Split('!');
            var first = JsonUtility.FromJson<DeckCardsWrapper>(parts[0]);
            var second = JsonUtility.FromJson<DeckCardsWrapper>(parts[1]);
            // The pie becomes master; player enumeration also changes. Ownership must not.
            var afterMigration = new Player[] { new Seat(pie, false), new Seat(2, true), new Seat(originalDealer, false) };
            Assert.IsTrue(TrucoHandSeats.TryRestore(first, second, round, 0, afterMigration, default, out var restored));
            Assert.AreEqual(originalDealer, restored.FirstActor);
            Assert.AreEqual(pie, restored.SecondActor);
            Assert.AreEqual(2, restored.SeatForActor(pie));
            int manoActor = round % 2 == 1 ? 1 : 3;
            int tiedHandWinner = TrucoHandWinner.Evaluate(new[] { 0, 0, 0 }, restored.SeatForActor(manoActor)).Value;
            Assert.AreEqual(manoActor, restored.ActorForSeat(tiedHandWinner), "An even-hand dealer remains Mano after authority changes.");
            Assert.AreEqual(originalDealer, TrucoMatchRules.ResolveEnvidoWinnerActor(33, 29, restored.FirstActor, restored.SecondActor, manoActor));
            Assert.AreEqual(CardSuit.Swords, first.cards[0].suit);
            Assert.AreEqual(CardSuit.Coins, second.cards[0].suit);
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void ReconnectRejectsStaleOrAmbiguousDealsAndOnlyReusesKnownLegacyOwnership()
    {
        var players = new Player[] { new Seat(1, false), new Seat(3, false) };
        var first = new DeckCardsWrapper { cards = Cards(CardSuit.Swords, 1), actorNumber = 1, handRound = 1 };
        var second = new DeckCardsWrapper { cards = Cards(CardSuit.Coins, 4), actorNumber = 3, handRound = 1 };
        Assert.IsFalse(TrucoHandSeats.TryRestore(first, second, 2, 1, players, default, out _), "Old cached hand cannot prepare the next round.");
        first.actorNumber = second.actorNumber = first.handRound = second.handRound = 0;
        Assert.IsFalse(TrucoHandSeats.TryRestore(first, second, 1, 0, players, default, out _), "Unknown legacy snapshot cannot guess from the new master.");
        var known = new TrucoHandSeats(1, 3, 1);
        Assert.IsTrue(TrucoHandSeats.TryRestore(first, second, 1, 1, players, known, out var legacy));
        Assert.AreEqual(1, legacy.FirstActor);
        second.cards[0] = first.cards[0];
        Assert.IsFalse(TrucoHandSeats.TryRestore(first, second, 1, 1, players, known, out _), "Duplicate cards are never a ready deal.");
    }

    [Test]
    public void CachedDealNeverResurrectsPlayedCardsOrReopensAClosedHand()
    {
        var current = new TrucoHandSeats(1, 3, 2);
        Assert.IsFalse(GameManager.ShouldReplaceDealtHand(current, new TrucoHandSeats(1, 3, 2), true, false));
        Assert.IsFalse(GameManager.ShouldReplaceDealtHand(current, current, true, true));
        Assert.IsFalse(GameManager.ShouldReplaceDealtHand(default, current, false, true));
        Assert.IsTrue(GameManager.ShouldReplaceDealtHand(default, current, false, false));
        Assert.IsTrue(GameManager.ShouldReplaceDealtHand(current, new TrucoHandSeats(3, 1, 3), true, false));
        Assert.IsFalse(GameManager.CanApplyHandSnapshot(current, 1), "A late snapshot must not roll a validated hand backward.");
        Assert.IsTrue(GameManager.CanApplyHandSnapshot(current, 2));
        Assert.IsTrue(GameManager.CanApplyHandSnapshot(default, 2), "A fresh rejoin may learn its real current round.");
    }

    [TestCase(true, true, true, false, false, true)]
    [TestCase(true, true, true, true, false, false)]
    [TestCase(true, true, true, false, true, false)]
    [TestCase(true, false, true, false, false, false)]
    [TestCase(false, true, true, false, false, false)]
    [TestCase(true, true, false, false, false, false)]
    public void MasterTakeoverResumesOnlyAnUnstartedPreparedTournamentHand(bool tournament, bool localMaster,
        bool dealt, bool started, bool locked, bool expected) =>
        Assert.AreEqual(expected, TurnManager.ShouldResumePreparedTournamentHand(tournament, localMaster, dealt, started, locked));

    [TestCase(2, 0, 15, 2)]
    [TestCase(3, 14, 15, 1)]
    [TestCase(2, 29, 30, 1)]
    [TestCase(5, 30, 30, 0)]
    [TestCase(-1, 0, 15, 0)]
    public void AcceptedEnvidoCapsAgainstWinnerScore(int stake, int winnerScore, int target, int expected)
    {
        Assert.AreEqual(expected, GameManager.AcceptedEnvidoAward(stake, winnerScore, target));
    }

    [Test]
    public void SpectatorBeforeOpponentCannotReceiveTrickOrCantoWinnerAssignment()
    {
        var local = new Seat(1, false);
        var admin = new Seat(2, true);
        var opponent = new Seat(3, false);
        Assert.AreEqual(3, GameManager.ResolveOpponentActorNumber(new Player[] { local, admin, opponent }, 1));
        Assert.AreEqual(1, GameManager.ResolveOpponentActorNumber(new Player[] { admin, opponent, local }, 3));
    }

    [TestCase(ChallengeType.Truco)]
    [TestCase(ChallengeType.Envido)]
    [TestCase(ChallengeType.Flor)]
    public void UnansweredCantoBlocksCardsEvenWhenPendingFlagWasCleared(ChallengeType challenge)
    {
        var root = new GameObject("UnansweredCantoReleaseRegression");
        root.SetActive(false);
        var instance = typeof(UIMANAGER).GetProperty(nameof(UIMANAGER.Instance));
        var previous = UIMANAGER.Instance;
        try
        {
            var ui = root.AddComponent<UIMANAGER>();
            instance.SetValue(null, ui);
            var game = root.AddComponent<GameManager>();
            ui._isChallengepPending = false;
            ui.unAnsweredChallenges[challenge] = 2;
            Assert.IsFalse(game.CanPlayCard(), "An unanswered canto must lock the cards independently of the UI flag.");
            game.SetCanPlayCard(true);
            Assert.IsFalse(game.CanPlayCard(), "A stale Turn RPC must not bypass a stacked canto.");
            ui.unAnsweredChallenges.Clear();
            game.SetCanPlayCard(true);
            Assert.IsTrue(game.CanPlayCard(), "Cards can resume after the outstanding canto resolves.");
        }
        finally
        {
            instance.SetValue(null, previous);
            Object.DestroyImmediate(root);
        }
    }
}
