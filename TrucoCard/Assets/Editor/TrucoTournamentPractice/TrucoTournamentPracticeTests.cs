using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

public class TrucoTournamentPracticeTests
{
    [TestCase(4, 15, false)] [TestCase(4, 30, true)]
    [TestCase(8, 15, true)] [TestCase(8, 30, false)]
    public void AIPlaysEveryMatchAndCompletesTheBracket(int capacity, int target, bool flor)
    {
        var tournament = new TrucoPracticeTournament(capacity, target, flor, 1731);
        tournament.CompleteWithBots();
        Assert.AreEqual(capacity - 1, tournament.CompletedMatches);
        Assert.Greater(tournament.Actions, capacity * 10);
        foreach (var round in tournament.Bracket.rounds) foreach (var match in round) Assert.IsTrue(match.IsFinished);
        var duplicate = new TrucoPracticeTournament(capacity, target, flor, 1731); duplicate.CompleteWithBots();
        Assert.AreEqual(tournament.Bracket.ToJson(), duplicate.Bracket.ToJson(), "Seeded test runs must replay consistently.");
        int actions = tournament.Actions; tournament.CompleteWithBots(); Assert.AreEqual(actions, tournament.Actions, "Completed matches cannot be paid/advanced again even in practice.");
    }
    [Test]
    public void PendingChallengeLocksCardsAndCannotResolveTwice()
    {
        var game = new TrucoPracticeMatch(99, 30, false);
        int caller = game.Turn;
        Assert.IsTrue(game.CallTruco(caller));
        Assert.IsFalse(game.Play(caller, 0));
        Assert.IsFalse(game.Play(1 - caller, 0));
        Assert.IsFalse(game.CallEnvido(caller));
        Assert.IsTrue(game.Reply(1 - caller, true)); Assert.AreEqual(2, game.Stake);
        Assert.IsFalse(game.Reply(1 - caller, true));
        Assert.IsTrue(game.Fold(caller)); Assert.AreEqual(2, game.Scores[1 - caller]);
        Assert.IsFalse(game.Fold(caller));
    }
    [Test]
    public void DealContainsSixDifferentCardsAndSinFlorNeverScoresFlor()
    {
        for (int seed = 1; seed <= 100; seed++)
        {
            var game = new TrucoPracticeMatch(seed, 15, false);
            Assert.AreEqual(6, game.Hands.SelectMany(h => h).Select(c => c.suit + ":" + c.rank).Distinct().Count());
            Assert.IsFalse(game.HasFlor(0)); Assert.IsFalse(game.HasFlor(1));
            Assert.AreEqual(0, game.Scores.Sum());
        }
    }
    [Test]
    public void PracticeUsesTheSameFortyCardFacesAsGameplay()
    {
        foreach (CardSuit suit in Enum.GetValues(typeof(CardSuit))) foreach (int rank in new[] { 1, 2, 3, 4, 5, 6, 7, 10, 11, 12 })
            Assert.IsNotNull(TrucoEditorCardArt.Face(new DeckCards { suit = suit, rank = rank }), suit + " " + rank);
    }
    [Test]
    public void SettingsAndAllPracticeSourcesAreEditorOnlyAndNotBuildDependencies()
    {
        Assert.IsNotNull(TrucoTournamentPracticeSettings.Load());
        Assert.IsTrue(TrucoTournamentPracticeSettings.AssetPath.StartsWith("Assets/Editor/"));
        Assert.IsNull(AssetDatabase.LoadMainAssetAtPath("Assets/Resources/TrucoDevelopmentSettings.asset"));
        var dependencies = AssetDatabase.GetDependencies(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(), true);
        Assert.IsFalse(dependencies.Contains(TrucoTournamentPracticeSettings.AssetPath));
        Assert.IsTrue(typeof(TrucoPracticeMatch).Assembly.GetName().Name.Contains("Editor"));
    }
}
