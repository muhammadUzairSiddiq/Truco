using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class TrucoTournamentFeedbackTests
{
    [TestCase(false, 0)]
    [TestCase(true, 34)]
    public void SameSuitHandRespectsVariantWithoutChangingFlorValue(bool withFlor, int expected)
    {
        bool previousVariant = OneVsOneMatchSession.WithFlor;
        var host = new GameObject("VariantRegression");
        host.SetActive(false); // Test calculation/guards only; never start Photon or deal a hand.
        try
        {
            var game = host.AddComponent<GameManager>();
            OneVsOneMatchSession.SetWithFlor(withFlor);
            var hand = new List<DeckCards> {
                new DeckCards { suit = CardSuit.Swords, rank = 7 },
                new DeckCards { suit = CardSuit.Swords, rank = 5 },
                new DeckCards { suit = CardSuit.Swords, rank = 2 }
            };
            Assert.AreEqual(withFlor, game.HasFlor(hand));
            Assert.AreEqual(expected, game.CalculateFlor(hand));
            if (!withFlor)
                foreach (var type in new[] { ChallengeType.Flor, ChallengeType.FlorChica,
                             ChallengeType.ContraFlor, ChallengeType.ConFlorQuiero })
                    Assert.DoesNotThrow(() => game.GetScore(type), "Disabled Flor must return before scoring dependencies");
        }
        finally
        {
            Object.DestroyImmediate(host);
            OneVsOneMatchSession.SetWithFlor(previousVariant);
        }
    }

    static TournamentBracketState Seed(int count)
    {
        var state = new TournamentBracketState(count);
        var players = new List<User>();
        for (int i = 0; i < count; i++) players.Add(new User { _id = "p" + i, username = "Jugador " + i });
        state.Seed(players);
        return state;
    }

    [TestCase(4)]
    [TestCase(8)]
    public void AllOpeningTablesReady_AndWinnerWaitsForSibling(int count)
    {
        var state = Seed(count);
        foreach (var match in state.rounds[0]) Assert.IsTrue(match.IsReady);
        Assert.IsTrue(state.RecordWinner(0, 0, "p0", "Jugador 0"));
        Assert.IsFalse(state.rounds[1][0].IsReady);
        Assert.IsFalse(state.RecordWinner(1, 0, "p0", "Jugador 0"));
        Assert.IsTrue(state.RecordWinner(0, 1, "p3", "Jugador 3"));
        Assert.IsTrue(state.rounds[1][0].IsReady);
        Assert.AreEqual("p0", state.rounds[1][0].first.playerId);
        Assert.AreEqual("p3", state.rounds[1][0].second.playerId);
    }

    [TestCase(4)]
    [TestCase(8)]
    public void DuplicateOrForeignWinnerCannotChangeBracket(int count)
    {
        var state = Seed(count);
        Assert.IsFalse(state.RecordWinner(0, 0, "outsider", "Unknown"));
        Assert.IsTrue(state.RecordWinner(0, 0, "p1", "One"));
        Assert.IsFalse(state.RecordWinner(0, 0, "p1", "One"));
        Assert.IsFalse(state.RecordWinner(0, 0, "p0", "Zero"));
        Assert.AreEqual("p1", state.rounds[1][0].first.playerId);
    }

    [TestCase(4)]
    [TestCase(8)]
    public void IndependentBranchMergePreservesBothWinners(int count)
    {
        var left = Seed(count);
        var right = Seed(count);
        left.RecordWinner(0, 0, "p0", "Zero");
        right.RecordWinner(0, 1, "p2", "Two");
        left.MergeResults(right);
        right.MergeResults(left);
        Assert.AreEqual(left.ToJson(), right.ToJson());
        Assert.IsTrue(left.rounds[1][0].IsReady);
        left.MergeResults(Seed(count));
        Assert.IsTrue(left.rounds[1][0].IsReady, "Stale seed must not erase results");
    }

    [TestCase(4)]
    [TestCase(8)]
    public void FullBracketReachesExactlyOneChampion_AndRoundTrips(int count)
    {
        var state = Seed(count);
        for (int r = 0; r < state.rounds.Count; r++)
            foreach (var match in state.rounds[r])
            {
                Assert.IsTrue(match.IsReady);
                Assert.IsTrue(state.RecordWinner(r, match.index, match.first.playerId, match.first.playerName));
            }
        Assert.AreEqual("p0", state.rounds[state.rounds.Count - 1][0].winnerId);
        Assert.AreEqual(state.ToJson(), TournamentBracketState.FromJson(state.ToJson()).ToJson());
    }

    [TestCase(ChallengeType.Flor)]
    [TestCase(ChallengeType.FlorChica)]
    [TestCase(ChallengeType.ConFlorQuiero)]
    [TestCase(ChallengeType.ContraFlor)]
    public void FlorGuardsCoverEveryVariant(ChallengeType type) => Assert.IsTrue(GameManager.IsFlorChallenge(type));

    [TestCase(ChallengeType.Truco)]
    [TestCase(ChallengeType.Envido)]
    [TestCase(ChallengeType.RealEnvido)]
    [TestCase(ChallengeType.FaltaEnvido)]
    public void NonFlorRulesAreNotFiltered(ChallengeType type) => Assert.IsFalse(GameManager.IsFlorChallenge(type));
}
