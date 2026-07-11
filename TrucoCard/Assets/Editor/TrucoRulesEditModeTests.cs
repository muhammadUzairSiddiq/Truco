using System.Collections.Generic;
using NUnit.Framework;

/// <summary>Edit-mode tests for Truco hand winner rules and card hierarchy (official rules reference).</summary>
public class TrucoRulesEditModeTests
{
    [Test]
    public void HandWinner_TwoTricksWonByPlayer1()
    {
        var results = new List<int> { 1, 1 };
        Assert.AreEqual(1, TrucoHandWinner.Evaluate(results, manoPlayer: 1));
    }

    [Test]
    public void HandWinner_FirstTrickParda_SecondTrickDecides()
    {
        Assert.AreEqual(2, TrucoHandWinner.Evaluate(new List<int> { 0, 2 }, 1));
        Assert.AreEqual(1, TrucoHandWinner.Evaluate(new List<int> { 0, 1 }, 2));
    }

    [Test]
    public void HandWinner_WinTrick1_TieTrick2_HandEnds()
    {
        Assert.AreEqual(1, TrucoHandWinner.Evaluate(new List<int> { 1, 0 }, 2));
        Assert.AreEqual(2, TrucoHandWinner.Evaluate(new List<int> { 2, 0 }, 1));
    }

    [Test]
    public void HandWinner_TripleParda_ManoWins()
    {
        Assert.AreEqual(1, TrucoHandWinner.Evaluate(new List<int> { 0, 0, 0 }, manoPlayer: 1));
        Assert.AreEqual(2, TrucoHandWinner.Evaluate(new List<int> { 0, 0, 0 }, manoPlayer: 2));
    }

    [Test]
    public void HandWinner_NeedsThirdTrick_WhenSplit()
    {
        Assert.IsNull(TrucoHandWinner.Evaluate(new List<int> { 1, 2 }, 1));
        Assert.AreEqual(1, TrucoHandWinner.Evaluate(new List<int> { 1, 2, 1 }, 1));
    }

    [Test]
    public void ManoAlternatesEachRound()
    {
        Assert.AreEqual(1, TrucoHandWinner.GetManoPlayerNumber(1));
        Assert.AreEqual(2, TrucoHandWinner.GetManoPlayerNumber(2));
        Assert.AreEqual(1, TrucoHandWinner.GetManoPlayerNumber(3));
    }

    [Test]
    public void CardRank_SwordsAceBeatsAll()
    {
        Assert.Greater(TrucoCardRules.TrucoRank(1, CardSuit.Swords), TrucoCardRules.TrucoRank(7, CardSuit.Swords));
        Assert.Greater(TrucoCardRules.TrucoRank(1, CardSuit.Swords), TrucoCardRules.TrucoRank(3, CardSuit.Cups));
    }

    [Test]
    public void EnvidoValue_FaceCardsAreZero()
    {
        Assert.AreEqual(0, TrucoCardRules.EnvidoValue(10));
        Assert.AreEqual(0, TrucoCardRules.EnvidoValue(11));
        Assert.AreEqual(0, TrucoCardRules.EnvidoValue(12));
        Assert.AreEqual(7, TrucoCardRules.EnvidoValue(7));
    }

    [Test]
    public void NoQuieroPoints_MatchOfficialRules()
    {
        Assert.AreEqual(1, TrucoRulePoints.NoQuieroAward(ChallengeType.Truco));
        Assert.AreEqual(2, TrucoRulePoints.NoQuieroAward(ChallengeType.Retruco));
        Assert.AreEqual(3, TrucoRulePoints.NoQuieroAward(ChallengeType.Vale4));
        Assert.AreEqual(1, TrucoRulePoints.NoQuieroAward(ChallengeType.Envido));
        Assert.AreEqual(2, TrucoRulePoints.NoQuieroAward(ChallengeType.RealEnvido));
        Assert.AreEqual(4, TrucoRulePoints.NoQuieroAward(ChallengeType.ContraFlor));
    }

    [Test]
    public void NoQuieroEndsHand_OnlyTrucoChain()
    {
        Assert.IsTrue(TrucoRulePoints.NoQuieroEndsHand(ChallengeType.Truco));
        Assert.IsTrue(TrucoRulePoints.NoQuieroEndsHand(ChallengeType.Vale4));
        Assert.IsFalse(TrucoRulePoints.NoQuieroEndsHand(ChallengeType.Envido));
        Assert.IsFalse(TrucoRulePoints.NoQuieroEndsHand(ChallengeType.Flor));
    }
}
