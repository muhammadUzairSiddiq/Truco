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
        Assert.AreEqual(1, TrucoRulePoints.NoQuieroAward(ChallengeType.RealEnvido));
        Assert.AreEqual(1, TrucoRulePoints.NoQuieroAward(ChallengeType.FaltaEnvido));
        Assert.AreEqual(4, TrucoRulePoints.NoQuieroAward(ChallengeType.ContraFlor));
    }

    [Test]
    public void EnvidoNoQuiero_AcceptanceSequences()
    {
        // REAL ENVIDO → NO QUIERO → 1
        Assert.AreEqual(1, TrucoRulePoints.EnvidoNoQuieroAward(ChallengeType.RealEnvido, 3, 1));
        // FALTA ENVIDO → NO QUIERO → 1
        Assert.AreEqual(1, TrucoRulePoints.EnvidoNoQuieroAward(ChallengeType.FaltaEnvido, 0, 1));
        // ENVIDO → ENVIDO → NO QUIERO → 2
        Assert.AreEqual(2, TrucoRulePoints.EnvidoNoQuieroAward(ChallengeType.Envido, 4, 2));
        // REAL ENVIDO → REAL ENVIDO → NO QUIERO → 3
        Assert.AreEqual(3, TrucoRulePoints.EnvidoNoQuieroAward(ChallengeType.RealEnvido, 6, 2));
        // ENVIDO → ENVIDO → FALTA → NO QUIERO → 4
        Assert.AreEqual(4, TrucoRulePoints.EnvidoNoQuieroAward(ChallengeType.FaltaEnvido, 4, 3));
        // REAL → REAL → FALTA → NO QUIERO → 6
        Assert.AreEqual(6, TrucoRulePoints.EnvidoNoQuieroAward(ChallengeType.FaltaEnvido, 6, 3));
        // ENVIDO → REAL → FALTA → NO QUIERO → 5
        Assert.AreEqual(5, TrucoRulePoints.EnvidoNoQuieroAward(ChallengeType.FaltaEnvido, 5, 3));
    }

    [Test]
    public void MazoAfterEnvido_MustNotReuseEnvidoStake()
    {
        Assert.AreEqual(1, TrucoRulePoints.MazoAwardForAcceptedTrucoLevel(0));
        Assert.IsTrue(TrucoRulePoints.IsEnvidoFamily(ChallengeType.FaltaEnvido));
        Assert.IsFalse(TrucoRulePoints.IsEnvidoFamily(ChallengeType.Truco));
    }

    [Test]
    public void MazoAfterAcceptedTruco_AwardsChallengeValue()
    {
        Assert.AreEqual(1, TrucoRulePoints.MazoAwardForAcceptedTrucoLevel(0));
        Assert.AreEqual(2, TrucoRulePoints.MazoAwardForAcceptedTrucoLevel(1));
        Assert.AreEqual(3, TrucoRulePoints.MazoAwardForAcceptedTrucoLevel(2));
        Assert.AreEqual(4, TrucoRulePoints.MazoAwardForAcceptedTrucoLevel(3));
        Assert.AreEqual(0, TrucoRulePoints.AcceptedTrucoLevel(false, false, false));
        Assert.AreEqual(1, TrucoRulePoints.AcceptedTrucoLevel(true, false, false));
        Assert.AreEqual(2, TrucoRulePoints.AcceptedTrucoLevel(true, true, false));
        Assert.AreEqual(3, TrucoRulePoints.AcceptedTrucoLevel(true, true, true));
    }

    /// <summary>Mazo at the start of the first trick: the Envido part is scored independently.</summary>
    [Test]
    public void MazoOnFirstTrick_SplitsEnvidoAndTrucoParts()
    {
        // Case A: fold before any card and before any canto → 1 (envido) + 1 (truco) = 2.
        Assert.AreEqual(1, TrucoRulePoints.MazoUnplayedEnvidoPoint(0, false, false, false, false));
        Assert.AreEqual(1, TrucoRulePoints.MazoAwardForAcceptedTrucoLevel(0));

        // Case B: Truco was called but never accepted → only the 1 point Truco part.
        Assert.AreEqual(0, TrucoRulePoints.MazoUnplayedEnvidoPoint(0, false, true, false, false));

        // Case C: Envido / Real Envido / Falta Envido called but unresolved → 1 + 1 = 2.
        Assert.AreEqual(1, TrucoRulePoints.MazoUnplayedEnvidoPoint(0, false, false, false, false));

        // Case D: Flor replaces the Envido phase and pays its own 3 → only the 1 point Truco part.
        Assert.AreEqual(0, TrucoRulePoints.MazoUnplayedEnvidoPoint(0, false, false, false, true));

        // Envido already settled (e.g. Envido → Falta Envido → No Quiero) is never charged twice.
        Assert.AreEqual(0, TrucoRulePoints.MazoUnplayedEnvidoPoint(0, false, false, true, false));

        // After the first card the Envido phase is closed.
        Assert.AreEqual(0, TrucoRulePoints.MazoUnplayedEnvidoPoint(0, true, false, false, false));

        // An accepted Truco chain supersedes the Envido part entirely.
        Assert.AreEqual(0, TrucoRulePoints.MazoUnplayedEnvidoPoint(1, false, false, false, false));
        Assert.AreEqual(0, TrucoRulePoints.MazoUnplayedEnvidoPoint(3, false, false, false, false));
    }

    [Test]
    public void NoQuieroEndsHand_OnlyTrucoChain()
    {
        Assert.IsTrue(TrucoRulePoints.NoQuieroEndsHand(ChallengeType.Truco));
        Assert.IsTrue(TrucoRulePoints.NoQuieroEndsHand(ChallengeType.Vale4));
        Assert.IsFalse(TrucoRulePoints.NoQuieroEndsHand(ChallengeType.Envido));
        Assert.IsFalse(TrucoRulePoints.NoQuieroEndsHand(ChallengeType.Flor));
    }

    [TestCase(0, 15)]
    [TestCase(15, 15)]
    [TestCase(29, 15)]
    [TestCase(30, 30)]
    public void MatchTarget_NormalizesToSupportedModes(int requested, int expected)
    {
        Assert.AreEqual(expected, TrucoMatchRules.NormalizeTargetScore(requested));
    }

    [Test]
    public void MatchEnd_Default15_AndSelectable30()
    {
        Assert.IsTrue(TrucoMatchRules.HasReachedTarget(15, 15));
        Assert.IsFalse(TrucoMatchRules.HasReachedTarget(15, 30));
        Assert.IsTrue(TrucoMatchRules.HasReachedTarget(30, 30));
    }

    [TestCase(16, 15, 15)]
    [TestCase(16, 30, 16)]
    [TestCase(35, 30, 30)]
    [TestCase(-1, 15, 0)]
    public void MatchScore_ClampsToSelectedTarget(int score, int target, int expected)
    {
        Assert.AreEqual(expected, TrucoMatchRules.ClampScoreToTarget(score, target));
    }

    [Test]
    public void EnvidoTie_ManoWins_NotFoot()
    {
        Assert.AreEqual(10, TrucoMatchRules.ResolveEnvidoWinnerActor(29, 29, 10, 20, 10));
        Assert.AreEqual(20, TrucoMatchRules.ResolveEnvidoWinnerActor(29, 29, 10, 20, 20));
    }

    [Test]
    public void ScoringCards_StayHiddenUntilHandEndOrMazo()
    {
        Assert.IsFalse(TrucoMatchRules.ShouldRevealScoringCards(false, false));
        Assert.IsTrue(TrucoMatchRules.ShouldRevealScoringCards(true, false));
        Assert.IsTrue(TrucoMatchRules.ShouldRevealScoringCards(false, true));
    }

    [Test]
    public void FlorThenMazo_PendingTrucoAwardsOnePoint()
    {
        // Acceptance #16: after Flor, Mazo vs unanswered Truco = 1 (not 2).
        Assert.AreEqual(1, TrucoRulePoints.NoQuieroAward(ChallengeType.Truco));
    }

    [Test]
    public void DefaultTargetRemainsFifteen()
    {
        Assert.AreEqual(15, TrucoMatchRules.DefaultTargetScore);
        Assert.AreEqual(15, TrucoMatchRules.NormalizeTargetScore(0));
        Assert.AreEqual(15, TrucoMatchRules.NormalizeTargetScore(15));
        Assert.AreEqual(30, TrucoMatchRules.NormalizeTargetScore(30));
    }
}
